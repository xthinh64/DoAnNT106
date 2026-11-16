using OnlineChessGame.Auth;
using OnlineChessGame.Network;
using Xunit;

public class AuthAdmissionTests
{
    private static readonly AuthRequest Request = new("signin", "user", "password");
    private static readonly AuthResponse Accepted = new(true, "ok", "ok");

    [Fact]
    public async Task ConcurrentOverloadReturnsBusyImmediatelyWithoutCallingInner()
    {
        var release = new TaskCompletionSource<AuthResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var inner = new Handler((_, _) => { Interlocked.Increment(ref calls); return release.Task; });
        var service = new AuthAdmissionService(inner, maxConcurrency: 2);
        var first = service.HandleAsync(Request);
        var second = service.HandleAsync(Request);
        try
        {
            var rejected = service.HandleAsync(Request);
            Assert.True(rejected.IsCompletedSuccessfully);
            Assert.Equal(new AuthResponse(false, "service_busy", "Authentication service is temporarily busy. Please try again later."), await rejected);
            Assert.Equal(2, calls);
        }
        finally { release.TrySetResult(Accepted); }
        await Task.WhenAll(first, second);
        Assert.Same(Accepted, await service.HandleAsync(Request));
    }

    [Fact]
    public async Task BurstIsBoundedAndRefillsUsingMonotonicTimeWithoutSleeping()
    {
        var clock = new ManualTimeProvider();
        var calls = 0;
        var service = new AuthAdmissionService(new Handler((_, _) => { calls++; return Task.FromResult(Accepted); }),
            burstCapacity: 2, refillPerSecond: 2, timeProvider: clock);
        Assert.Same(Accepted, await service.HandleAsync(Request));
        Assert.Same(Accepted, await service.HandleAsync(Request));
        Assert.Equal("service_busy", (await service.HandleAsync(Request)).Code);
        clock.Advance(TimeSpan.FromMilliseconds(250));
        Assert.Equal("service_busy", (await service.HandleAsync(Request)).Code);
        clock.Advance(TimeSpan.FromMilliseconds(250));
        Assert.Same(Accepted, await service.HandleAsync(Request));
        Assert.Equal("service_busy", (await service.HandleAsync(Request)).Code);
        clock.Advance(TimeSpan.FromHours(1));
        Assert.Same(Accepted, await service.HandleAsync(Request));
        Assert.Same(Accepted, await service.HandleAsync(Request));
        Assert.Equal("service_busy", (await service.HandleAsync(Request)).Code);
        Assert.Equal(5, calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExceptionOrCancellationReleasesSlotAndPreservesFailure(bool cancel)
    {
        using var source = new CancellationTokenSource();
        var completion = new TaskCompletionSource<AuthResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var service = new AuthAdmissionService(new Handler((_, ct) =>
        {
            if (++calls == 1)
            {
                Assert.Equal(source.Token, ct);
                return completion.Task;
            }
            return Task.FromResult(Accepted);
        }), maxConcurrency: 1);
        var running = service.HandleAsync(Request, source.Token);
        if (cancel)
        {
            source.Cancel();
            completion.SetCanceled(source.Token);
            var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);
            Assert.Equal(source.Token, error.CancellationToken);
        }
        else
        {
            var failure = new InvalidOperationException("inner failure");
            completion.SetException(failure);
            Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() => running));
        }
        Assert.Same(Accepted, await service.HandleAsync(Request));
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task SynchronousInnerExceptionReleasesSlot()
    {
        var calls = 0;
        var service = new AuthAdmissionService(new Handler((_, _) =>
            ++calls == 1 ? throw new InvalidOperationException() : Task.FromResult(Accepted)), maxConcurrency: 1);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.HandleAsync(Request));
        Assert.Same(Accepted, await service.HandleAsync(Request));
    }

    [Fact]
    public async Task PreCanceledRequestDoesNotConsumeRateTokenOrCallInner()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        var calls = 0;
        var service = new AuthAdmissionService(new Handler((_, _) => { calls++; return Task.FromResult(Accepted); }), burstCapacity: 1);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.HandleAsync(Request, source.Token));
        Assert.Same(Accepted, await service.HandleAsync(Request));
        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData(0, 20, 5)]
    [InlineData(5, 20, 5)]
    [InlineData(4, 0, 5)]
    [InlineData(4, 20, 0)]
    [InlineData(4, 20, -1)]
    [InlineData(4, 20, double.NaN)]
    [InlineData(4, 20, double.PositiveInfinity)]
    public void InvalidBudgetsAreRejected(int concurrency, int burst, double refill)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AuthAdmissionService(
            new Handler((_, _) => Task.FromResult(Accepted)), concurrency, burst, refill));
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private long _timestamp;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => _timestamp;
        public void Advance(TimeSpan elapsed) => _timestamp += elapsed.Ticks;
    }

    private sealed class Handler(Func<AuthRequest, CancellationToken, Task<AuthResponse>> handle) : IAuthService
    {
        public Task<AuthResponse> HandleAsync(AuthRequest request, CancellationToken ct) => handle(request, ct);
    }
}
