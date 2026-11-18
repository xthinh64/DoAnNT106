using OnlineChessGame.Auth;
using OnlineChessGame.Network;
using Xunit;

public class AuthServiceTests
{
    [Fact]
    public async Task BoundsAreEnforcedBeforeDatabaseAccess()
    {
        var service = new MySqlAuthService("Server=127.0.0.1;Port=1");
        var requests = new[] {
            new AuthRequest("signup", new string('u',51), "password"),
            new AuthRequest("signup", "user", new string('p',129)),
            new AuthRequest("signup", "user", "password", new string('d',101)),
            new AuthRequest("signin", "user", new string('p',129))
        };
        foreach (var request in requests) Assert.Equal("invalid_request", (await service.HandleAsync(request, default)).Code);
    }

    [Fact]
    public async Task CancellationIsNotConvertedToAnAuthenticationFailure()
    {
        using var source = new CancellationTokenSource(); source.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new MySqlAuthService("Server=127.0.0.1;Port=1").HandleAsync(new AuthRequest("signin", "user", "password"), source.Token));
    }

    [Theory]
    [InlineData("unknown", "user", "password", null)]
    [InlineData("signup", "user", "short", null)]
    [InlineData("signup", "   ", "password", null)]
    [InlineData("signin", "user", "", null)]
    public async Task InvalidInputNeverOpensDatabase(string action, string user, string password, string? display)
    {
        var result = await new MySqlAuthService("Server=127.0.0.1;Port=1").HandleAsync(new AuthRequest(action,user,password,display), default);
        Assert.False(result.Success);
        Assert.Equal("invalid_request", result.Code);
    }
}
