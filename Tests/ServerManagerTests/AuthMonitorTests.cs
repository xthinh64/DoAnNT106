using OnlineChessGame.Network;
using OnlineChessGame.ServerManager;
using Xunit;

public sealed class AuthMonitorTests
{
    private sealed class Service(Func<AuthRequest, CancellationToken, Task<AuthResponse>> handle) : IAuthService
    {
        public Task<AuthResponse> HandleAsync(AuthRequest request, CancellationToken ct) => handle(request, ct);
    }

    [Fact]
    public async Task Monitoring_preserves_response_without_recording_credentials_or_response_text()
    {
        var response = new AuthResponse(true, "ok", "private-message", new(1, "private-name", "private-display", 1200, 1200));
        var monitor = new AuthMonitor(new Service((_, _) => Task.FromResult(response)));
        Assert.Same(response, await monitor.HandleAsync(new("signin", "private-name", "private-password"), default));
        string log = string.Join("\n", monitor.DrainEvents());
        Assert.DoesNotContain("private-", log);
        Assert.Contains("Đăng nhập", log);
        Assert.Equal(1, monitor.Successful);
        Assert.Equal(0, monitor.Failed);
        Assert.Empty(monitor.DrainEvents());
    }

    [Fact]
    public async Task Untrusted_action_code_and_exception_messages_never_enter_the_log()
    {
        var monitor = new AuthMonitor(new Service((_, _) => Task.FromResult(new AuthResponse(false, "private-code\nforged-entry", "private-message"))));
        await monitor.HandleAsync(new("private-action\nforged-entry", "private-name", "private-password"), default);
        string log = string.Join("\n", monitor.DrainEvents());
        Assert.DoesNotContain("private-", log);
        Assert.DoesNotContain("forged-entry", log);
        var failing = new AuthMonitor(new Service((_, _) => throw new InvalidOperationException("private-connection-string")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => failing.HandleAsync(new("signup", "name", "password"), default));
        Assert.DoesNotContain("private-", string.Join("\n", failing.DrainEvents()));
        Assert.Equal(1, failing.Failed);
    }

    [Fact]
    public async Task Cancellation_is_propagated_and_not_counted_as_a_business_failure()
    {
        using var stop = new CancellationTokenSource();
        stop.Cancel();
        var monitor = new AuthMonitor(new Service((_, ct) => Task.FromCanceled<AuthResponse>(ct)));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => monitor.HandleAsync(new("signin", "name", "password"), stop.Token));
        Assert.Equal(0, monitor.Failed);
        Assert.Equal(0, monitor.Successful);
        Assert.Contains("đã hủy", Assert.Single(monitor.DrainEvents()));
    }

    [Fact]
    public async Task Concurrent_auth_counts_are_exact_and_pending_events_are_bounded()
    {
        var monitor = new AuthMonitor(new Service((r, _) => Task.FromResult(new AuthResponse(r.Action == "signin", "ok", ""))));
        await Task.WhenAll(Enumerable.Range(0, 1200).Select(i => Task.Run(() => monitor.HandleAsync(new(i % 2 == 0 ? "signin" : "signup", "name", "password"), default))));
        Assert.Equal(600, monitor.Successful);
        Assert.Equal(600, monitor.Failed);
        Assert.Equal(500, monitor.DrainEvents().Length);
    }
}
