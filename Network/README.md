# Authentication transport (.NET 8)

Namespace: `OnlineChessGame.Network`. No WinForms, database, or password hashing dependency.

## Exact integration API

```csharp
public sealed record AuthRequest(string Action, string Username, string Password, string? DisplayName = null);
public sealed record PlayerProfile(int UserId, string Username, string DisplayName, int ChessElo, int CaroElo);
public sealed record AuthResponse(bool Success, string Code, string Message, PlayerProfile? Player = null);
public interface IAuthService {
    Task<AuthResponse> HandleAsync(AuthRequest request, CancellationToken ct);
}
public sealed class AuthClient {
    public AuthClient(string endpoint, string certificatePin);
    public Task<AuthResponse> SendAsync(AuthRequest request, CancellationToken ct = default);
}
public sealed class AuthCluster : IAsyncDisposable {
    public AuthCluster(IAuthService service, X509Certificate2 certificate,
        int gatewayPort = 8888, int worker1Port = 9001, int worker2Port = 9002,
        IPAddress? bindAddress = null);
    public int GatewayPort { get; }
    public int Worker1Port { get; }
    public int Worker2Port { get; }
    public long Worker1Requests { get; }
    public long Worker2Requests { get; }
    public Task StartAsync(CancellationToken ct = default);
    public Task StopAsync();
    public ValueTask DisposeAsync();
}
```

The parent executable should reference `Network/OnlineChessGame.Network.csproj`, implement `IAuthService`, load a provisioned certificate with its private key, and keep that certificate alive until after disposing the cluster. There is deliberately no `Server/Program.cs` or server project here, to avoid conflicts with the persistence/executable implementation. `IAuthService` must support concurrent calls and honor cancellation. Transport does not log credentials or exceptions.

```csharp
await using var cluster = new AuthCluster(authService, serverCertificate);
await cluster.StartAsync(shutdownToken);
// Remain alive until application shutdown, then dispose cluster.
var client = new AuthClient("127.0.0.1:8888", trustedCertificateSha256Hex);
AuthResponse response = await client.SendAsync(new("login", username, password));
```

## Security and protocol

- Gateway defaults to loopback TCP 8888 and passes encrypted bytes to round-robin loopback TLS workers 9001/9002. All ports accept 0 for OS-assigned ports; read actual properties after start.
- Both workers use the same caller-owned certificate. TLS 1.2/1.3 only. Production certificates are provisioned externally; the self-signed generator exists only in tests. On Windows, load PFX with a persisted/user key rather than ephemeral key storage for Schannel.
- Client pin is exactly 64 hexadecimal characters: SHA-256 of the full DER certificate, NOT SPKI or SHA-1 thumbprint. Obtain through `cert.GetCertHashString(HashAlgorithmName.SHA256)` over a trusted provisioning channel. Pin match is the trust policy (including self-signed deployments); it intentionally replaces PKI hostname/chain trust. Wrong/missing/malformed pins fail closed.
- Endpoints require DNS/IPv4 `host:port`, or bracketed `[IPv6]:port`; no implicit port or scheme.
- One request and response per TLS connection. Frames are 4-byte signed positive big-endian lengths followed by UTF-8 `System.Text.Json` JSON, default case-sensitive PascalCase record field names. Maximum payload 16384 bytes. Null/malformed/oversized frames close that connection.
- Ten-second total connection deadline, bounded 128 active connections per listener with bounded listen backlog. Separate admission budgets prevent gateway connections starving worker acceptance. Invalid peers cannot terminate accept loops.
- Fallback occurs only when TCP worker connect fails, before forwarding any bytes. No request replay, no retry after TLS/auth failure. This is TCP-connect availability routing, not an independent health-probe service.
- Cancellation/Stop closes listeners, cancels in-flight network I/O, and awaits tracked handlers. Start is one-shot. Bind address is optional; exposing beyond loopback requires firewall/deployment review.

## Verified commands (repository root)

```sh
dotnet build Network/OnlineChessGame.Network.csproj --nologo
dotnet test Tests/TransportTests/TransportTests.csproj --nologo
```

Real loopback tests cover auth/profile round-trip, pin rejection before auth, round-robin routing, unavailable-worker fallback, malformed/oversized/negative/zero frames, listener shutdown with an idle peer, explicit endpoint validation, and worker admission under 70 idle gateway connections.
