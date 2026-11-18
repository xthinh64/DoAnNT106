using System.Buffers.Binary;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Text.Json;

namespace OnlineChessGame.Network;

internal static class Wire
{
    internal const int MaxFrame = 16 * 1024;
    internal static async Task<T> ReadAsync<T>(Stream stream, CancellationToken ct)
    {
        var header = new byte[4];
        await stream.ReadExactlyAsync(header, ct);
        int size = BinaryPrimitives.ReadInt32BigEndian(header);
        if (size <= 0 || size > MaxFrame) throw new InvalidDataException("Invalid frame length.");
        var payload = new byte[size];
        await stream.ReadExactlyAsync(payload, ct);
        try { return JsonSerializer.Deserialize<T>(payload) ?? throw new InvalidDataException("Null JSON frame."); }
        catch (JsonException ex) { throw new InvalidDataException("Malformed JSON frame.", ex); }
    }
    internal static async Task WriteAsync<T>(Stream stream, T value, CancellationToken ct)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(value);
        if (payload.Length > MaxFrame) throw new InvalidDataException("Frame exceeds 16 KiB.");
        var header = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(header, payload.Length);
        await stream.WriteAsync(header, ct);
        await stream.WriteAsync(payload, ct);
    }
}

/// <summary>One connection and one request per call; never retries an authentication request.</summary>
public sealed class AuthClient
{
    private readonly string host;
    private readonly int port;
    private readonly byte[] pin;
    public AuthClient(string endpoint, string certificatePin)
    {
        if (string.IsNullOrWhiteSpace(endpoint)) throw new ArgumentException("Expected host:port.", nameof(endpoint));
        int split = endpoint.LastIndexOf(':');
        if (split <= 0 || !int.TryParse(endpoint[(split + 1)..], out port) || port is < 1 or > 65535)
            throw new ArgumentException("Expected host:port with a valid explicit port.", nameof(endpoint));
        host = endpoint[..split];
        if (host.StartsWith('[') && host.EndsWith(']')) host = host[1..^1];
        else if (host.Contains(':')) throw new ArgumentException("IPv6 addresses must be bracketed.", nameof(endpoint));
        if (string.IsNullOrWhiteSpace(host) || host.Contains('/') || host.Contains('[') || host.Contains(']')) throw new ArgumentException("Invalid host.", nameof(endpoint));
        try { pin = Convert.FromHexString(certificatePin); }
        catch (Exception ex) when (ex is FormatException or ArgumentNullException) { throw new ArgumentException("Pin must be a SHA-256 certificate DER hex digest.", nameof(certificatePin), ex); }
        if (pin.Length != 32) throw new ArgumentException("Pin must contain 32 bytes.", nameof(certificatePin));
    }
    public async Task<AuthResponse> SendAsync(AuthRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        using var tcp = new TcpClient();
        await tcp.ConnectAsync(host, port, timeout.Token);
        using var tls = new SslStream(tcp.GetStream(), false, (_, certificate, _, _) =>
            certificate is not null && CryptographicOperations.FixedTimeEquals(SHA256.HashData(certificate.GetRawCertData()), pin));
        await tls.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
        {
            TargetHost = host,
            EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
        }, timeout.Token);
        await Wire.WriteAsync(tls, request, timeout.Token);
        var response = await Wire.ReadAsync<AuthResponse>(tls, timeout.Token);
        if (response.Code is null || response.Message is null || (response.Success && response.Player is null))
            throw new InvalidDataException("Invalid authentication response contract.");
        return response;
    }
}
