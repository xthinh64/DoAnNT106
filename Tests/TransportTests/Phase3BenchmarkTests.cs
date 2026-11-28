using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using MySqlConnector;
using OnlineChessGame.Network;
using OnlineChessGame.Server;
using Xunit;

// Opt-in, real loopback TLS -> Gateway -> worker processes -> disposable MySQL.
public sealed class Phase3BenchmarkTests
{
    public sealed class BenchmarkFactAttribute : FactAttribute
    {
        public BenchmarkFactAttribute()
        {
            if (Environment.GetEnvironmentVariable("PHASE3_BENCHMARK") != "1")
                Skip = "Opt in with PHASE3_BENCHMARK=1 and MATCH_TEST_MYSQL on isolated MySQL.";
        }
    }

    [BenchmarkFact]
    public async Task RealTlsMySqlMoveStateCapacityAndRecovery()
    {
        Assert.False(string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MATCH_TEST_MYSQL")));
        int capacity = Setting("PHASE3_CAPACITY", 3, 2, 32);
        int samples = Setting("PHASE3_SAMPLES", 10, 1, 40);
        int longHistory = Setting("PHASE3_LONG_HISTORY", 60, 2, 100);
        string output = Environment.GetEnvironmentVariable("PHASE3_OUTPUT") ?? Path.Combine(AppContext.BaseDirectory, "phase3-evidence");
        Directory.CreateDirectory(output);
        var rows = new List<object>();
        var raw = new List<Sample>();
        var failures = new List<string>();
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(12));
        var ct = deadline.Token;
        var connectionString = Environment.GetEnvironmentVariable("MATCH_TEST_MYSQL")
            ?? throw new InvalidOperationException("MATCH_TEST_MYSQL is required for the benchmark.");
        var config = new MySqlConnectionStringBuilder(connectionString);
        Assert.Contains(config.Server, new[] { "localhost", "127.0.0.1", "::1" });
        config.Database = ""; config.Pooling = false;
        await using var admin = new MySqlConnection(config.ConnectionString);
        await admin.OpenAsync(ct);
        string database = $"phase3_bench_{Guid.NewGuid():N}";
        await new MySqlCommand($"CREATE DATABASE `{database}`", admin).ExecuteNonQueryAsync(ct);
        string mysqlVersion = (string)(await new MySqlCommand("SELECT VERSION()", admin).ExecuteScalarAsync(ct))!;
        try
        {
            config.Database = database;
            await using (var schema = new MySqlConnection(config.ConnectionString))
            {
                await schema.OpenAsync(ct);
                var migration = await File.ReadAllLinesAsync(Path.Combine(AppContext.BaseDirectory, "online_matches.sql"), ct);
                await new MySqlCommand(string.Join('\n', migration.Skip(1)), schema).ExecuteNonQueryAsync(ct);
            }
            foreach (int history in new[] { 2, longHistory }.Distinct())
            foreach (int rooms in new[] { 1, capacity - 1, capacity }.Distinct())
            {
                string scenario = $"history-{history}_rooms-{rooms}";
                var store = new TimedStore(new MySqlMatchStore(config.ConnectionString));
                try { rows.Add(await RunAsync(scenario, history, rooms, capacity, samples, store, raw, ct)); }
                catch (Exception ex) { failures.Add($"{scenario}: {ex.GetType().Name}: {ex.Message}"); }
                // No open matches may leak between scenarios into startup recovery.
                await using var clean = new MySqlConnection(config.ConnectionString);
                await clean.OpenAsync(ct);
                await new MySqlCommand("DELETE FROM OnlineMatches", clean).ExecuteNonQueryAsync(ct);
            }
        }
        finally
        {
            await new MySqlCommand($"DROP DATABASE `{database}`", admin).ExecuteNonQueryAsync(CancellationToken.None);
            var report = new { commit = Environment.GetEnvironmentVariable("PHASE3_COMMIT") ?? "unrecorded", timestampUtc = DateTimeOffset.UtcNow,
                topology = "single-host loopback; two worker PIDs; TLS clients; real MySQL; NOT multi-PC LAN", framework = RuntimeInformation.FrameworkDescription,
                os = RuntimeInformation.OSDescription, processors = Environment.ProcessorCount, mysqlVersion, capacity, admissionCapacity = capacity,
                samplesPerRoom = samples, longHistory, game = "CARO", auth = "in-process fixture (not account DB)",
                percentileMethod = "nearest rank; MOVE send to both matching STATE revisions; DB UpdateAsync wall time; worker apply is heartbeat average only",
                scenarios = rows, failures, rawSampleCount = raw.Count };
            await File.WriteAllTextAsync(Path.Combine(output, "benchmark.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
            await File.WriteAllLinesAsync(Path.Combine(output, "samples.csv"), new[] { "scenario,metric,room,revision,milliseconds" }.Concat(raw.Select(s => $"{s.Scenario},{s.Metric},{s.Room},{s.Revision},{s.Milliseconds.ToString("F6", CultureInfo.InvariantCulture)}")));
        }
        Assert.Empty(failures);
    }

    private static async Task<object> RunAsync(string scenario, int history, int rooms, int capacity, int samples, TimedStore store, List<Sample> raw, CancellationToken ct)
    {
        using var cert = Certificate(); using var trust = new X509Certificate2(cert.RawData);
        await using var workers = new GameWorkerPool(capacity);
        int p1 = Port(), p2 = Port(); while (p1 == p2) p2 = Port();
        workers.Start(p1, p2, typeof(GameServer).Assembly.Location);
        await workers.WaitUntilReadyAsync(ct);
        workers.SetDraining(1, true);
        var auth = new Auth();
        await using var gateway = new GameServer(IPAddress.Loopback, SslStreamCertificateContext.Create(cert, null, offline: true), new GameSessions(auth), auth, workers, 0, store);
        gateway.Start(ct);
        var client = new GatewayClient($"127.0.0.1:{gateway.Port}", trust);
        var sessions = new List<Room>();
        var clients = new List<TcpClient>();
        var streams = new List<SslStream>();
        try
        {
            for (int i = 0; i < rooms; i++)
            {
                var a = await Connect(i * 2 + 1); var b = await Connect(i * 2 + 2);
                await GameWire.WriteAsync(a, new GamePacket(PacketType.FIND_MATCH, GameMode: "CARO"), ct);
                await GameWire.WriteAsync(b, new GamePacket(PacketType.FIND_MATCH, GameMode: "CARO"), ct);
                var match = await Read(a, PacketType.MATCH_FOUND, ct); await Read(b, PacketType.MATCH_FOUND, ct);
                bool first = match.Payload.StartsWith("FIRST|", StringComparison.Ordinal);
                sessions.Add(new Room(match.RoomId, first ? a : b, first ? b : a));
            }
            var coordinates = Enumerable.Range(0, 225).Select(n => (x: n % 15, y: n / 15)).ToArray();
            var firstMoves = coordinates.Where(p => (p.x + p.y / 2) % 2 == 0).ToArray();
            var secondMoves = coordinates.Where(p => (p.x + p.y / 2) % 2 != 0).ToArray();
            async Task Play(Room room, int revision, bool measure)
            {
                var p = (revision % 2 == 0 ? firstMoves : secondMoves)[revision / 2];
                long start = Stopwatch.GetTimestamp();
                await GameWire.WriteAsync(revision % 2 == 0 ? room.First : room.Second,
                    new GamePacket(PacketType.MOVE, RoomId: room.Id, Payload: $"{p.x},{p.y}", ExpectedRevision: revision), ct);
                var a = (await Read(room.First, PacketType.STATE, ct)).State!;
                var b = (await Read(room.Second, PacketType.STATE, ct)).State!;
                Assert.Equal(revision + 1, a.Revision); Assert.Equal(a, b); Assert.Null(a.Result);
                double elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                var saved = await store.ReadAsync(room.Id, ct);
                Assert.Equal(a.Revision, saved!.Revision); Assert.Equal(a.Moves, saved.Moves);
                if (measure) lock (raw) raw.Add(new(scenario, "move_state_ms", room.Id, a.Revision, elapsed));
            }
            for (int move = 0; move < history; move++) await Task.WhenAll(sessions.Select(r => Play(r, move, false)));
            store.Durations.Clear();
            for (int move = history; move < history + samples; move++) await Task.WhenAll(sessions.Select(r => Play(r, move, true)));
            var before = new[] { workers.GetSnapshot(0), workers.GetSnapshot(1) };
            workers.SetDraining(1, false);
            long recoveryStart = Stopwatch.GetTimestamp();
            using (var process = Process.GetProcessById(workers.GetSnapshot(0).ProcessId!.Value)) { process.Kill(); await process.WaitForExitAsync(ct); }
            await Task.WhenAll(sessions.Select(async room => {
                await Read(room.First, PacketType.RECOVERING, ct); await Read(room.Second, PacketType.RECOVERING, ct);
                var a = (await Read(room.First, PacketType.STATE, ct)).State!; var b = (await Read(room.Second, PacketType.STATE, ct)).State!;
                Assert.Equal(a, b); Assert.Equal(history + samples, a.Revision); Assert.Equal(2, a.Generation);
                lock (raw) raw.Add(new(scenario, "recovery_ms", room.Id, a.Revision, Stopwatch.GetElapsedTime(recoveryStart).TotalMilliseconds));
            }));
            await Task.WhenAll(sessions.Select(r => Play(r, history + samples, true)));
            lock (raw) raw.AddRange(store.Durations.Select(ms => new Sample(scenario, "db_update_ms", "all", -1, ms)));
            return new { scenario, rooms, history, before, after = new[] { workers.GetSnapshot(0), workers.GetSnapshot(1) },
                metrics = raw.Where(s => s.Scenario == scenario).GroupBy(s => s.Metric).ToDictionary(g => g.Key, g => Stats(g.Select(s => s.Milliseconds))), failedRequests = 0 };
        }
        finally { foreach (var s in streams) s.Dispose(); foreach (var c in clients) c.Dispose(); }
        async Task<SslStream> Connect(int id)
        {
            var tcp = new TcpClient(); clients.Add(tcp);
            var login = await client.SendAsync(new AuthRequest("signin", id.ToString(CultureInfo.InvariantCulture), "benchmark-only"), ct);
            var ticket = await client.RequestTicketAsync(login.SessionToken!, ct);
            var tls = await client.ConnectAsync(tcp, ct); streams.Add(tls);
            await GameWire.WriteAsync(tls, new GatewayRequest(GatewayRequest.CurrentVersion, "game", Token: ticket.Token), ct);
            await Read(tls, PacketType.AUTHENTICATED, ct); return tls;
        }
    }
    internal static object Stats(IEnumerable<double> source)
    {
        var v = source.Order().ToArray();
        double P(double p) => v[Math.Max(0, (int)Math.Ceiling(p * v.Length) - 1)];
        return new { count = v.Length, p50 = P(.50), p95 = P(.95), p99 = P(.99), max = v[^1] };
    }
    private static int Setting(string key, int fallback, int min, int max) { int v = int.TryParse(Environment.GetEnvironmentVariable(key), out var n) ? n : fallback; return v >= min && v <= max ? v : throw new ArgumentOutOfRangeException(key); }
    private static int Port() { using var l = new TcpListener(IPAddress.Loopback, 0); l.Start(); return ((IPEndPoint)l.LocalEndpoint).Port; }
    private static async Task<GamePacket> Read(Stream s, PacketType type, CancellationToken ct) { var p = await GameWire.ReadAsync<GamePacket>(s, ct); Assert.NotNull(p); Assert.Equal(type, p.Type); return p; }
    private static X509Certificate2 Certificate()
    {
        using var rsa = RSA.Create(2048); var request = new CertificateRequest("CN=Phase3", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var san = new SubjectAlternativeNameBuilder(); san.AddIpAddress(IPAddress.Loopback); request.CertificateExtensions.Add(san.Build());
        using var generated = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
        return new X509Certificate2(generated.Export(X509ContentType.Pfx), (string?)null, X509KeyStorageFlags.UserKeySet);
    }
    private sealed record Room(string Id, SslStream First, SslStream Second);
    private sealed record Sample(string Scenario, string Metric, string Room, long Revision, double Milliseconds);
    private sealed class Auth : IAuthService { public Task<AuthResponse> HandleAsync(AuthRequest r, CancellationToken ct) => Task.FromResult(new AuthResponse(true, "ok", "", new PlayerProfile(int.Parse(r.Username), r.Username, "", 1200, 1200))); }
    private sealed class TimedStore(IMatchStore inner) : IMatchStore
    {
        public List<double> Durations { get; } = new();
        public Task CreateAsync(string id, string mode, int a, int b, MatchState initial, CancellationToken ct) => inner.CreateAsync(id, mode, a, b, initial, ct);
        public Task<MatchState?> ReadAsync(string id, CancellationToken ct) => inner.ReadAsync(id, ct);
        public Task<IReadOnlyList<OpenMatchRecord>> ReadOpenMatchesAsync(CancellationToken ct) => inner.ReadOpenMatchesAsync(ct);
        public Task<bool> TryReclaimAsync(string id, int generation, MatchState next, CancellationToken ct) => inner.TryReclaimAsync(id, generation, next, ct);
        public Task<bool> AbortMatchAsync(string id, int generation, MatchState next, CancellationToken ct) => inner.AbortMatchAsync(id, generation, next, ct);
        public async Task<bool> UpdateAsync(string id, MatchState previous, MatchState next, CancellationToken ct) { long start = Stopwatch.GetTimestamp(); try { return await inner.UpdateAsync(id, previous, next, ct); } finally { lock (Durations) Durations.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds); } }
    }
}
