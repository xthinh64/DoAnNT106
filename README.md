# Cờ vua & Cờ caro trực tuyến — NT106.R13

Đồ án cuối kỳ môn **Lập trình mạng căn bản**, nhóm 7. Ứng dụng gồm client C# WinForms, server xác thực TCP/TLS, giao diện quản trị server và cơ sở dữ liệu MySQL.

**Hiện trạng:** đăng ký/đăng nhập qua server đã được tích hợp; có giao diện và chế độ chơi cục bộ cho hai trò chơi. Luồng thi đấu trực tuyến giữa hai máy, chat qua mạng và session sau đăng nhập **chưa hoàn chỉnh**.

## Thành viên

| Họ và tên | MSSV |
|---|---|
| Lê Vũ Khánh Minh | 25521016 |
| Ngô Trọng Nhân | 25521283 |
| Tống Mai Xuân Thịnh | 25521782 |
| Trần Ngọc Tài | 25521631 |

## Chức năng hiện có

| Phần | Đã triển khai | Giới hạn hiện tại |
|---|---|---|
| Tài khoản | Đăng ký, đăng nhập qua TCP/TLS; lưu tài khoản và đọc hồ sơ/Elo từ MySQL | Chưa có token/session, thu hồi phiên hoặc quy trình quên mật khẩu |
| Gateway và worker | Round-robin theo kết nối, gateway chuyển tiếp TLS tới hai worker loopback; fallback nếu chưa kết nối TCP được tới worker | Ba listener cùng một tiến trình; chưa có health check định kỳ hoặc gateway dự phòng |
| Quản trị server | WinForms: cấu hình IP/cổng, PFX và MySQL; tạo PFX; kiểm tra schema auth; khởi động/dừng; sao chép certificate pin; bộ đếm và nhật ký | Quản lý instance do chính cửa sổ khởi chạy; chưa có điều khiển server console từ xa hoặc CRUD tài khoản/phòng/trận |
| Client | Đăng nhập, đăng ký, sảnh, bàn cờ vua/caro; giao diện sáng/tối | Một số luồng gameplay cũ còn truy cập MySQL trực tiếp |
| Cờ vua cục bộ | Bàn 8×8, nước đi cơ bản, bắt quân, phong hậu, bot đơn giản và đồng hồ | Chưa hoàn thiện an toàn vua, chiếu/chiếu bí, nhập thành, en passant và các luật hòa |
| Caro cục bộ | Bàn 15×15, kiểm tra từ 5 quân liên tiếp theo bốn hướng, bot đơn giản và đồng hồ | Chưa có xác nhận luật/kết quả trên server |
| Hàng chờ và trận | Có UI và logic hàng chờ MySQL cũ | Chưa bảo đảm hai client cùng tham gia và đồng bộ một trận qua server |
| Chat, lịch sử và Elo | Có UI/event chat, schema và một phần lưu kết quả cờ vua | Chưa có transport chat liên máy; lịch sử sảnh đang dùng dữ liệu mẫu; kết quả/Elo chưa thống nhất mọi nhánh |

## Kiến trúc đang triển khai

```text
WinForms client B/C
        │ TCP/TLS, JSON có tiền tố độ dài
        ▼
Gateway :8888 ── round-robin ──┬── Worker TLS 127.0.0.1:9001
                              └── Worker TLS 127.0.0.1:9002
                                           │
                              AuthAdmissionService dùng chung
                                           │
                                    MySqlAuthService
                                           │
                                         MySQL
```

Gateway và hai worker chạy trong **cùng tiến trình**. Có thể chọn một trong hai cách khởi chạy: `Server` (console) hoặc `ServerManager` (WinForms). Hai chương trình dùng chung thư viện `Network` và `Auth`; không chạy đồng thời trên cùng bộ cổng.

- TLS 1.2/1.3; client xác thực bằng SHA-256 của **toàn bộ chứng chỉ DER**, không phải SHA-1 thumbprint hoặc SPKI.
- Giao thức JSON UTF-8, header độ dài 4 byte big-endian, payload tối đa 16 KiB. Mỗi kết nối phục vụ một yêu cầu/phản hồi.
- Hai action nghiệp vụ: `signin` và `signup`. Phản hồi thành công chứa `UserId`, `Username`, `DisplayName`, `ChessElo`, `CaroElo`.
- PBKDF2-HMAC-SHA256: 600.000 vòng, salt ngẫu nhiên 16 byte, khóa dẫn xuất 32 byte; đối chiếu thời gian cố định.
- Đăng ký ghi tài khoản và thống kê CHESS/CARO trong một transaction; Elo ban đầu 1200.
- Mỗi listener giới hạn 128 kết nối đang xử lý; deadline 10 giây. Admission chung: tối đa 4 nghiệp vụ đồng thời, burst 20, nạp 5 yêu cầu/giây.

## Cấu trúc repository

```text
OnlineChessGame.slnx        Solution chung: client, hai server, thư viện và test
Design/                    Client WinForms .NET 8 Windows
Server/                    Chương trình server console
ServerManager/             Giao diện vận hành/quản trị server WinForms
Network/                   TCP/TLS, AuthClient, AuthCluster, hợp đồng JSON
Auth/                      MySQL, băm mật khẩu, admission
onlinedb/game_auth.sql      Schema onlinechessdb
Tests/AuthTests/           Test nghiệp vụ xác thực, hash, admission và MySQL
Tests/TransportTests/      Test transport, TLS, phân tải, shutdown và bộ đếm kết nối
Tests/ServerManagerTests/  Test bộ đếm/nhật ký của giao diện server
Tests/test_*.py            Kiểm tra cấu trúc nối UI và entrypoint
BaoCao/                    Báo cáo Word đã tạo
AUTH-SERVER.md             Tài liệu kỹ thuật nền: auth, LAN và các phần cần hoàn thiện
```

`Design/OnlineChessGame.slnx` vẫn là solution riêng cho client. Solution `.slnx` cần Visual Studio hỗ trợ định dạng này (Visual Studio 2022 17.12+); có thể mở từng `.csproj` hoặc dùng CLI. README gốc này mô tả cả phần ServerManager mới; một số tài liệu thành phần có thể mô tả phạm vi trước khi bổ sung UI.

## Yêu cầu môi trường

- Windows 10/11 cho client và ServerManager, .NET 8 SDK (hoặc SDK mới hơn hỗ trợ target .NET 8).
- Visual Studio có workload **.NET desktop development** nếu dùng IDE.
- MySQL/MariaDB tương thích và schema từ `onlinedb/game_auth.sql`.
- PFX có khóa riêng và mật khẩu; pin chứng chỉ được cấp tin cậy cho client.

## Chạy bằng giao diện quản trị server

Từ thư mục repository:

```powershell
dotnet run --project ServerManager/OnlineChessGame.ServerManager.csproj
```

Lệnh trên dùng trong **PowerShell/CMD Windows**. Nếu đang ở terminal **WSL**, gọi SDK Windows bằng `dotnet.exe` vì ServerManager là WinForms:

```bash
dotnet.exe run --project ServerManager/OnlineChessGame.ServerManager.csproj
# Nếu dotnet.exe chưa có trong PATH:
"/mnt/c/Program Files/dotnet/dotnet.exe" run --project ServerManager/OnlineChessGame.ServerManager.csproj
```

Lỗi `[/usr/lib/dotnet/host/fxr] does not exist` cho biết lệnh `dotnet` trong WSL đang gọi bản .NET Linux thiếu host/runtime. Với client và ServerManager, sử dụng SDK Windows như trên; cài lại .NET Linux cũng không giúp chạy WinForms trực tiếp trên Linux.

1. Mở tab **Cấu hình**. Nhập IP gateway, ba cổng khác nhau, chuỗi kết nối MySQL và mật khẩu PFX. Mặc định gateway là `127.0.0.1:8888`, worker là `127.0.0.1:9001` và `127.0.0.1:9002`.
2. Nếu chưa có chứng chỉ, nhập mật khẩu PFX rồi chọn **Tạo PFX mới…** và chọn tên tệp chưa tồn tại. Công cụ tạo chứng chỉ tự ký RSA 3072 bit, hạn một năm, không ghi đè chứng chỉ cũ. Nếu đã có PFX, dùng **Chọn PFX…**.
3. Nhập chuỗi kết nối, ví dụ `Server=127.0.0.1;Database=onlinechessdb;User ID=chess_app;Password=<mật khẩu>;`. Chọn **Kiểm tra MySQL**. Kiểm tra chỉ mở kết nối và đọc cấu trúc bảng auth bằng truy vấn không trả dòng; không sửa dữ liệu.
4. Chọn **Khởi động**. Chương trình kiểm tra MySQL/schema auth và chứng chỉ trước khi mở cả ba listener. Nếu một cổng bị chiếm, những listener đã mở trong lần khởi động đó được đóng lại.
5. Sao chép **SHA-256 certificate pin** trong tab Cấu hình để thiết lập `CHESS_CERT_PIN` ở client. Đường dẫn PFX và khóa riêng chỉ dùng trên server.
6. Tab **Vận hành** hiển thị endpoint, số kết nối đang xử lý của từng listener, số yêu cầu auth từng worker, kết quả nghiệp vụ, thời gian chạy và nhật ký.
7. Chọn **Dừng server** trước khi sửa cấu hình. Có thể khởi động lại trong cùng cửa sổ. Đóng cửa sổ sẽ hủy thao tác đang chờ, dừng cluster và chờ xử lý kết nối kết thúc.

Ghi chú về thông tin trên UI:

- Kết nối đang xử lý **không phải số người chơi online**. Một yêu cầu đi qua gateway thường xuất hiện ở cả gateway và một worker; không cộng hai số thành số người chơi.
- Số yêu cầu worker tính sau TLS và kiểm tra các trường bắt buộc, trước khi gọi nghiệp vụ; có thể khác tổng thành công/thất bại khi có hủy hoặc lỗi mạng.
- Kết quả nghiệp vụ không bảo đảm client đã nhận phản hồi. TLS sai hoặc JSON lỗi trước nghiệp vụ không được ghi thành một lần đăng nhập thất bại.
- Bộ đếm đặt lại khi khởi động mới. Kiểm tra MySQL thể hiện kết quả lần kiểm tra gần nhất, không phải health check liên tục.
- Nhật ký không chứa mật khẩu, tên tài khoản, profile, JSON request hoặc thông báo lỗi gốc của nhà cung cấp DB. Giữ tối đa 500 sự kiện chờ và 1.000 dòng hiển thị; chưa lưu log ra đĩa.
- Cấu hình chỉ giữ trong bộ nhớ và có thể nạp từ biến môi trường bên dưới; không tự lưu mật khẩu vào tệp cấu hình.

## Cấu hình bằng biến môi trường

| Biến | Nơi dùng | Ý nghĩa |
|---|---|---|
| `CHESS_DB_CONNECTION` | Console / UI server | Chuỗi kết nối MySQL |
| `CHESS_CERT_PATH` | Console / UI server | Đường dẫn PFX |
| `CHESS_CERT_PASSWORD` | Console / UI server | Mật khẩu PFX, không rỗng |
| `CHESS_BIND_ADDRESS` | Console / UI server | IP gateway; mặc định `127.0.0.1` |
| `AUTH_ALLOW_LEGACY_MIGRATION` | Console / UI server | Chỉ `true` mới bật chuyển mật khẩu cũ; mặc định tắt |
| `CHESS_AUTH_ENDPOINT` | Client | `host:port`; mặc định `127.0.0.1:8888` |
| `CHESS_CERT_PIN` | Client | 64 ký tự hex SHA-256 DER chứng chỉ server |
| `AUTH_TEST_MYSQL` | Test | Instance MySQL dùng một lần cho test tích hợp |

Cổng của UI được chọn trực tiếp trên form. Server console vẫn dùng cố định 8888/9001/9002. Không có biến môi trường đổi cổng console.

### Chạy server console

Thiết lập các biến cần thiết trong cùng PowerShell. Dùng chứng chỉ có sẵn hoặc tạo một lần:

```powershell
# Sau khi đã đặt CHESS_CERT_PATH và CHESS_CERT_PASSWORD:
dotnet run --project Server/OnlineChessGame.Server.csproj -- --create-cert

# Sau khi đặt thêm CHESS_DB_CONNECTION và địa chỉ bind phù hợp:
dotnet run --project Server/OnlineChessGame.Server.csproj
```

Dừng bằng `Ctrl+C`. Không chạy thêm ServerManager trên cùng bộ cổng khi console đang hoạt động.

### Chạy client và thử LAN

Trên máy A, chạy server và MySQL. Để nhận kết nối LAN, đặt gateway theo IP LAN máy A hoặc `0.0.0.0`, đồng thời cho phép cổng gateway qua firewall; hai worker giữ loopback.

Trên máy B/C, đặt IP thực của máy A và pin tin cậy:

```powershell
$env:CHESS_AUTH_ENDPOINT = "192.168.1.10:8888"  # Thay bằng IP máy A
$env:CHESS_CERT_PIN = "<64 ký tự hex từ chứng chỉ server>"
dotnet run --project Design/OnlineChessGame.csproj
```

Nếu UI server dùng cổng khác, thay endpoint tương ứng. Không dùng `0.0.0.0` làm địa chỉ đích của client. Thử đăng ký mới, đăng nhập đúng/sai và đối chiếu hồ sơ/Elo; đây là kịch bản xác thực LAN, chưa phải kịch bản thi đấu hai máy.

Luồng auth không cần client biết tài khoản DB. Tuy nhiên, logic hàng chờ và lưu kết quả cờ vua cũ trong client vẫn còn truy cập MySQL trực tiếp với cấu hình cố định; thay cấu hình server không tự sửa các luồng đó. Cần chuyển chúng sang API server trước khi nghiệm thu gameplay online.

## Cơ sở dữ liệu

Schema có `Users`, `UserGameStats`, `Rooms`, `RoomMembers`, `Matches`, `MatchPlayers`, `RoomMessages` và `MatchmakingQueue`. Auth đang sử dụng hai bảng đầu. Sự tồn tại của bảng phòng/trận không chứng minh server đã quản lý gameplay hoàn chỉnh.

Mật khẩu tài khoản mới được băm. Với mật khẩu cũ dạng rõ, mặc định trả `reset_required` nếu xác minh đúng; chưa có UI reset mật khẩu. Chỉ bật migration trong giai đoạn chuyển đổi chủ động rồi tắt lại.

## Biên dịch và kiểm thử

```powershell
dotnet build OnlineChessGame.slnx --nologo
dotnet test Tests/TransportTests/TransportTests.csproj --nologo
dotnet test Tests/AuthTests/AuthTests.csproj --nologo
dotnet test Tests/ServerManagerTests/ServerManagerTests.csproj --nologo
python -m unittest discover -s Tests -p "test_*.py"
```

- Transport test dùng kết nối loopback/TLS thật; có kiểm tra pin sai, phân tải, fallback, frame lỗi, stop và bộ đếm kết nối.
- ServerManagerTests kiểm tra tính chính xác bộ đếm, truyền hủy, giới hạn hàng đợi và tránh đưa dữ liệu xác thực hoặc thông báo tùy ý vào log. Nhóm này không tự động thao tác WinForms.
- Test tích hợp DB chỉ chạy khi có `AUTH_TEST_MYSQL`. Fixture tạo/xóa database tạm và cần quyền tương ứng: dùng **instance thử nghiệm riêng**, không trỏ tới DB đang sử dụng. Không có biến thì test DB bỏ qua; kiểm tra số lượng skipped.
- Test Python kiểm tra cấu trúc mã nối UI/entrypoint, không thay thế kiểm thử thao tác GUI hoặc LAN nhiều máy.
- Các số liệu test/build cũ trong `AUTH-SERVER.md` là kết quả ghi nhận ở thời điểm tài liệu đó được viết, không phải kết quả tự động cập nhật của checkout hiện tại.

Kiểm tra khi bổ sung ServerManager (19/11/2026): build solution đạt, 0 lỗi và 7 cảnh báo hiện có ở client; ServerManager không có cảnh báo. Transport: 27 đạt; auth: 24 đạt, 5 test DB bỏ qua; giám sát: 4 đạt; Python: 6 đạt. Kiểm tra trực tiếp WinForms trên Windows đã xác nhận mở hai tab, báo IP sai/cổng trùng, khôi phục trạng thái nút sau lỗi và đóng cửa sổ sạch. Chưa nghiệm thu kết nối MySQL thật từ UI hoặc LAN ba máy trong đợt kiểm tra này.

## Những phần cần hoàn thiện

1. Session/token, thời hạn và thu hồi phiên; lấy danh tính từ phiên cho mọi thao tác sau đăng nhập.
2. Dịch vụ phòng/ghép trận trên server; hai client cùng match ID, phân màu và trạng thái ready.
3. Bộ luật đầy đủ và trạng thái bàn cờ/đồng hồ do server xác nhận.
4. Chốt trận và cập nhật Elo/lịch sử một lần, thống nhất cả hai trò chơi.
5. Chat, heartbeat, reconnect và đồng bộ snapshot khi mất mạng.
6. Thay truy vấn MySQL trong client bằng API server; hoàn thiện nguồn dữ liệu thật cho lịch sử/xếp hạng.
7. Mở rộng UI quản trị với người chơi, phòng và trận khi các dịch vụ tương ứng đã có; tách gateway/worker thành tiến trình riêng nếu cần khả năng triển khai độc lập.

Tài liệu chi tiết về auth và các giới hạn nền: [AUTH-SERVER.md](AUTH-SERVER.md). Repository tham khảo: [CS-Winform-Project-nd-Lab](https://github.com/thnhngaan/CS-Winform-Project-nd-Lab); dự án hiện tại giữ client WinForms và MySQL, không chuyển sang Unity/SQL Server.
