using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using OnlineChessGame.Auth;
using OnlineChessGame.Network;
using OnlineChessGame.Server;

try
{
    string certPath = Required("CHESS_CERT_PATH");
    string certPassword = Required("CHESS_CERT_PASSWORD");
    if (args is ["--create-cert"])
    {
        using var rsa = RSA.Create(3072);
        var request = new CertificateRequest("CN=Chess Auth", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
        var usages = new System.Security.Cryptography.OidCollection { new("1.3.6.1.5.5.7.3.1") };
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(usages, false));
        using var created = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddYears(1));
        // CreateNew refuses to overwrite a provisioned identity. Protect this directory with OS ACLs.
        using var output = new FileStream(certPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        output.Write(created.Export(X509ContentType.Pfx, certPassword));
        Console.WriteLine($"CHESS_CERT_PIN={Convert.ToHexString(SHA256.HashData(created.RawData))}");
        return 0;
    }
    if (args.Length != 0) throw new ArgumentException("Usage: server [--create-cert]");
    using var certificate = new X509Certificate2(certPath, certPassword, X509KeyStorageFlags.UserKeySet);
    if (DateTime.UtcNow < certificate.NotBefore.ToUniversalTime() || DateTime.UtcNow > certificate.NotAfter.ToUniversalTime())
        throw new InvalidOperationException("Certificate is outside its validity period.");
    string connection = Required("CHESS_DB_CONNECTION");
    bool allowLegacy = string.Equals(Environment.GetEnvironmentVariable("AUTH_ALLOW_LEGACY_MIGRATION"), "true", StringComparison.OrdinalIgnoreCase);
    var service = new AuthAdmissionService(new MySqlAuthService(connection, allowLegacyMigration: allowLegacy));
    // One shared global budget for both workers: max 4 active, burst 20, refill 5/s; no queue.
    var bind = IPAddress.Parse(Environment.GetEnvironmentVariable("CHESS_BIND_ADDRESS") ?? "127.0.0.1");
    using var stop = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; stop.Cancel(); };
    await using var cluster = new AuthCluster(service, certificate, bindAddress: bind);
    await cluster.StartAsync(stop.Token);
    Console.WriteLine($"Auth ready: gateway {bind}:{cluster.GatewayPort}; workers {cluster.Worker1Port}/{cluster.Worker2Port}");
    Console.WriteLine($"CHESS_CERT_PIN={Convert.ToHexString(SHA256.HashData(certificate.RawData))}");

    // Game Server chạy cổng 9000 song song với AuthCluster
    int gamePort = int.TryParse(Environment.GetEnvironmentVariable("CHESS_GAME_PORT"), out int gp) ? gp : 9000;
    await using var gameServer = new GameServer(bind, gamePort);
    gameServer.Start(stop.Token);
    Console.WriteLine($"Game ready: {bind}:{gameServer.Port}");

    try { await Task.Delay(Timeout.Infinite, stop.Token); }
    catch (OperationCanceledException) { }
    Console.WriteLine($"Stopped. Worker requests: {cluster.Worker1Requests}/{cluster.Worker2Requests}");
    return 0;
}
catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException or CryptographicException or System.Net.Sockets.SocketException)
{
    // Never print connection strings, passwords, or provider exception messages.
    Console.Error.WriteLine($"Server startup failed ({ex.GetType().Name}). Check required environment settings, certificate and available ports.");
    return 1;
}

static string Required(string name) => Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
    ? value : throw new InvalidOperationException($"Missing {name}");
