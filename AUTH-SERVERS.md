# Hai worker xác thực — WinForms / MySQL

## Phạm vi

`WinForms → TCP gateway :8888 → TLS worker :9001 / :9002 → MySQL`.
Gateway chuyển tiếp byte TLS, không giải mã mật khẩu. Hai worker là hai listener độc lập trong **cùng một tiến trình**; dùng chung dịch vụ xác thực/database và chứng chỉ. Đây không phải hai máy chủ HA hay cơ chế failover tiến trình. Gateway round-robin từng kết nối, chỉ chuyển sang worker còn lại khi TCP connect thất bại trước khi chuyển dữ liệu; không tự phát lại đăng ký/đăng nhập.

Áp dụng mô hình client gửi yêu cầu / server truy vấn DB / server phản hồi từ project tham khảo `https://github.com/thnhngaan/CS-Winform-Project-nd-Lab`. Không chạy hoặc sao chép nguyên server mẫu. Giữ MySQL hiện tại thay vì SQLite trong sơ đồ. UDP chat/probe, heartbeat liên tiến trình, matchmaker, phiên chơi và đồng bộ gameplay không thuộc phần xác thực này.

## Bảo mật

- TLS 1.2/1.3; client yêu cầu pin SHA-256 của **toàn bộ DER certificate**, cấu hình ngoài băng. Không tải pin tự động từ kết nối chưa xác thực.
- Mật khẩu không trim, không ghi log, không ghi vào checkbox ghi nhớ. Lưu bằng PBKDF2-SHA256 có salt ngẫu nhiên, không phải mã hóa có thể giải ngược.
- Gói JSON có tiền tố độ dài 4 byte big-endian, tối đa 16 KiB; deadline kết nối 10 giây, giới hạn kết nối cho mỗi listener.
- Cả hai worker dùng chung giới hạn xác thực: tối đa 4 yêu cầu đang xử lý, không xếp hàng; token bucket toàn cục burst 20, nạp 5 yêu cầu/giây. Quá tải trả `service_busy`. Đây không phải giới hạn từng IP/tài khoản và không thay thế firewall hoặc chống DDoS.
- Chỉ server auth nhận chuỗi kết nối MySQL qua môi trường. Luồng gameplay cũ ở Dashboard/ChessBoard vẫn truy vấn DB trực tiếp: **chưa phải toàn bộ ứng dụng đã chuyển sang server**.
- Pin thay thế CA/hostname trust: chỉ phân phối đúng pin từ người quản trị qua kênh đáng tin cậy. Khi đổi chứng chỉ cần cập nhật pin trên client.

## Chuẩn bị và chạy

1. Cài .NET 8 runtime/SDK và MySQL/MariaDB. Tạo schema bằng `onlinedb/game_auth.sql` trên DB do bạn quản lý. Không tự chạy script lên DB đang dùng trong kiểm thử.
2. Cấp user DB riêng có quyền SELECT/INSERT/UPDATE phù hợp cho `Users`, `UserGameStats`; không dùng root trên môi trường thật. Với DB qua mạng, cấu hình TLS và xác thực certificate phía MySQL bằng connection options tương ứng.
3. Đặt biến môi trường cho server: `CHESS_DB_CONNECTION`, `CHESS_CERT_PATH` (đường dẫn tuyệt đối tới PFX), `CHESS_CERT_PASSWORD`. Không ghi giá trị thật vào repo/chat.
4. Tạo chứng chỉ phát triển một lần: `dotnet run --project Server/OnlineChessGame.Server.csproj -- --create-cert`. Lệnh từ chối ghi đè file sẵn có. Chọn thư mục chỉ tài khoản chạy server đọc/ghi được; bảo vệ private key bằng ACL Windows. Pin công khai được in ra, không phải private key.
5. Chạy `dotnet run --project Server/OnlineChessGame.Server.csproj`. Mặc định gateway bind `127.0.0.1`; muốn LAN, đặt `CHESS_BIND_ADDRESS` là IP máy chủ và firewall chỉ cho phép cổng gateway 8888 từ client. Hai worker phải chỉ bind loopback, không công bố 9001/9002 ra LAN.
6. Trên client, đặt `CHESS_CERT_PIN` bằng pin lấy từ quản trị viên; tùy chọn `CHESS_AUTH_ENDPOINT=host:8888`. Khởi chạy WinForms từ tiến trình kế thừa các biến này: `dotnet run --project Design/OnlineChessGame.csproj`. Có thể thay endpoint bằng ô Server IP trước khi vào đăng ký. Không cần và không cung cấp chuỗi DB cho luồng auth của client.
7. Ctrl+C dừng server và đóng listener/kết nối. Không tự khởi động server nền khi build.

Certificate sinh bởi lệnh trên là self-signed dùng pin, RSA-3072, hạn một năm. Không tự thêm vào trust store Windows. Bảo vệ PFX và mật khẩu bằng cơ chế secrets của hệ điều hành khi triển khai lâu dài.

## Tài khoản cũ lưu mật khẩu rõ

Mặc định server từ chối mật khẩu legacy và trả `reset_required`; UI chưa có chức năng gửi email reset. Không âm thầm cho phép dữ liệu mật khẩu rõ tiếp tục tồn tại.

Nếu quản trị viên chấp thuận chuyển đổi tại chỗ: sao lưu DB bằng công cụ quản trị, giới hạn truy cập gateway cho nhóm người dùng cần chuyển đổi, đặt `AUTH_ALLOW_LEGACY_MIGRATION=true` **chỉ trên server**, khởi động lại rồi yêu cầu từng người đăng nhập bằng mật khẩu cũ qua TLS. Chỉ lần đăng nhập đúng mới thay cột password_hash bằng PBKDF2; sai mật khẩu không thay đổi dữ liệu. Đối chiếu định dạng hash trong DB bằng công cụ quản trị, không xuất giá trị ra log. Sau đợt chuyển đổi, gỡ biến này và khởi động lại. Đường migration đã có test DB riêng; không tự động thực hiện trên DB hiện hữu. Tài khoản không đăng nhập được cần quản trị viên triển khai quy trình reset riêng; không yêu cầu người dùng gửi mật khẩu qua chat.

## Luồng UI

- Đăng ký: kiểm tra username/display name và mật khẩu 8–128 ký tự → TLS → server kiểm tra lại → transaction tạo Users và hai UserGameStats → quay lại login.
- Đăng nhập: TLS → server xác minh password hash → trả profile và Elo thực → vào Dashboard. Không trả password/hash trong profile.
- Nút submit/chuyển màn hình bị khóa khi đang chờ; dispose Form hủy yêu cầu. Lỗi mạng hiển thị thông báo chung, không lộ exception DB.
- Profile xác thực hiện được giữ trong bộ nhớ UI, **không phải bearer session/token cho gameplay**. Gameplay cũ cần bước di chuyển riêng, không được tin thông tin username từ client như bằng chứng quyền hạn.

## Kiểm tra

- `dotnet test Tests/TransportTests/TransportTests.csproj --nologo`
- `dotnet test Tests/AuthTests/AuthTests.csproj --nologo`
- `python -m unittest discover -s Tests -p 'test_*.py'` (chỉ kiểm tra nối mã nguồn UI/entrypoint; không thay thế GUI test)
- `dotnet build Server/OnlineChessGame.Server.csproj --nologo`
- `dotnet build Design/OnlineChessGame.csproj --nologo`

Báo cáo kết quả thực thi cuối cùng sẽ nêu riêng test đơn vị, loopback và database; không suy diễn test fake service thành bằng chứng MySQL hoạt động.
