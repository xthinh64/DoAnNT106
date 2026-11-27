using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using OnlineChessGame.Network;

namespace OnlineChessGame.Server;

internal sealed record WorkerRequest(string Secret, WorkerRoomRequest? Room = null);
internal sealed record WorkerRoomRequest(string RoomId, string Mode, string First, string Second);
internal sealed record WorkerHeartbeat(bool Ready, int Rooms, int Sessions, int Capacity,
    double CpuPercent = 0, double AvgApplyMs = 0, int InFlightRequests = 0);

internal sealed class WorkerMetrics
{
    private readonly Process process = Process.GetCurrentProcess();
    private readonly object gate = new();
    private TimeSpan lastCpuTime;
    private long lastCpuSample;
    private double avgApplyMs;
    private int inFlight;

    public WorkerMetrics()
    {
        lastCpuTime = process.TotalProcessorTime;
        lastCpuSample = Stopwatch.GetTimestamp();
    }

    public void EnterRequest() => Interlocked.Increment(ref inFlight);
    public void ExitRequest() => Interlocked.Decrement(ref inFlight);

    public void RecordApply(double elapsedMs)
    {
        lock (gate)
            avgApplyMs = avgApplyMs == 0 ? elapsedMs : avgApplyMs * 0.8 + elapsedMs * 0.2;
    }

    public WorkerHeartbeat Snapshot(int rooms, int capacity)
    {
        double cpuPercent;
        double applyMs;
        lock (gate)
        {
            long nowStamp = Stopwatch.GetTimestamp();
            TimeSpan nowCpu = process.TotalProcessorTime;
            double wallMs = Stopwatch.GetElapsedTime(lastCpuSample, nowStamp).TotalMilliseconds;
            double cpuMs = (nowCpu - lastCpuTime).TotalMilliseconds;
            cpuPercent = wallMs > 0 ? Math.Clamp(cpuMs / (wallMs * Environment.ProcessorCount) * 100.0, 0.0, 100.0) : 0.0;
            lastCpuSample = nowStamp;
            lastCpuTime = nowCpu;
            applyMs = avgApplyMs;
        }
        return new WorkerHeartbeat(true, rooms, 2 * rooms, capacity, cpuPercent, applyMs, Math.Max(0, Volatile.Read(ref inFlight)));
    }
}

public static class GameWorker
{
    public static async Task RunAsync(int port, int capacity)
    {
        string secret = await Console.In.ReadLineAsync() ?? throw new InvalidOperationException("Missing worker bootstrap.");
        using var stop = new CancellationTokenSource();
        _ = Task.Run(async () =>
        {
            await Console.In.ReadLineAsync();
            stop.Cancel();
        });
        var listener = new TcpListener(IPAddress.Loopback, port);
        var slots = new SemaphoreSlim(capacity, capacity);
        var metrics = new WorkerMetrics();
        listener.Start();
        try
        {
            while (!stop.IsCancellationRequested)
            {
                var tcp = await listener.AcceptTcpClientAsync(stop.Token);
                _ = ServeAsync(tcp, secret, slots, capacity, metrics, stop.Token);
            }
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        finally { listener.Stop(); }
    }

    private static async Task ServeAsync(TcpClient tcp, string secret, SemaphoreSlim slots, int capacity, WorkerMetrics metrics, CancellationToken ct)
    {
        using (tcp)
        try
        {
            using var setup = CancellationTokenSource.CreateLinkedTokenSource(ct);
            setup.CancelAfter(TimeSpan.FromSeconds(3));
            var stream = tcp.GetStream();
            var request = await GameWire.ReadAsync<WorkerRequest>(stream, setup.Token);
            if (request?.Secret is not { Length: 64 } || !CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(request.Secret), Encoding.ASCII.GetBytes(secret)))
                return;

            if (request.Room is not { } room)
            {
                int rooms = capacity - slots.CurrentCount;
                await GameWire.WriteAsync(stream, metrics.Snapshot(rooms, capacity), setup.Token);
                return;
            }
            if (!slots.Wait(0))
            {
                await GameWire.WriteAsync(stream, false, setup.Token);
                return;
            }
            try
            {
                await GameWire.WriteAsync(stream, true, setup.Token);
                while (await GameWire.ReadAsync<GamePacket>(stream, ct) is { } packet)
                {
                    if (packet.RoomId != room.RoomId || packet.GameMode != room.Mode ||
                        (packet.Sender != room.First && packet.Sender != room.Second)) return;
                    if (packet.State is not { } state)
                    {
                        await GameWire.WriteAsync(stream, packet, ct);
                        continue;
                    }
                    metrics.EnterRequest();
                    GamePacket response;
                    try
                    {
                        long applyStart = Stopwatch.GetTimestamp();
                        int side = packet.Sender == room.First ? 1 : 2;
                        response = packet with { State = MatchRules.Apply(state, room.Mode, side, packet) };
                        metrics.RecordApply(Stopwatch.GetElapsedTime(applyStart).TotalMilliseconds);
                    }
                    catch (InvalidOperationException ex)
                    {
                        response = packet with { Type = PacketType.ERROR, State = null, Payload = ex.Message };
                    }
                    finally { metrics.ExitRequest(); }
                    await GameWire.WriteAsync(stream, response, ct);
                }
            }
            finally { slots.Release(); }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or SocketException or OperationCanceledException or System.Text.Json.JsonException) { }
    }
}

internal sealed class WorkerRoom(TcpClient tcp, Action released, Func<bool> available, Action markFailed) : IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private int disposed;
    public bool IsAvailable => Volatile.Read(ref disposed) == 0 && available();
    public void MarkFailed() => markFailed();

    public async Task<GamePacket> RelayAsync(GamePacket packet, CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
            deadline.CancelAfter(TimeSpan.FromSeconds(3));
            var stream = tcp.GetStream();
            await GameWire.WriteAsync(stream, packet, deadline.Token);
            return await GameWire.ReadAsync<GamePacket>(stream, deadline.Token) ?? throw new IOException("Worker disconnected.");
        }
        finally { gate.Release(); }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        tcp.Dispose();
        released();
    }
}
