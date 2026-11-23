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

    public string CurrentUsername { get; private set; } = string.Empty;
    public string CurrentRoomId { get; set; } = string.Empty;
    public MatchState? CurrentMatchState { get; private set; }
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

    public async Task ConnectAsync(string username)
    {
        CurrentUsername = string.IsNullOrWhiteSpace(username) ? "Player" : username;
        if (_tcp is { Connected: true } && _stream is not null) return;

        Disconnect();
        var tcp = new TcpClient();
        _tcp = tcp;
        SslStream? tls = null;
        try
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var gateway = AuthConnection.CreateClient();
            string session = AuthConnection.SessionToken ?? throw new InvalidOperationException("Vui lòng đăng nhập lại để mở phiên game.");
            var ticket = await gateway.RequestTicketAsync(session, deadline.Token);
            if (ticket.Token is null) throw new InvalidOperationException("Phiên game hết hạn. Vui lòng đăng nhập lại.");
            tls = await gateway.ConnectAsync(tcp, deadline.Token);
            await GameWire.WriteAsync(tls, new GatewayRequest(GatewayRequest.CurrentVersion, "game", Token: ticket.Token), deadline.Token);
            var reply = await GameWire.ReadAsync<GamePacket>(tls, deadline.Token);
            if (reply?.Type != PacketType.AUTHENTICATED)
                throw new InvalidOperationException("Phiên game hết hạn. Vui lòng đăng nhập lại.");
            if (!ReferenceEquals(tcp, _tcp)) throw new OperationCanceledException();
            _stream = tls;
            _ = Task.Run(() => ListenLoopAsync(tcp, tls));
        }
        catch
        {
            tls?.Dispose();
            if (ReferenceEquals(tcp, _tcp)) Disconnect();
            throw;
        }
    }

    private async Task ListenLoopAsync(TcpClient tcp, SslStream stream)
    {
        try
        {
            while (ReferenceEquals(_tcp, tcp))
            {
                var packet = await GameWire.ReadAsync<GamePacket>(stream);
                if (packet is null) break;

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
                            CurrentRoomId = packet.RoomId;
                            var parts = packet.Payload.Split('|');
                            bool isFirst = parts.Length > 0 && parts[0] == "FIRST";
                            MatchSide = isFirst ? 1 : 2;
                            CurrentMatchState = packet.State;
                            IsRecovering = false;
                            int oppElo = parts.Length > 1 && int.TryParse(parts[1], out int e) ? e : 1200;
                            OnMatchFound?.Invoke(packet.RoomId, packet.Sender, oppElo, isFirst, packet.GameMode);
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
                Disconnect();
                OnDisconnected?.Invoke();
            }
        }
    }

    public void Disconnect()
    {
        var tcp = _tcp;
        var stream = _stream;
        _stream = null;
        _tcp = null;
        stream?.Dispose();
        tcp?.Dispose();
        CurrentRoomId = string.Empty;
        CurrentMatchState = null;
        IsRecovering = false;
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
