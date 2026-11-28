using OnlineChessGame.Network;
using Xunit;

public class ProfileRefreshTests
{
    sealed class Auth : IAuthService, IPlayerProfileReader
    {
        public PlayerProfile Current = new(1,"one","One",1200,1200);
        public Task<AuthResponse> HandleAsync(AuthRequest r, CancellationToken ct) => Task.FromResult(new AuthResponse(true,"ok","",Current));
        public Task<PlayerProfile?> ReadProfileAsync(int userId, CancellationToken ct) => Task.FromResult<PlayerProfile?>(Current);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProductionDecoratorsRefreshSameSessionAndReconnect(bool gui)
    {
        var source = new Auth();
        IAuthService chain = new OnlineChessGame.Auth.AuthAdmissionService(source);
        if (gui) chain = new OnlineChessGame.ServerManager.AuthMonitor(chain);
        var sessions = new GameSessions(chain);
        string login = sessions.CreateSession(source.Current);
        var connected = sessions.Consume(sessions.IssueTicket(login)!)!;
        source.Current = source.Current with { ChessElo = 1216, CaroElo = 1184 };
        Assert.Equal(source.Current, await sessions.RefreshAsync(connected, default));
        Assert.Equal(source.Current, await sessions.RefreshAsync(sessions.Consume(sessions.IssueTicket(login)!)!, default));
    }

    [Fact]
    public void SettlementRefreshesOnlyOwnPoolAndRejectsOtherUsers()
    {
        var rating = new RatingSettlement("internal-elo-k32-v1", "CHESS", 1, 2, 1200, 1200, 16);
        var player = new PlayerProfile(1,"one","One",1200,1300);
        Assert.Equal(player with { ChessElo = 1216 }, rating.Apply(player));
        Assert.Equal(player with { UserId = 3 }, rating.Apply(player with { UserId = 3 }));
    }

    [Fact]
    public async Task ExistingLoginAndTicketsRefreshFromPersistedProfile()
    {
        var auth = new Auth(); var sessions = new GameSessions(auth);
        string login = sessions.CreateSession(auth.Current);
        var oldTicket = sessions.IssueTicket(login)!;
        auth.Current = auth.Current with { ChessElo = 1216 };
        var refreshed = await sessions.RefreshAsync(sessions.Consume(oldTicket)!, default);
        Assert.Equal(1216, refreshed.ChessElo);
        Assert.Equal(1200, refreshed.CaroElo);
        var packet = new GamePacket(PacketType.AUTHENTICATED, Player: refreshed);
        var json = System.Text.Json.JsonSerializer.Serialize(packet);
        Assert.Equal(refreshed, System.Text.Json.JsonSerializer.Deserialize<GamePacket>(json)!.Player);
    }
}
