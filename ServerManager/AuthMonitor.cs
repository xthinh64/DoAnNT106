using OnlineChessGame.Network;

namespace OnlineChessGame.ServerManager;

/// <summary>Bounded, sanitized operational events; never retain requests or player profiles.</summary>
public sealed class AuthMonitor(IAuthService inner) : IAuthService, IPlayerProfileReader
{
    private readonly object gate = new();
    private readonly Queue<string> events = new();
    private long successful, failed;
    public long Successful => Interlocked.Read(ref successful);
    public long Failed => Interlocked.Read(ref failed);

    public void Log(string message)
    {
        lock (gate)
        {
            if (events.Count == 500) events.Dequeue();
            events.Enqueue($"[{DateTimeOffset.Now:HH:mm:ss}] {message}");
        }
    }

    public string[] DrainEvents()
    {
        lock (gate)
        {
            var result = events.ToArray();
            events.Clear();
            return result;
        }
    }

    public Task<PlayerProfile?> ReadProfileAsync(int userId, CancellationToken ct) =>
        inner is IPlayerProfileReader reader ? reader.ReadProfileAsync(userId, ct)
            : throw new InvalidOperationException("Profile reader is not configured.");

    public async Task<AuthResponse> HandleAsync(AuthRequest request, CancellationToken ct)
    {
        string action = request.Action switch { "signin" => "Đăng nhập", "signup" => "Đăng ký", _ => "Yêu cầu không hợp lệ" };
        try
        {
            var response = await inner.HandleAsync(request, ct).ConfigureAwait(false);
            if (response.Success) Interlocked.Increment(ref successful);
            else Interlocked.Increment(ref failed);
            string result = response.Code switch
            {
                "ok" => "thành công",
                "invalid_credentials" => "thông tin xác thực không đúng",
                "username_taken" => "tên tài khoản đã được sử dụng",
                "reset_required" => "cần đặt lại mật khẩu",
                "service_unavailable" => "dịch vụ dữ liệu không sẵn sàng",
                "service_busy" => "server đang bận",
                "invalid_request" => "dữ liệu không hợp lệ",
                _ => "đã nhận phản hồi"
            };
            Log($"{action}: {result}.");
            return response;
        }
        catch (OperationCanceledException)
        {
            Log($"{action}: đã hủy hoặc hết thời gian.");
            throw;
        }
        catch
        {
            Interlocked.Increment(ref failed);
            Log($"{action}: lỗi xử lý nội bộ.");
            throw;
        }
    }
}
