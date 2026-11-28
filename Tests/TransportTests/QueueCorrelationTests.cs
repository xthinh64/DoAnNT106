using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using OnlineChessGame.Network;
using OnlineChessGame.Server;
using Xunit;

public sealed class QueueCorrelationTests
{
    [Fact]
    public async Task QueueIdentityIsCapturedInRoomNotReadFromLaterQueueEntries()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var a = new TcpClient();
        await a.ConnectAsync((IPEndPoint)listener.LocalEndpoint);
        using var aPeer = await listener.AcceptTcpClientAsync();
        using var b = new TcpClient();
        await b.ConnectAsync((IPEndPoint)listener.LocalEndpoint);
        using var bPeer = await listener.AcceptTcpClientAsync();
        var first = new ClientSession(a, a.GetStream());
        var second = new ClientSession(b, b.GetStream());
        var rooms = new RoomManager();
        Assert.Null(rooms.EnqueueOrMatch(first, "CHESS", queueAttemptId: "cancelled"));
        rooms.CancelQueue(first);
        Assert.Null(rooms.EnqueueOrMatch(first, "CHESS", queueAttemptId: "A"));
        var room = rooms.EnqueueOrMatch(second, "CHESS", queueAttemptId: "opponent-A")!;
        await rooms.RemoveClientAsync(first, notifyOpponent: false);
        Assert.Null(rooms.EnqueueOrMatch(first, "CHESS", queueAttemptId: "B"));
        var next = rooms.EnqueueOrMatch(second, "CHESS", queueAttemptId: "opponent-B")!;
        Assert.Equal("A", room.Player1QueueAttemptId);
        Assert.Equal("opponent-A", room.Player2QueueAttemptId);
        Assert.Equal("B", next.Player1QueueAttemptId);
        Assert.Equal("opponent-B", next.Player2QueueAttemptId);
    }

    [Fact]
    public void LegacyPacketsWithoutOptionalQueueIdStillDeserialize()
    {
        var packet = JsonSerializer.Deserialize<GamePacket>("{\"Type\":2,\"GameMode\":\"CHESS\"}")!;
        Assert.Null(packet.QueueAttemptId);
        var correlated = packet with { QueueAttemptId = "attempt" };
        Assert.Equal(correlated, JsonSerializer.Deserialize<GamePacket>(JsonSerializer.Serialize(correlated)));
    }
}
