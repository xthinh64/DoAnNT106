using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Net;
using System.Net.Sockets;
using OnlineChessGame.Network;
using OnlineChessGame.Server;
using Xunit;

namespace TransportTests;

public class GameServerTests
{
    [Fact]
    public async Task WrongCertificatePinRejectsGameConnection()
    {
        using var cert = ClusterTests.Certificate();
        var sessions = new GameSessions(new ClusterTests.Service());
        await using var server = new GameServer(IPAddress.Loopback, cert, sessions, 0);
        server.Start();
        using var tcp = new TcpClient();
        await tcp.ConnectAsync(IPAddress.Loopback, server.Port);
        using var tls = new SslStream(tcp.GetStream(), false, (_, actual, _, _) => actual is not null &&
            CryptographicOperations.FixedTimeEquals(SHA256.HashData(actual.GetRawCertData()), new byte[32]));
        await Assert.ThrowsAsync<System.Security.Authentication.AuthenticationException>(() => tls.AuthenticateAsClientAsync("localhost"));
    }

    [Fact]
    public async Task TokenIsSingleUseAndIdentityComesFromLogin()
    {
        var sessions = new GameSessions(new ClusterTests.Service());
        Assert.Null(sessions.Consume(new string('0', 64)));
        var response = await sessions.HandleAsync(new AuthRequest("signin", "Alice", "secret"), default);
        Assert.Equal("Alice", sessions.Consume(response.GameToken!)?.Username);
        Assert.Null(sessions.Consume(response.GameToken!));
        var failed = await sessions.HandleAsync(new AuthRequest("invalid", "Alice", "secret"), default);
        Assert.Null(failed.GameToken);
    }

    [Fact]
    public async Task TwoPlayersMatchAndExchangeMoveChatAndLeave()
    {
        using var cert = ClusterTests.Certificate();
        var sessions = new GameSessions(new ClusterTests.Service());
        await using var server = new GameServer(IPAddress.Loopback, cert, sessions, 0);
        server.Start();
        using var first = new TcpClient();
        using var second = new TcpClient();
        await first.ConnectAsync(IPAddress.Loopback, server.Port);
        await second.ConnectAsync(IPAddress.Loopback, server.Port);
        await using var a = await Connect(first, cert, sessions, "Alice");
        await using var b = await Connect(second, cert, sessions, "Bob");

        await GameWire.WriteAsync(a, new GamePacket(PacketType.FIND_MATCH, "Alice", GameMode: "CHESS", Payload: "1200"));
        await GameWire.WriteAsync(b, new GamePacket(PacketType.FIND_MATCH, "Bob", GameMode: "CHESS", Payload: "1250"));

        var matchedA = await GameWire.ReadAsync<GamePacket>(a).WaitAsync(TimeSpan.FromSeconds(5));
        var matchedB = await GameWire.ReadAsync<GamePacket>(b).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(matchedA);
        Assert.NotNull(matchedB);
        Assert.Equal(PacketType.MATCH_FOUND, matchedA.Type);
        Assert.Equal(matchedA.RoomId, matchedB.RoomId);
        Assert.EndsWith("|1200", matchedA.Payload);
        Assert.EndsWith("|1200", matchedB.Payload);
        Assert.NotEqual(matchedA.Payload.Split('|')[0], matchedB.Payload.Split('|')[0]);

        await GameWire.WriteAsync(a, new GamePacket(PacketType.MOVE, "Alice", matchedA.RoomId, "CHESS", "e2e4"));
        var move = await GameWire.ReadAsync<GamePacket>(b).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(PacketType.MOVE, move?.Type);
        Assert.Equal("e2e4", move?.Payload);

        await GameWire.WriteAsync(b, new GamePacket(PacketType.CHAT, "Bob", matchedB.RoomId, "CHESS", "Xin chào"));
        var chat = await GameWire.ReadAsync<GamePacket>(a).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(PacketType.CHAT, chat?.Type);
        Assert.Equal("Xin chào", chat?.Payload);

        await GameWire.WriteAsync(a, new GamePacket(PacketType.LEAVE_ROOM, "Alice", matchedA.RoomId));
        var left = await GameWire.ReadAsync<GamePacket>(b).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(PacketType.OPPONENT_LEFT, left?.Type);
    }
    private static async Task<SslStream> Connect(TcpClient tcp, X509Certificate2 cert, GameSessions sessions, string username)
    {
        var tls = new SslStream(tcp.GetStream(), false, (_, actual, _, _) => actual is not null &&
            CryptographicOperations.FixedTimeEquals(SHA256.HashData(actual.GetRawCertData()), SHA256.HashData(cert.RawData)));
        await tls.AuthenticateAsClientAsync("localhost");
        var response = await sessions.HandleAsync(new AuthRequest("signin", username, "secret"), default);
        await GameWire.WriteAsync(tls, new GamePacket(PacketType.AUTHENTICATE, Payload: response.GameToken!));
        var ack = await GameWire.ReadAsync<GamePacket>(tls).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(PacketType.AUTHENTICATED, ack?.Type);
        Assert.Null(sessions.Consume(response.GameToken!));
        return tls;
    }

    [Fact]
    public async Task MissingTokenCannotEnterQueue()
    {
        using var cert = ClusterTests.Certificate();
        var sessions = new GameSessions(new ClusterTests.Service());
        await using var server = new GameServer(IPAddress.Loopback, cert, sessions, 0);
        server.Start();
        using var tcp = new TcpClient();
        await tcp.ConnectAsync(IPAddress.Loopback, server.Port);
        using var tls = new SslStream(tcp.GetStream(), false, (_, _, _, _) => true);
        await tls.AuthenticateAsClientAsync("localhost");
        await GameWire.WriteAsync(tls, new GamePacket(PacketType.FIND_MATCH, Sender: "Impersonated"));
        var reply = await GameWire.ReadAsync<GamePacket>(tls).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(PacketType.ERROR, reply?.Type);
    }
}
