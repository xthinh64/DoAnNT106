using System.Net.Sockets;
using OnlineChessGame.Network;

namespace OnlineChessGame;

public sealed class GameClient
{
    public static GameClient Instance { get; } = new();

    private TcpClient? _tcp;
    private NetworkStream? _stream;
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    public string CurrentUsername { get; private set; } = string.Empty;
    public string CurrentRoomId { get; set; } = string.Empty;

    public event Action<string, string, int, bool>? OnMatchFound; // roomId, oppName, oppElo, isFirstTurn
    public event Action<string>? OnMoveReceived;
    public event Action<string, string>? OnChatReceived;
    public event Action<string>? OnSurrenderReceived;
    public event Action<string>? OnDrawRequestReceived;
    public event Action<bool>? OnDrawResponseReceived;
    public event Action<string>? OnOpponentLeft;

    public async Task ConnectAsync(string host, int port, string username)
    {
        CurrentUsername = string.IsNullOrWhiteSpace(username) ? "Player" : username;
        if (_tcp is { Connected: true }) return;

        _tcp = new TcpClient();
        await _tcp.ConnectAsync(host, port);
        _stream = _tcp.GetStream();

        _ = Task.Run(ListenLoopAsync);
    }

    private async Task ListenLoopAsync()
    {
        if (_stream is null) return;
        try
        {
            while (_tcp is { Connected: true })
            {
                var packet = await GameWire.ReadAsync<GamePacket>(_stream);
                if (packet is null) break;

                switch (packet.Type)
                {
                    case PacketType.MATCH_FOUND:
                        {
                            CurrentRoomId = packet.RoomId;
                            var parts = packet.Payload.Split('|');
                            bool isFirst = parts.Length > 0 && parts[0] == "FIRST";
                            int oppElo = parts.Length > 1 && int.TryParse(parts[1], out int e) ? e : 1200;
                            OnMatchFound?.Invoke(packet.RoomId, packet.Sender, oppElo, isFirst);
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
                        CurrentRoomId = string.Empty;
                        OnOpponentLeft?.Invoke(packet.Payload);
                        break;
                }
            }
        }
        catch { }
    }

    public async Task SendAsync(GamePacket packet)
    {
        if (_stream is null || _tcp is not { Connected: true }) return;

        var outbound = packet with
        {
            Sender = CurrentUsername,
            RoomId = string.IsNullOrEmpty(packet.RoomId) ? CurrentRoomId : packet.RoomId
        };

        await _sendLock.WaitAsync();
        try
        {
            await GameWire.WriteAsync(_stream, outbound);
        }
        finally
        {
            _sendLock.Release();
        }
    }
}