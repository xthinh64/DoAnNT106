using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using OnlineChessGame.Network;

namespace OnlineChessGame.Server;

public enum WorkerStatus { Starting, Ready, Full, Draining, Offline }

public sealed record WorkerSnapshot(int Port, int? ProcessId, WorkerStatus Status, int Rooms, int Sessions,
    int Capacity, int ReservedRooms, TimeSpan? HeartbeatAge, TimeSpan? HeartbeatLatency, bool Draining);

public sealed class GameWorkerPool : IAsyncDisposable
{
    public const int DefaultRoomCapacity = 32;
    public const int MaxRoomCapacity = 127;
    private static readonly TimeSpan ProbeInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromMilliseconds(800);
    private static readonly TimeSpan Freshness = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan RestartDelay = TimeSpan.FromSeconds(3);

    private sealed class Instance(Process process, string secret)
    {
        public Process Process { get; } = process;
        public string Secret { get; } = secret;
        public WorkerHeartbeat? Heartbeat;
        public long SampleTimestamp;
        public TimeSpan Latency;
        public bool Responsive;
        public int Reservations;
    }

    private sealed class Worker(int port)
    {
        public int Port { get; } = port;
        public bool Draining;
        public bool Failed;
        public long? RetryStarted;
        public Instance? Current;
    }

    private readonly object gate = new();
    private readonly CancellationTokenSource stop = new();
    private readonly int capacity;
    private Worker[] workers = [];
    private string? workerAssembly;
    private Task? monitoring;
    private uint next;

    public GameWorkerPool(int capacity = DefaultRoomCapacity)
    {
        if (capacity is < 1 or > MaxRoomCapacity) throw new ArgumentOutOfRangeException(nameof(capacity));
        this.capacity = capacity;
    }

    public void Start(int firstPort, int secondPort, string? workerAssembly = null)
    {
        if (monitoring is not null) throw new InvalidOperationException("Workers already started.");
        if (firstPort is < 1 or > 65535 || secondPort is < 1 or > 65535 || firstPort == secondPort)
            throw new ArgumentException("Two distinct worker ports are required.");
        this.workerAssembly = workerAssembly;
        workers = [new(firstPort), new(secondPort)];
        monitoring = Task.Run(MonitorAsync);
    }

    public async Task WaitUntilReadyAsync(CancellationToken ct)
    {
        if (monitoring is null) throw new InvalidOperationException("Workers have not started.");
        var started = Stopwatch.GetTimestamp();
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var snapshots = new[] { GetSnapshot(0), GetSnapshot(1) };
            if (snapshots.All(w => w.Status == WorkerStatus.Ready)) return;
            if (Stopwatch.GetElapsedTime(started) >= TimeSpan.FromSeconds(5))
            {
                if (snapshots.Any(w => w.Status == WorkerStatus.Ready)) return;
                throw new InvalidOperationException("Không có game worker khởi động thành công.");
            }
            await Task.Delay(50, ct);
        }
    }

    public void SetDraining(int index, bool draining)
    {
        lock (gate) workers[index].Draining = draining;
    }

    public WorkerSnapshot GetSnapshot(int index)
    {
        lock (gate)
        {
            var worker = workers[index];
            var instance = worker.Current;
            bool exited = instance?.Process.HasExited ?? worker.Failed;
            var heartbeat = instance?.Heartbeat;
            TimeSpan? age = heartbeat is null ? null : Stopwatch.GetElapsedTime(instance!.SampleTimestamp);
            var status = exited || instance is { Heartbeat: not null } && !IsResponsive(instance)
                ? WorkerStatus.Offline
                : heartbeat is null ? WorkerStatus.Starting
                : worker.Draining ? WorkerStatus.Draining
                : AtCapacity(instance!) ? WorkerStatus.Full : WorkerStatus.Ready;
            return new(worker.Port, exited ? null : instance?.Process.Id, status, heartbeat?.Rooms ?? 0, heartbeat?.Sessions ?? 0,
                capacity, instance?.Reservations ?? 0, age, heartbeat is null ? null : instance!.Latency, worker.Draining);
        }
    }

    private async Task MonitorAsync()
    {
        try
        {
            while (!stop.IsCancellationRequested)
            {
                await Task.WhenAll(workers.Select(ProbeAsync));
                await Task.Delay(ProbeInterval, stop.Token);
            }
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
    }

    private async Task ProbeAsync(Worker worker)
    {
        Instance instance;
        lock (gate)
        {
            if (worker.Current?.Process.HasExited == true)
            {
                CloseBootstrap(worker.Current.Process);
                worker.Current.Process.Dispose();
                worker.Current = null;
                worker.Failed = true;
                worker.RetryStarted = Stopwatch.GetTimestamp();
            }
            if (worker.Current is null)
            {
                if (worker.RetryStarted is long retry && Stopwatch.GetElapsedTime(retry) < RestartDelay) return;
                try { worker.Current = Launch(worker.Port); }
                catch (Exception ex) when (ex is IOException or InvalidOperationException or Win32Exception)
                {
                    worker.Failed = true;
                    worker.RetryStarted = Stopwatch.GetTimestamp();
                    return;
                }
            }
            instance = worker.Current;
        }

        long started = Stopwatch.GetTimestamp();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stop.Token);
        timeout.CancelAfter(ProbeTimeout);
        try
        {
            using var tcp = await ConnectAsync(worker.Port, new(instance.Secret), timeout.Token);
            var heartbeat = await GameWire.ReadAsync<WorkerHeartbeat>(tcp.GetStream(), timeout.Token);
            if (heartbeat is not { Ready: true } || heartbeat.Capacity != capacity ||
                heartbeat.Rooms < 0 || heartbeat.Rooms > capacity || heartbeat.Sessions != 2 * heartbeat.Rooms)
                throw new InvalidDataException("Invalid game worker heartbeat.");
            lock (gate)
            {
                instance.Heartbeat = heartbeat;
                instance.SampleTimestamp = Stopwatch.GetTimestamp();
                instance.Latency = Stopwatch.GetElapsedTime(started);
                instance.Responsive = true;
                worker.Failed = false;
            }
        }
        catch (Exception ex) when (IsWorkerIoError(ex))
        {
            lock (gate)
            {
                instance.Responsive = false;
                worker.Failed = true;
            }
        }
    }

    internal async Task<WorkerRoom> OpenRoomAsync(GameRoom room, CancellationToken ct)
    {
        var tried = new HashSet<Worker>();
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            Worker? worker;
            Instance instance;
            lock (gate)
            {
                if (workers.Length == 0) break;
                int start = (int)(next++ % (uint)workers.Length);
                worker = workers.Skip(start).Concat(workers.Take(start))
                    .Where(w => !tried.Contains(w) && !w.Draining &&
                        w.Current is { } current && IsResponsive(current) && !AtCapacity(current))
                    .OrderBy(w => Load(w.Current!)).ThenBy(w => w.Current!.Latency).FirstOrDefault();
                if (worker is null) break;
                tried.Add(worker);
                instance = worker.Current!;
                instance.Reservations++;
            }

            TcpClient? tcp = null;
            bool transferred = false;
            try
            {
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct, stop.Token);
                deadline.CancelAfter(TimeSpan.FromSeconds(2));
                var request = new WorkerRoomRequest(room.RoomId, room.GameMode, room.Player1.SessionId, room.Player2.SessionId);
                tcp = await ConnectAsync(worker.Port, new(instance.Secret, request), deadline.Token);
                if (!await GameWire.ReadAsync<bool>(tcp.GetStream(), deadline.Token)) continue;
                var reservedRoom = new WorkerRoom(tcp, () => Release(instance), () =>
                {
                    lock (gate) return ReferenceEquals(worker.Current, instance) && IsResponsive(instance);
                }, () =>
                {
                    lock (gate) instance.Responsive = false;
                });
                transferred = true;
                return reservedRoom;
            }
            catch (Exception ex) when (IsWorkerIoError(ex))
            {
                ct.ThrowIfCancellationRequested();
                if (!stop.IsCancellationRequested)
                    lock (gate) instance.Responsive = false;
            }
            finally
            {
                if (!transferred)
                {
                    tcp?.Dispose();
                    Release(instance);
                }
            }
        }
        throw new IOException("Game server đang bận hoặc bảo trì. Vui lòng tìm trận lại.");
    }

    private void Release(Instance instance)
    {
        lock (gate) instance.Reservations--;
    }

    private bool AtCapacity(Instance instance) => Load(instance) >= capacity;
    private static int Load(Instance instance) => Math.Max(instance.Reservations, instance.Heartbeat?.Rooms ?? 0);
    private static bool IsResponsive(Instance instance) => instance.Responsive && !instance.Process.HasExited &&
        Stopwatch.GetElapsedTime(instance.SampleTimestamp) <= Freshness;
    private static bool IsWorkerIoError(Exception ex) =>
        ex is IOException or InvalidDataException or SocketException or OperationCanceledException or JsonException;

    private Instance Launch(int port)
    {
        string executable = workerAssembly is null
            ? Environment.ProcessPath ?? throw new InvalidOperationException("Unknown server executable.")
            : Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet";
        var info = new ProcessStartInfo(executable) { UseShellExecute = false, RedirectStandardInput = true, CreateNoWindow = true };
        if (workerAssembly is not null) info.ArgumentList.Add(workerAssembly);
        else if (Path.GetFileNameWithoutExtension(executable).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            info.ArgumentList.Add(Assembly.GetEntryAssembly()!.Location);
        info.ArgumentList.Add("--game-worker");
        info.ArgumentList.Add(port.ToString(CultureInfo.InvariantCulture));
        info.ArgumentList.Add(capacity.ToString(CultureInfo.InvariantCulture));
        foreach (string key in new[] { "CHESS_DB_CONNECTION", "CHESS_CERT_PASSWORD", "CHESS_CERT_PATH" })
            info.Environment.Remove(key);
        string secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var process = Process.Start(info) ?? throw new InvalidOperationException("Could not start game worker.");
        try
        {
            process.StandardInput.WriteLine(secret);
            return new Instance(process, secret);
        }
        catch
        {
            CloseBootstrap(process);
            process.Dispose();
            throw;
        }
    }

    private static void CloseBootstrap(Process process)
    {
        try { process.StandardInput.Close(); } catch (IOException) { }
    }

    private static async Task<TcpClient> ConnectAsync(int port, WorkerRequest request, CancellationToken ct)
    {
        var tcp = new TcpClient();
        try
        {
            await tcp.ConnectAsync(IPAddress.Loopback, port, ct);
            await GameWire.WriteAsync(tcp.GetStream(), request, ct);
            return tcp;
        }
        catch { tcp.Dispose(); throw; }
    }

    public async ValueTask DisposeAsync()
    {
        stop.Cancel();
        if (monitoring is not null) await monitoring;
        foreach (var worker in workers)
        {
            if (worker.Current is not { } instance) continue;
            using var process = instance.Process;
            CloseBootstrap(process);
            try
            {
                using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                await process.WaitForExitAsync(deadline.Token);
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(); } catch (InvalidOperationException) { }
                await process.WaitForExitAsync();
            }
        }
        stop.Dispose();
    }
}
