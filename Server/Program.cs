using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using OnlineChessGame.Auth;
using OnlineChessGame.Network;
using OnlineChessGame.Server;

try
{
    if (args is ["--game-worker", var workerPort, var workerCapacity])
    {
        await GameWorker.RunAsync(int.Parse(workerPort, System.Globalization.CultureInfo.InvariantCulture),
            int.Parse(workerCapacity, System.Globalization.CultureInfo.InvariantCulture));
        return 0;
    }
    EnvironmentFile.LoadForServer();
    var bind = ServerNetworkOptions.ResolveBindAddress(Environment.GetEnvironmentVariable("CHESS_BIND_ADDRESS"));
    string certPath = Required("CHESS_CERT_PATH");
    string certPassword = Required("CHESS_CERT_PASSWORD");
    if (args is ["--create-cert"])
    {
        using var rsa = RSA.Create(3072);
        var request = new CertificateRequest("CN=Chess Auth", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var san = ServerNetworkOptions.CreateSubjectAlternativeNames(bind,
            Environment.GetEnvironmentVariable("CHESS_CERT_HOSTS"));
        request.CertificateExtensions.Add(san.Build());
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
        var usages = new System.Security.Cryptography.OidCollection { new("1.3.6.1.5.5.7.3.1") };
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(usages, false));
        using var created = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddYears(1));
        // CreateNew refuses to overwrite a provisioned identity. Protect this directory with OS ACLs.
        using var output = new FileStream(certPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        output.Write(created.Export(X509ContentType.Pfx, certPassword));
        Console.WriteLine("Created a development PFX with localhost, loopback, concrete bind IP and CHESS_CERT_HOSTS identities.");
        return 0;
    }
    if (args.Length != 0) throw new ArgumentException("Usage: server [--create-cert]");
    using var certificate = new GatewayCertificate(certPath, certPassword);
    string connection = Required("CHESS_DB_CONNECTION");
    bool allowLegacy = string.Equals(Environment.GetEnvironmentVariable("AUTH_ALLOW_LEGACY_MIGRATION"), "true", StringComparison.OrdinalIgnoreCase);
    await DatabaseReadiness.CheckAsync(connection, allowLegacyMigration: allowLegacy);
    var service = new AuthAdmissionService(new MySqlAuthService(connection, allowLegacyMigration: allowLegacy));
    var sessions = new GameSessions(service);
    // One shared global budget for both workers: max 4 active, burst 20, refill 5/s; no queue.
    using var stop = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; stop.Cancel(); };
    var configuredCapacity = Environment.GetEnvironmentVariable("CHESS_WORKER_MAX_ROOMS");
    int capacity = configuredCapacity is null ? GameWorkerPool.DefaultRoomCapacity :
        int.Parse(configuredCapacity, System.Globalization.CultureInfo.InvariantCulture);
    await using var workers = new GameWorkerPool(capacity);
    workers.Start(9001, 9002);
    await workers.WaitUntilReadyAsync(stop.Token);
    await using var gateway = new GameServer(bind, certificate.Context, sessions, service, workers,
        matchStore: new MySqlMatchStore(connection));
    await gateway.StartAsync(stop.Token);
    Console.WriteLine($"Gateway ready: {bind}:{gateway.Port}; loopback game workers 9001/9002");

    try { await Task.Delay(Timeout.Infinite, stop.Token); }
    catch (OperationCanceledException) { }
    return 0;
}
catch (DatabaseReadinessException ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}
catch (ArgumentException ex) when (ex.ParamName is "bindAddress" or "certificateHosts")
{
    // These messages are controlled by ServerNetworkOptions and contain no input values or secrets.
    Console.Error.WriteLine($"Server startup failed: {ex.Message}");
    return 1;
}
catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException or InvalidOperationException or IOException or CryptographicException or System.Net.Sockets.SocketException or OperationCanceledException or System.ComponentModel.Win32Exception)
{
    // Never print connection strings, passwords, or provider exception messages.
    Console.Error.WriteLine($"Server startup failed ({ex.GetType().Name}). Check required environment settings, certificate and available ports.");
    return 1;
}

static string Required(string name) => Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
    ? value : throw new InvalidOperationException($"Missing {name}");
