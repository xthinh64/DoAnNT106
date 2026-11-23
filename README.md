# Cờ vua & Cờ caro trực tuyến — NT106.R13

Đồ án cuối kỳ môn Lập trình mạng căn bản của nhóm 7. Client WinForms chạy trên Windows. Gateway, hai game worker và MySQL phục vụ xác thực, ghép trận và chơi online.

## Chạy dự án

- [Thiết lập MySQL, chứng chỉ thử nghiệm và chạy hai client qua LAN](docs/HUONG_DAN_CHAY_LAN.md).
- [Đóng gói client Windows và triển khai server console trên VPS Linux](docs/PHAT_HANH_VPS.md).

Nếu đã có database `onlinechessdb` từ bản cũ, chạy [migration OnlineMatches](onlinedb/online_matches.sql) và cấp quyền `SELECT, INSERT, UPDATE` trên bảng mới cho tài khoản server. Với database mới, chạy [game_auth.sql](onlinedb/game_auth.sql) trước. Sau đó build lại cả client và server. Bản này dùng **giao thức Gateway v3**.

## Chức năng và giới hạn hiện tại

| Thành phần | Hành vi |
| --- | --- |
| Xác thực | Đăng ký và đăng nhập qua TLS 1.2/1.3. MySQL lưu mật khẩu dưới dạng băm PBKDF2. Client dùng kho chứng chỉ hệ điều hành hoặc chứng chỉ đã đóng gói cho LAN. Sau đăng nhập, Gateway cấp token phiên trong RAM và phát vé game dùng một lần có hạn 30 giây. |
| Cờ vua Solo | Chạy Stockfish khi có file thực thi. Khi thiếu Stockfish, engine C# tích hợp cung cấp ba mức khó. |
| Caro Solo | Engine C# chạy cục bộ trên bàn 15×15, thắng khi nối từ năm quân. Bàn hỗ trợ chuột và bàn phím. |
| Chơi online | Ghép hai tài khoản khác nhau theo loại cờ. Worker xác nhận lượt, luật, đồng hồ, đầu hàng và cầu hòa. Hai client nhận cùng snapshot sau khi Gateway ghi thành công vào MySQL. |
| Chuyển worker giữa trận | Khi worker mất kết nối, Gateway cấp thế hệ mới cho phòng, nạp lại snapshot đã ghi và gửi lại cho hai client đang kết nối. Cần worker khỏe còn sức chứa. Đồng hồ tiếp tục tính trong lúc chuyển. |
| Kết quả | Kết quả online được chốt trong `OnlineMatches` bằng cập nhật có điều kiện theo thế hệ và revision. Code hiện đọc Elo từ hồ sơ. Cập nhật Elo sau trận, hiển thị lịch sử trong sảnh và lưu chat thuộc phần mở rộng. |

Gateway dùng **TCP/TLS `8888`** cho cả xác thực và gameplay. Hai worker chạy trong hai tiến trình khác nhau, lắng nghe loopback `9001` và `9002`. Gateway kiểm tra heartbeat, giữ chỗ trước khi gán trận và hỗ trợ chuyển worker sang chế độ bảo trì trong ServerManager. Framing mạng dùng độ dài big-endian 4 byte theo sau là JSON UTF-8, với giới hạn 16 KiB mỗi payload. Giao thức triển khai dùng TCP, kể cả chat trong trận.

Snapshot truyền lịch sử nước đi dạng gọn. Cờ vua online tự hòa nếu tới giới hạn kỹ thuật 1.024 lượt đi đơn mà luật cờ chưa kết thúc ván.

Có thể chạy Gateway bằng `Server` console hoặc `ServerManager` WinForms. Dùng một trong hai trên mỗi bộ cổng. MySQL giữ lịch sử nước đi, lượt, hai số dư đồng hồ, thời điểm bắt đầu lượt, revision và thế hệ phòng. Gateway giữ kết nối client và phân công worker trong RAM. Khôi phục hiện áp dụng khi **một worker sập trong lúc Gateway và MySQL còn hoạt động**. Nếu Gateway khởi động lại hoặc client rớt mạng, luồng hiện tại chưa có tính năng vào lại trận. Khi mọi worker đều đầy hoặc đang bảo trì, trận chờ phục hồi cho tới lúc có sức chứa.

## Biên dịch và kiểm thử

Chạy ở thư mục repo bằng Windows PowerShell:

```powershell
dotnet build .\OnlineChessGame.slnx -c Release
dotnet test .\OnlineChessGame.slnx -c Release
dotnet run --project .\Tests\UiSmoke\UiSmoke.csproj
python -m unittest discover -s Tests -p "test_*.py"
```

Bộ test transport gồm TLS thật qua loopback, phiên game, phân tải và thử dừng tiến trình worker giữa trận cho cả hai trò chơi. Test phục hồi dùng kho giả lập để kiểm tra xác nhận sau khi ghi, từ chối revision cũ và xử lý trường hợp DB đã ghi nhưng phản hồi lưu bị mất. Test MySQL cần server loopback dành riêng cho thử nghiệm. Đặt `AUTH_TEST_MYSQL` cho bài test xác thực hoặc `MATCH_TEST_MYSQL` cho bài test migration và kho trận. Xem [hướng dẫn LAN](docs/HUONG_DAN_CHAY_LAN.md) để nghiệm thu WinForms trên máy A/B/C.

## Cấu trúc repository

| Thư mục | Vai trò |
| --- | --- |
| `Design` | Client WinForms, màn hình đăng ký, đăng nhập, sảnh và bàn cờ |
| `Chess`, `Caro` | Luật cờ dùng chung và engine Solo |
| `Network` | Giao thức TCP/TLS, framing, xác thực endpoint và phiên game |
| `Auth` | Xác thực, băm mật khẩu, giới hạn tải và truy cập MySQL |
| `Server` | Gateway, hàng chờ, vòng đời worker, lưu/khôi phục trận |
| `ServerManager` | Cấu hình và theo dõi Gateway, xuất chứng chỉ LAN |
| `onlinedb`, `Tests`, `docs` | Schema và migration, kiểm thử, hướng dẫn |

## Thành viên

| Họ và tên | MSSV |
| --- | --- |
| Lê Vũ Khánh Minh | 25521016 |
| Ngô Trọng Nhân | 25521283 |
| Tống Mai Xuân Thịnh | 25521782 |
| Trần Ngọc Tài | 25521631 |
