using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography;
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

    public event Action<string, string, int, bool, string>? OnMatchFound; // roomId, opponent, elo, first, mode
    public event Action<string>? OnMoveReceived;
    public event Action<string, string>? OnChatReceived;
    public event Action<string>? OnSurrenderReceived;
    public event Action<string>? OnDrawRequestReceived;
    public event Action<bool>? OnDrawResponseReceived;
    public event Action<string>? OnOpponentLeft;
    public event Action? OnDisconnected;

    public async Task ConnectAsync(string host, int port, string username)
    {
        CurrentUsername = string.IsNullOrWhiteSpace(username) ? "Player" : username;
        if (_tcp is { Connected: true } && _stream is not null) return;

        Disconnect();
        _tcp = new TcpClient();
        try
        {
            await _tcp.ConnectAsync(host, port).WaitAsync(TimeSpan.FromSeconds(5));
            byte[] pin = Convert.FromHexString(AuthConnection.CertificatePin);
            if (pin.Length != 32) throw new InvalidOperationException("Certificate pin không hợp lệ.");
            _stream = new SslStream(_tcp.GetStream(), false, (_, cert, _, _) => cert is not null &&
                CryptographicOperations.FixedTimeEquals(SHA256.HashData(cert.GetRawCertData()), pin));
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await _stream.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
            {
                TargetHost = host, EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
            }, deadline.Token);
            string token = AuthConnection.GameToken ?? throw new InvalidOperationException("Vui lòng đăng nhập lại để mở phiên game.");
            await GameWire.WriteAsync(_stream, new GamePacket(PacketType.AUTHENTICATE, Payload: token), deadline.Token);
            var reply = await GameWire.ReadAsync<GamePacket>(_stream, deadline.Token);
            AuthConnection.GameToken = null;
            if (reply?.Type != PacketType.AUTHENTICATED)
                throw new InvalidOperationException("Phiên game hết hạn. Vui lòng đăng nhập lại.");
        }
        catch
        {
            Disconnect();
            throw;
        }

        var tcp = _tcp;
        var stream = _stream;
        _ = Task.Run(() => ListenLoopAsync(tcp, stream));
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
                    case PacketType.MATCH_FOUND:
                        {
                            CurrentRoomId = packet.RoomId;
                            var parts = packet.Payload.Split('|');
                            bool isFirst = parts.Length > 0 && parts[0] == "FIRST";
                            int oppElo = parts.Length > 1 && int.TryParse(parts[1], out int e) ? e : 1200;
                            OnMatchFound?.Invoke(packet.RoomId, packet.Sender, oppElo, isFirst, packet.GameMode);
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
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or System.Text.Json.JsonException) { }
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
