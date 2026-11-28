using System.Net.Security;
using System.Net.Sockets;
using OnlineChessGame.Network;

namespace OnlineChessGame;

public sealed class GameClient
{
    public static GameClient Instance { get; } = new();

    private TcpClient? _tcp;
    private SslStream? _stream;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly SemaphoreSlim _connectLock = new(1, 1);
    private CancellationTokenSource _lifetime = new();
    private Task? _reconnectTask;
    public long ConnectionVersion { get; private set; }
    private string? authenticatedSession;
    private string? abandonedRoomId;
    public bool IsConnected => _stream is not null && _tcp is { Connected: true };
    public event Action<string>? OnRecoveryFailed;

    public string CurrentUsername { get; private set; } = string.Empty;
    public string CurrentRoomId { get; set; } = string.Empty;
    public MatchState? CurrentMatchState { get; private set; }
    public bool IsResumedMatch { get; private set; }
    public string? MatchQueueAttemptId { get; private set; }
    public bool IsRecovering { get; private set; }
    public int MatchSide { get; private set; }

    public event Action<string, string, int, bool, string>? OnMatchFound; // roomId, opponent, elo, first, mode
    public event Action<string>? OnMoveReceived;
    public event Action<string, string>? OnChatReceived;
    public event Action<string>? OnSurrenderReceived;
    public event Action<string>? OnDrawRequestReceived;
    public event Action<bool>? OnDrawResponseReceived;
    public event Action<string>? OnOpponentLeft;
    public event Action? OnDisconnected;
    public event Action<string>? OnMatchUnavailable;
    public event Action<MatchState, string>? OnMatchState;
    public event Action? OnRecovering;

    public async Task ConnectAsync(string username, CancellationToken ct = default)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _lifetime.Token);
        ct = linked.Token;
        await _connectLock.WaitAsync(ct);
        try
        {
            ct.ThrowIfCancellationRequested();
            if (IsConnected && authenticatedSession == AuthConnection.SessionToken) return;
            if (authenticatedSession != AuthConnection.SessionToken)
            {
                abandonedRoomId = null;
                CurrentRoomId = string.Empty;
                CurrentMatchState = null;
            }
            CloseTransport();
            CurrentUsername = string.IsNullOrWhiteSpace(username) ? "Player" : username;
            var tcp = new TcpClient();
            _tcp = tcp;
            SslStream? tls = null;
            try
            {
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
                deadline.CancelAfter(TimeSpan.FromSeconds(10));
                var gateway = AuthConnection.CreateClient();
                string session = AuthConnection.SessionToken ?? throw new InvalidOperationException("Vui lòng đăng nhập lại.");
                var ticket = await gateway.RequestTicketAsync(session, deadline.Token);
                if (ticket.Token is null) throw new InvalidOperationException("Phiên game hết hạn. Vui lòng đăng nhập lại.");
                tls = await gateway.ConnectAsync(tcp, deadline.Token);
                await GameWire.WriteAsync(tls, new GatewayRequest(GatewayRequest.CurrentVersion, "game", Token: ticket.Token), deadline.Token);
                var reply = await GameWire.ReadAsync<GamePacket>(tls, deadline.Token);
                if (reply?.Type != PacketType.AUTHENTICATED)
                    throw new InvalidOperationException("Phiên game hết hạn. Vui lòng đăng nhập lại.");
                ct.ThrowIfCancellationRequested();
                if (!ReferenceEquals(tcp, _tcp) || AuthConnection.SessionToken != session) throw new OperationCanceledException();
                if (reply.Player is { } refreshed && AuthConnection.Player?.UserId == refreshed.UserId)
                    AuthConnection.Player = refreshed;
                authenticatedSession = session;
                _stream = tls;
                _ = Task.Run(() => ListenLoopAsync(tcp, tls));
            }
            catch
            {
                tls?.Dispose();
                tcp.Dispose();
                if (ReferenceEquals(tcp, _tcp)) CloseTransport();
                throw;
            }
        }
        finally { _connectLock.Release(); }
    }

    private void StartReconnect()
    {
        if (_reconnectTask is { IsCompleted: false }) return;
        var token = _lifetime.Token;
        _reconnectTask = Task.Run(() => ReconnectAsync(token));
    }

    private async Task ReconnectAsync(CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(90));
        var retry = deadline.Token;
        try
        {
            while (true)
            {
                retry.ThrowIfCancellationRequested();
                try
                {
                    await ConnectAsync(CurrentUsername, retry);
                    // AUTHENTICATED alone does not prove that a retained room resumed.
                    while (IsConnected && IsRecovering) await Task.Delay(250, retry);
                    if (IsConnected) return;
                }
                catch (Exception ex) when (ex is IOException or SocketException or TimeoutException or OperationCanceledException) { }
                await Task.Delay(1000, retry);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex) when (ex is OperationCanceledException or InvalidOperationException or ArgumentException or System.Security.Authentication.AuthenticationException)
        {
            if (ct.IsCancellationRequested) return;
            CloseTransport();
            IsRecovering = false;
            OnRecoveryFailed?.Invoke("Không thể khôi phục phiên. Vui lòng đăng nhập lại (thời hạn 90 giây).");
        }
    }

    private async Task ListenLoopAsync(TcpClient tcp, SslStream stream)
    {
        try
        {
            while (ReferenceEquals(_tcp, tcp))
            {
                var packet = await GameWire.ReadAsync<GamePacket>(stream);
                if (!ReferenceEquals(_tcp, tcp)) break;
                if (packet is null) break;
                if (packet.Type is not (PacketType.MATCH_FOUND or PacketType.ERROR or PacketType.AUTHENTICATED) &&
                    packet.RoomId != CurrentRoomId) continue;

                switch (packet.Type)
                {
                    case PacketType.ERROR:
                        if (string.IsNullOrEmpty(CurrentRoomId))
                        {
                            OnMatchUnavailable?.Invoke(packet.Payload);
                            break;
                        }
                        return;
                    case PacketType.MATCH_FOUND:
                        {
                            if (packet.RoomId == abandonedRoomId)
                            {
                                await SendAsync(new GamePacket(PacketType.LEAVE_ROOM, RoomId: packet.RoomId));
                                abandonedRoomId = null;
                                break;
                            }
                            CurrentRoomId = packet.RoomId;
                            MatchQueueAttemptId = packet.QueueAttemptId;
                            var parts = packet.Payload.Split('|');
                            IsResumedMatch = parts.Length > 2 && parts[2] == "RESUME";
                            bool isFirst = parts.Length > 0 && parts[0] == "FIRST";
                            MatchSide = isFirst ? 1 : 2;
                            CurrentMatchState = packet.State;
                            IsRecovering = false;
                            int oppElo = parts.Length > 1 && int.TryParse(parts[1], out int e) ? e : 1200;
                            OnMatchFound?.Invoke(packet.RoomId, packet.Sender, oppElo, isFirst, packet.GameMode);
                            if (packet.State is { } restored) OnMatchState?.Invoke(restored, "");
                            break;
                        }
                    case PacketType.RECOVERING when packet.RoomId == CurrentRoomId:
                        IsRecovering = true;
                        OnRecovering?.Invoke();
                        break;
                    case PacketType.STATE when packet.RoomId == CurrentRoomId && packet.State is { } state:
                    {
                        var previous = CurrentMatchState;
                        if (previous is not null && (state.Generation < previous.Generation || state.Revision < previous.Revision)) break;
                        CurrentMatchState = state;
                        IsRecovering = false;
                        if (state.Rating is { } rating && AuthConnection.Player is { } player)
                            AuthConnection.Player = rating.Apply(player);
                        OnMatchState?.Invoke(state, packet.Payload);
                        if (state.DrawOfferedBy != 0 && state.DrawOfferedBy != MatchSide && state.DrawOfferedBy != previous?.DrawOfferedBy)
                            OnDrawRequestReceived?.Invoke("");
                        break;
                    }
                    case PacketType.MOVE:
                        OnMoveReceived?.Invoke(packet.Payload);
                        break;
                    case PacketType.CHAT:
                        OnChatReceived?.Invoke(packet.Sender, packet.Payload);
                        break;
                    case PacketType.SURRENDER:
                        OnSurrenderReceived?.Invoke(packet.Sender);
                        break;
                    case PacketType.DRAW_REQUEST:
                        OnDrawRequestReceived?.Invoke(packet.Sender);
                        break;
                    case PacketType.DRAW_RESPONSE:
                        OnDrawResponseReceived?.Invoke(packet.Payload == "ACCEPT");
                        break;
                    case PacketType.OPPONENT_LEFT:
                        if (packet.RoomId != CurrentRoomId) break;
                        CurrentRoomId = string.Empty;
                        CurrentMatchState = null;
                        IsRecovering = false;
                        OnOpponentLeft?.Invoke(packet.Payload);
                        break;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or ObjectDisposedException or System.Text.Json.JsonException) { }
        finally
        {
            if (ReferenceEquals(_tcp, tcp))
            {
                CloseTransport();
                IsRecovering = !string.IsNullOrEmpty(CurrentRoomId);
                OnDisconnected?.Invoke();
                StartReconnect();
            }
        }
    }

    public void Disconnect()
    {
        _lifetime.Cancel();
        _lifetime = new CancellationTokenSource();
        _reconnectTask = null;
        CloseTransport();
        CurrentRoomId = string.Empty;
        CurrentMatchState = null;
        IsResumedMatch = false;
        IsRecovering = false;
    }

    public async Task LeaveRoomAsync()
    {
        string room = CurrentRoomId;
        if (!string.IsNullOrEmpty(room)) abandonedRoomId = room;
        _lifetime.Cancel();
        _lifetime = new CancellationTokenSource();
        _reconnectTask = null;
        ConnectionVersion++;
        CurrentRoomId = string.Empty;
        CurrentMatchState = null;
        IsResumedMatch = false;
        IsRecovering = false;
        try
        {
            if (!string.IsNullOrEmpty(room) && IsConnected)
                await SendAsync(new GamePacket(PacketType.LEAVE_ROOM, RoomId: room)).WaitAsync(TimeSpan.FromSeconds(3));
        }
        catch (Exception ex) when (ex is IOException or TimeoutException) { CloseTransport(); }
    }

    private void CloseTransport()
    {
        ConnectionVersion++;
        var tcp = _tcp;
        var stream = _stream;
        _stream = null;
        _tcp = null;
        stream?.Dispose();
        tcp?.Dispose();
    }

    public async Task SendAsync(GamePacket packet)
    {
        var stream = _stream;
        if (stream is null || _tcp is not { Connected: true })
            throw new IOException("Chưa kết nối game server.");

        var outbound = packet with
        {
            Sender = CurrentUsername,
            RoomId = string.IsNullOrEmpty(packet.RoomId) ? CurrentRoomId : packet.RoomId
        };

        await _sendLock.WaitAsync();
        try
        {
            if (!ReferenceEquals(stream, _stream)) throw new IOException("Kết nối game đã thay đổi.");
            await GameWire.WriteAsync(stream, outbound);
        }
        catch (ObjectDisposedException exception)
        {
            throw new IOException("Kết nối game server đã đóng.", exception);
        }
        finally
        {
            _sendLock.Release();
        }
    }
}
