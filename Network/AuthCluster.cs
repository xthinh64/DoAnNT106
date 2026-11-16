using System.Collections.Concurrent;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace OnlineChessGame.Network;

/// <summary>Loopback TCP pass-through gateway and two TLS workers. The caller owns the certificate.</summary>
public sealed class AuthCluster : IAsyncDisposable
{
    private readonly IAuthService service;
    private readonly X509Certificate2 certificate;
    private readonly TcpListener gateway, worker1, worker2;
    private readonly CancellationTokenSource stop = new();
    private readonly SemaphoreSlim gatewaySlots = new(128);
    private readonly SemaphoreSlim worker1Slots = new(128);
    private readonly SemaphoreSlim worker2Slots = new(128);
    private readonly ConcurrentDictionary<long, Task> connections = new();
    private readonly List<Task> loops = new();
    private CancellationTokenRegistration registration;
    private long sequence, worker1Requests, worker2Requests;
    private int route, state;
    public int GatewayPort => ((IPEndPoint)gateway.LocalEndpoint).Port;
    public int Worker1Port => ((IPEndPoint)worker1.LocalEndpoint).Port;
    public int Worker2Port => ((IPEndPoint)worker2.LocalEndpoint).Port;
    public long Worker1Requests => Interlocked.Read(ref worker1Requests);
    public long Worker2Requests => Interlocked.Read(ref worker2Requests);

    public AuthCluster(IAuthService service, X509Certificate2 certificate, int gatewayPort = 8888, int worker1Port = 9001, int worker2Port = 9002, IPAddress? bindAddress = null)
    {
        this.service = service ?? throw new ArgumentNullException(nameof(service));
        this.certificate = certificate ?? throw new ArgumentNullException(nameof(certificate));
        if (!certificate.HasPrivateKey) throw new ArgumentException("Server certificate requires a private key.", nameof(certificate));
        var bind = bindAddress ?? IPAddress.Loopback;
        gateway = new(bind, gatewayPort);
        worker1 = new(IPAddress.Loopback, worker1Port);
        worker2 = new(IPAddress.Loopback, worker2Port);
    }
    public Task StartAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (Interlocked.CompareExchange(ref state, 1, 0) != 0) throw new InvalidOperationException("Cluster may only be started once.");
        try
        {
            worker1.Start(128); worker2.Start(128); gateway.Start(128);
            registration = ct.Register(() => stop.Cancel());
            loops.Add(AcceptAsync(worker1, worker1Slots, tcp => WorkerAsync(tcp, true)));
            loops.Add(AcceptAsync(worker2, worker2Slots, tcp => WorkerAsync(tcp, false)));
            loops.Add(AcceptAsync(gateway, gatewaySlots, GatewayAsync));
            return Task.CompletedTask;
        }
        catch { stop.Cancel(); gateway.Stop(); worker1.Stop(); worker2.Stop(); throw; }
    }
    private async Task AcceptAsync(TcpListener listener, SemaphoreSlim slots, Func<TcpClient, Task> handler)
    {
        try
        {
            while (!stop.IsCancellationRequested)
            {
                await slots.WaitAsync(stop.Token);
                TcpClient tcp;
                try { tcp = await listener.AcceptTcpClientAsync(stop.Token); }
                catch { slots.Release(); throw; }
                long id = Interlocked.Increment(ref sequence);
                // Register before removal continuation, even if handling finishes synchronously.
                var task = RunAsync(tcp, slots, handler);
                connections[id] = task;
                _ = task.ContinueWith(_ => { connections.TryRemove(id, out var ignored); }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or SocketException or ObjectDisposedException) { }
    }
    private async Task RunAsync(TcpClient tcp, SemaphoreSlim slots, Func<TcpClient, Task> handler)
    {
        using (tcp)
        {
            try { await handler(tcp); }
            // A malformed/disconnected peer or failing auth implementation cannot kill a listener.
            catch (Exception) { }
            finally { slots.Release(); }
        }
    }
    private CancellationTokenSource Deadline()
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(stop.Token);
        cts.CancelAfter(TimeSpan.FromSeconds(10));
        return cts;
    }
    private async Task WorkerAsync(TcpClient tcp, bool first)
    {
        using var deadline = Deadline();
        var ct = deadline.Token;
        using var tls = new SslStream(tcp.GetStream(), false);
        await tls.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
        {
            ServerCertificate = certificate,
            EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
            ClientCertificateRequired = false
        }, ct);
        var request = await Wire.ReadAsync<AuthRequest>(tls, ct);
        if (string.IsNullOrWhiteSpace(request.Action) || string.IsNullOrWhiteSpace(request.Username) || request.Password is null)
        {
            await Wire.WriteAsync(tls, new AuthResponse(false, "INVALID_REQUEST", "Action, username and password are required."), ct);
            return;
        }
        if (first) Interlocked.Increment(ref worker1Requests); else Interlocked.Increment(ref worker2Requests);
        var response = await service.HandleAsync(request, ct).WaitAsync(ct);
        await Wire.WriteAsync(tls, response, ct);
    }
    private async Task GatewayAsync(TcpClient tcp)
    {
        using var deadline = Deadline();
        var ct = deadline.Token;
        int first = (Interlocked.Increment(ref route) & 1) == 1 ? Worker1Port : Worker2Port;
        int second = first == Worker1Port ? Worker2Port : Worker1Port;
        using var upstream = new TcpClient();
        var address = IPAddress.Loopback;
        // No downstream bytes are read before a successful connect; fallback is safe only here.
        try { await upstream.ConnectAsync(address, first, ct); }
        catch (SocketException) { await upstream.ConnectAsync(address, second, ct); }
        using var downstream = tcp.GetStream();
        using var target = upstream.GetStream();
        var outgoing = downstream.CopyToAsync(target, ct);
        var incoming = target.CopyToAsync(downstream, ct);
        await Task.WhenAny(outgoing, incoming);
        deadline.Cancel();
        try { await Task.WhenAll(outgoing, incoming); }
        catch (OperationCanceledException) { }
    }
    public async Task StopAsync()
    {
        stop.Cancel();
        gateway.Stop(); worker1.Stop(); worker2.Stop();
        await Task.WhenAll(loops);
        await Task.WhenAll(connections.Values);
    }
    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        registration.Dispose();
        // Keep cancellation/semaphore usable for idempotent Stop/Dispose calls.
    }
}
