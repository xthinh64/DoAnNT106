using System.Collections.Concurrent;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using OnlineChessGame.Network;
using OnlineChessGame.Server;
using Xunit;

public sealed class StartupTests
{
    [Fact]
    public async Task SynchronousCompatibilityStartupDoesNotDeadlockAnStaContext()
    {
        using var cert = CreateCertificate();
        await using var workers = new GameWorkerPool();
        var store = new AsyncStore();
        await using var server = CreateServer(cert, workers, store);
        using var context = new QueuedContext();
        using var returned = new ManualResetEventSlim();
        Exception? error = null;
        var thread = new Thread(() =>
        {
            SynchronizationContext.SetSynchronizationContext(context);
            try { server.Start(); }
            catch (Exception ex) { error = ex; }
            finally { returned.Set(); }
        }) { IsBackground = true };
        if (OperatingSystem.IsWindows()) thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        await store.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        store.Release.TrySetResult();
        bool completedWithoutPumping = returned.Wait(TimeSpan.FromSeconds(2));
        // Release the original deadlock so a RED run leaves no blocked thread/resources.
        var cleanup = DateTime.UtcNow.AddSeconds(5);
        while (!returned.IsSet && DateTime.UtcNow < cleanup)
        {
            context.Pump();
            Thread.Sleep(10);
        }
        Assert.True(thread.Join(TimeSpan.FromSeconds(1)), "STA startup thread did not exit");
        Assert.Null(error);
        Assert.True(completedWithoutPumping, "Startup deadlocked waiting for a continuation on its blocked STA synchronization context");
    }

    [Fact]
    public async Task AsyncStartupKeepsStaResponsiveAndDoesNotListenBeforeRecoveryCompletes()
    {
        using var cert = CreateCertificate();
        await using var workers = new GameWorkerPool();
        var store = new AsyncStore();
        await using var server = CreateServer(cert, workers, store);
        using var context = new QueuedContext();
        using var returned = new ManualResetEventSlim();
        using var heartbeat = new ManualResetEventSlim();
        using var stopPump = new ManualResetEventSlim();
        Task? startup = null;
        var thread = new Thread(() =>
        {
            SynchronizationContext.SetSynchronizationContext(context);
            startup = server.StartAsync();
            returned.Set();
            context.Post(_ => heartbeat.Set(), null);
            while (!stopPump.IsSet) { context.Pump(); Thread.Sleep(1); }
            context.Pump();
        }) { IsBackground = true };
        if (OperatingSystem.IsWindows()) thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        try
        {
            await store.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(returned.Wait(TimeSpan.FromSeconds(2)), "StartAsync blocked the STA caller");
            Assert.True(heartbeat.Wait(TimeSpan.FromSeconds(2)), "STA stopped servicing messages during recovery");
            Assert.False(startup!.IsCompleted);
            Assert.Equal(0, server.Port); // An ephemeral endpoint is not bound until recovery succeeds.
            store.Release.TrySetResult();
            await startup.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(server.Port > 0);
            using var client = new System.Net.Sockets.TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, server.Port).WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            store.Release.TrySetResult();
            stopPump.Set();
            Assert.True(thread.Join(TimeSpan.FromSeconds(5)), "STA message loop did not exit");
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedOrCancelledRecoveryNeverOpensListener(bool cancel)
    {
        using var cert = CreateCertificate();
        await using var workers = new GameWorkerPool();
        var store = new AsyncStore();
        await using var server = CreateServer(cert, workers, store);
        using var lifetime = new CancellationTokenSource();
        var startup = server.StartAsync(lifetime.Token);
        await store.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        if (cancel)
        {
            lifetime.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => startup.WaitAsync(TimeSpan.FromSeconds(5)));
        }
        else
        {
            store.Release.TrySetException(new IOException("Injected recovery read failure"));
            var error = await Assert.ThrowsAsync<IOException>(() => startup.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Equal("Injected recovery read failure", error.Message);
        }
        Assert.Equal(0, server.Port);
    }

    [Fact]
    public async Task RecoveryDeadlineCancelsAStoreThatNeverCompletes()
    {
        using var cert = CreateCertificate();
        await using var workers = new GameWorkerPool();
        var store = new AsyncStore();
        await using var server = CreateServer(cert, workers, store);
        var startup = server.StartAsync();
        await store.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => startup.WaitAsync(TimeSpan.FromSeconds(40)));
        Assert.Equal(0, server.Port);
    }

    [Fact]
    public async Task DisposeJoinsPendingStartupBeforeDisposingCancellationSource()
    {
        using var cert = CreateCertificate();
        await using var workers = new GameWorkerPool();
        var store = new AsyncStore(deferCancellation: true);
        var server = CreateServer(cert, workers, store);
        var startup = server.StartAsync();
        await store.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var disposal = server.DisposeAsync().AsTask();
        bool joinedStartup = !disposal.IsCompleted;
        store.Release.TrySetResult();
        var error = await Record.ExceptionAsync(() => startup.WaitAsync(TimeSpan.FromSeconds(5)));
        await disposal.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.IsAssignableFrom<OperationCanceledException>(error);
        Assert.True(joinedStartup, "DisposeAsync returned before pending startup finished");
        Assert.Equal(0, server.Port);
    }

    [Fact]
    public async Task RepeatedStartSharesStartupAndDoesNotRepeatRecovery()
    {
        using var cert = CreateCertificate();
        await using var workers = new GameWorkerPool();
        var store = new AsyncStore();
        await using var server = CreateServer(cert, workers, store);
        var first = server.StartAsync();
        await store.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var second = server.StartAsync();
        store.Release.TrySetResult();
        await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Same(first, second);
        Assert.Same(first, server.StartAsync());
    }

    private static GameServer CreateServer(X509Certificate2 cert, GameWorkerPool workers, IMatchStore store) =>
        new(IPAddress.Loopback, SslStreamCertificateContext.Create(cert, null, offline: true),
            new GameSessions(new FixtureAuth()), new FixtureAuth(), workers, 0, store);

    private static X509Certificate2 CreateCertificate()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1));
    }

    private sealed class FixtureAuth : IAuthService
    {
        public Task<AuthResponse> HandleAsync(AuthRequest request, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class QueuedContext : SynchronizationContext, IDisposable
    {
        private readonly ConcurrentQueue<(SendOrPostCallback Callback, object? State)> queue = new();
        public override void Post(SendOrPostCallback d, object? state) => queue.Enqueue((d, state));
        public void Pump() { while (queue.TryDequeue(out var item)) item.Callback(item.State); }
        public void Dispose() => Pump();
    }

    private sealed class AsyncStore(bool deferCancellation = false) : IMatchStore
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<IReadOnlyList<OpenMatchRecord>> ReadOpenMatchesAsync(CancellationToken ct)
        {
            Entered.TrySetResult();
            // Model a store read whose cancellation cleanup is still pending.
            if (deferCancellation) await Release.Task;
            else await Release.Task.WaitAsync(ct);
            return Array.Empty<OpenMatchRecord>();
        }
        public Task CreateAsync(string id, string mode, int first, int second, MatchState state, CancellationToken ct) => throw new NotSupportedException();
        public Task<MatchState?> ReadAsync(string id, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> UpdateAsync(string id, MatchState previous, MatchState next, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> TryReclaimAsync(string id, int generation, MatchState state, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> AbortMatchAsync(string id, int generation, MatchState state, CancellationToken ct) => throw new NotSupportedException();
    }
}
