using OnlineChessGame.Network;

namespace OnlineChessGame.Auth;

/// <summary>Global, zero-queue admission before any inner authentication work.</summary>
public sealed class AuthAdmissionService : IAuthService, IPlayerProfileReader
{
    private readonly IAuthService _inner;
    private readonly object _gate = new();
    private readonly int _maxConcurrency;
    private int _active;
    private readonly int _burstCapacity;
    private readonly double _refillPerSecond;
    private readonly TimeProvider _timeProvider;
    private double _tokens;
    private long _lastRefill;

    public AuthAdmissionService(IAuthService inner, int maxConcurrency = 4,
        int burstCapacity = 20, double refillPerSecond = 5, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(inner);
        if (maxConcurrency is < 1 or > 4) throw new ArgumentOutOfRangeException(nameof(maxConcurrency));
        if (burstCapacity < 1) throw new ArgumentOutOfRangeException(nameof(burstCapacity));
        if (!double.IsFinite(refillPerSecond) || refillPerSecond <= 0) throw new ArgumentOutOfRangeException(nameof(refillPerSecond));
        _inner = inner;
        _maxConcurrency = maxConcurrency;
        _burstCapacity = burstCapacity;
        _refillPerSecond = refillPerSecond;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _tokens = burstCapacity;
        _lastRefill = _timeProvider.GetTimestamp();
    }

    public Task<PlayerProfile?> ReadProfileAsync(int userId, CancellationToken ct) =>
        _inner is IPlayerProfileReader reader ? reader.ReadProfileAsync(userId, ct)
            : throw new InvalidOperationException("Profile reader is not configured.");

    public async Task<AuthResponse> HandleAsync(AuthRequest request, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        lock (_gate)
        {
            var now = _timeProvider.GetTimestamp();
            var elapsed = _timeProvider.GetElapsedTime(_lastRefill, now).TotalSeconds;
            _tokens = Math.Min(_burstCapacity, _tokens + Math.Max(0, elapsed) * _refillPerSecond);
            _lastRefill = now;
            if (_active >= _maxConcurrency || _tokens < 1) return Busy();
            _tokens--;
            _active++;
        }
        try { return await _inner.HandleAsync(request, ct).ConfigureAwait(false); }
        finally { lock (_gate) { _active--; } }
    }

    private static AuthResponse Busy() => new(false, "service_busy", "Authentication service is temporarily busy. Please try again later.");
}
