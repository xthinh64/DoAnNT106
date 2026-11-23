# Chạy Chess & Caro qua LAN với Gateway v3

Máy A chạy MySQL và ServerManager. Máy B/C chạy client Windows cùng phiên bản với server. Client dùng **một cổng TCP/TLS `8888`** để đăng ký, đăng nhập, tìm trận, gửi nước đi và chat.

| Thành phần | Địa chỉ ví dụ | Máy B/C có kết nối trực tiếp? |
| --- | --- | --- |
| Gateway trên máy A | `192.168.1.10:8888` | Có |
| Hai game worker trên máy A | `127.0.0.1:9001`, `127.0.0.1:9002` | Không |
| MySQL trên máy A | `127.0.0.1:3306` | Không |

Trong toàn bộ hướng dẫn, thay `192.168.1.10` bằng IPv4 thực tế của máy A. Dùng **Windows PowerShell** cho các lệnh Windows và `dotnet`. Đường dẫn `Q:\DoAnNT106` chỉ là ví dụ.

## 1. Máy A chuẩn bị MySQL

Nếu ServerManager đã báo **MySQL: schema auth và trận sẵn sàng** và tài khoản server đã có quyền ghi `OnlineMatches`, có thể chuyển sang bước 2.

1. Cài và khởi động **MySQL Server** trên máy A. MySQL Workbench là giao diện thực thi SQL.
2. Trong Workbench, kết nối bằng tài khoản quản trị. Với database mới, chạy [onlinedb/game_auth.sql](../onlinedb/game_auth.sql) trước. Sau đó chạy [onlinedb/online_matches.sql](../onlinedb/online_matches.sql) để tạo bảng lưu trận. Với database đang dùng, chỉ cần chạy migration mới.
3. Tạo tài khoản ứng dụng và cấp quyền. Thay mật khẩu mẫu trước khi chạy. Nếu tài khoản đã tồn tại, bỏ qua `CREATE USER` và cấp thêm quyền cho bảng mới.

```sql
CREATE USER 'chess_auth'@'127.0.0.1' IDENTIFIED BY 'THAY_BANG_MAT_KHAU_DB';

GRANT SELECT, INSERT ON onlinechessdb.Users
TO 'chess_auth'@'127.0.0.1';

GRANT SELECT, INSERT ON onlinechessdb.UserGameStats
TO 'chess_auth'@'127.0.0.1';

GRANT SELECT, INSERT, UPDATE ON onlinechessdb.OnlineMatches
TO 'chess_auth'@'127.0.0.1';
```

Quyền trên dành cho cấu hình mặc định với **Cho phép chuyển mật khẩu cũ** bỏ chọn. Có thể dùng Workbench để thử kết nối bằng chính tài khoản ứng dụng qua `127.0.0.1`. Giữ thông tin kết nối MySQL trên máy A.

## 2. Máy A lấy IP LAN và mở cổng Gateway

Cho A, B và C vào cùng mạng nội bộ. Trên máy A, chạy:

```powershell
ipconfig
Get-NetConnectionProfile
```

Lấy IPv4 của card Wi-Fi hoặc Ethernet đang dùng. Trên mạng nội bộ tin cậy, đặt profile mạng Windows thành **Private**. Mở **PowerShell bằng quyền Administrator** và thêm quy tắc sau một lần:

```powershell
New-NetFirewallRule -DisplayName 'OnlineChess Gateway TCP 8888 LAN' `
  -Direction Inbound -Action Allow -Protocol TCP -LocalPort 8888 `
  -Profile Private -RemoteAddress LocalSubnet
```

Nếu đổi cổng Gateway, sửa cổng trong firewall, địa chỉ client và lệnh kiểm tra TCP tương ứng.

## 3. Máy A chạy ServerManager và xuất chứng chỉ LAN

Từ thư mục repo trên máy A:

```powershell
Set-Location Q:\DoAnNT106
dotnet run --project .\ServerManager\OnlineChessGame.ServerManager.csproj -c Release
```

Trong tab **Cấu hình**, nhập:

| Trường | Giá trị |
| --- | --- |
| IP gateway | IPv4 LAN máy A, ví dụ `192.168.1.10` |
| Gateway / Worker 1 / Worker 2 | `8888` / `9001` / `9002` |
| Số phòng tối đa / worker | Mặc định `32` |
| Chuỗi kết nối MySQL | Theo mẫu bên dưới |
| Cho phép chuyển mật khẩu cũ | Bỏ chọn |

```text
Server=127.0.0.1;Port=3306;Database=onlinechessdb;User ID=chess_auth;Password=THAY_BANG_MAT_KHAU_DB;SslMode=None;
```

`SslMode=None` trong mẫu dành cho MySQL cục bộ trên loopback. Kết nối client–Gateway vẫn dùng TLS.

1. **Đặt IP gateway trước khi tạo PFX.** Nhập mật khẩu PFX, bấm **Tạo PFX mới…**, lưu vào một tên chưa tồn tại, ví dụ `C:\ChessServer\chess-lan.pfx`. ServerManager thêm IP đã nhập vào SAN của chứng chỉ. Có thể dùng lại PFX tự ký nếu SAN đã chứa đúng địa chỉ client sẽ dùng.
2. Bấm **Kiểm tra MySQL**. Khi schema sẵn sàng, bấm **Khởi động**. Trong tab **Vận hành**, xác nhận Gateway hiển thị IP LAN và cổng `8888`, hai worker hiển thị `127.0.0.1:9001` và `127.0.0.1:9002`.
3. Quay lại **Cấu hình → Xuất .cer LAN…** và lưu `chess-lan.cer`. Khi đang chạy, nút xuất chứng chỉ công khai từ PFX Gateway đang dùng. Chuyển file `.cer` cho B/C qua kênh tin cậy. Giữ PFX và mật khẩu PFX trên máy A.

Nếu nút báo PFX thiếu SAN, tạo PFX mới bằng ServerManager hiện tại rồi xuất lại. Với PFX do CA nội bộ cấp, dùng file `.cer` công khai của **CA gốc** do quản trị viên cung cấp. Tên DNS hoặc IP client dùng phải khớp SAN.

## 4. Chuẩn bị client cho máy B/C

Trên máy có source và .NET SDK Windows, publish client **cùng bản mã nguồn với server**. Bản Windows x64 tự chứa runtime nên B/C có thể chạy trực tiếp:

```powershell
dotnet publish .\Design\OnlineChessGame.csproj -c Release -r win-x64 --self-contained true -o .\publish\Client-LAN
```

Đặt `chess-lan.cer` và `client-config.json` **cạnh `OnlineChessGame.exe`** trong thư mục `publish\Client-LAN`. Nội dung `client-config.json`:

```json
{
  "endpoint": "192.168.1.10:8888",
  "trustedRootCertificate": "chess-lan.cer"
}
```

Sao chép **toàn bộ thư mục** `Client-LAN` sang cả B và C. Hai máy dùng cùng file `.cer` và cùng endpoint. App nạp chứng chỉ từ cấu hình riêng của nó, nên không cần nhập pin hoặc cài chứng chỉ vào kho Windows theo cách này.

Nếu B/C đã có source và SDK, có thể dùng `dotnet build .\Design\OnlineChessGame.csproj -c Release`, đặt hai file cạnh EXE trong `Design\bin\Release\net8.0-windows`, rồi mở đúng EXE đó. Nếu cửa sổ PowerShell đang đặt `CHESS_AUTH_ENDPOINT`, biến này ưu tiên hơn endpoint trong JSON. Xóa biến khỏi cửa sổ đó hoặc đặt nó thành đúng địa chỉ LAN trước khi chạy client.

## 5. Chạy thử trên máy B/C

Trên **từng máy B và C**, chạy trong Windows PowerShell:

```powershell
Test-NetConnection 192.168.1.10 -Port 8888
```

Khi thấy `TcpTestSucceeded : True`:

1. Mở `OnlineChessGame.exe` từ thư mục đã sao chép. Nếu cần, mở **Cấu hình kết nối** trên màn hình đăng nhập và kiểm tra địa chỉ là IP máy A kèm `:8888`.
2. Đăng ký nếu chưa có tài khoản, rồi đăng nhập bằng **hai tài khoản khác nhau** trên B và C.
3. Trên cả hai máy, chọn cùng loại cờ và bấm **Tìm đối thủ online**. Khi vào cùng trận, thử đi một nước và gửi chat, rồi kiểm tra máy còn lại nhận được.
4. Để xác nhận kết thúc kết nối đúng luồng, cho một người **Về sảnh** và kiểm tra thông báo đối thủ đã rời trận trên máy kia.

Nếu chỉ có A và B, có thể chạy thêm một client trên A. Client đó cũng dùng **IP LAN của A** khi Gateway bind đích danh IP LAN.

## 6. Thử chuyển hướng trận mới giữa hai worker

1. Mở tab **Vận hành** trên máy A. Mỗi worker hiển thị PID, trạng thái, số phòng, số phiên và độ trễ/tuổi của heartbeat.
2. Bấm **Ngừng nhận trận · Worker 1**. Trận đã được gán vào Worker 1 có thể tiếp tục. Dùng B/C tìm một trận mới và kiểm tra số phòng của Worker 2 tăng. Bấm **Nhận trận lại · Worker 1** để đưa worker về nhóm nhận trận.
3. Trong buổi test, có thể dùng Task Manager trên máy A dừng tiến trình **đúng PID Worker 1**. Giữ Gateway tiếp tục chạy. Đăng nhập mới vẫn hoạt động, trận mới được chuyển sang Worker 2 nếu worker này còn chỗ. Gateway tự khởi động lại worker bị dừng và chỉ phân trận mới vào nó khi nhận được heartbeat hợp lệ.

Nếu tất cả worker đang đầy hoặc không nhận trận, tìm trận mới sẽ báo thử lại. Trận đang chơi chờ tới khi có worker đủ điều kiện nhận phục hồi.

## 7. Thử phục hồi giữa trận

1. Cho B/C vào cùng trận. Đi vài nước rồi kiểm tra hai bàn cờ khớp nhau. Trong tab **Vận hành**, ghi nhận worker nào tăng số phòng lúc tạo trận. Nếu chỉ có một trận thử, đó là worker hiển thị một phòng. Giữ worker còn lại ở trạng thái **Sẵn sàng**, với ít nhất một chỗ trống.
2. Trong buổi test, dừng **đúng PID worker đang giữ phòng** trên máy A. Giữ Gateway, MySQL và hai client chạy.
3. Hai client có thể hiện **Đang chuyển worker…** trong lúc Gateway tìm chỗ. Khi đồng bộ xong, kiểm tra quân cờ, lượt đi và đồng hồ. Đồng hồ vẫn tính thời gian trong lúc chuyển worker. Thử đi tiếp và kết thúc trận bằng đầu hàng hoặc đồng ý hòa.
4. Trên database thử, kiểm tra dòng tương ứng trong `OnlineMatches`. `generation` tăng khi chuyển worker, `revision` tăng khi xác nhận thay đổi nghiệp vụ và `result` có một kết quả cuối cho mỗi phòng.

```sql
SELECT room_id, game_type, generation, revision, result
FROM onlinechessdb.OnlineMatches
ORDER BY updated_at DESC
LIMIT 10;
```

Phép thử này áp dụng cho lỗi một worker. Giữ hai client kết nối với Gateway trong suốt bài thử.

## Xử lý nhanh khi lỗi

| Hiện tượng | Kiểm tra |
| --- | --- |
| B/C báo `TcpTestSucceeded : False` | Kiểm tra Gateway có đang chạy và bind IP LAN hay không. Kiểm tra firewall có áp dụng cho profile hiện tại và B/C có ở trong phạm vi `LocalSubnet`. Wi-Fi khách hoặc Wi-Fi trường có thể chặn kết nối giữa thiết bị. Có thể thử mạng riêng hoặc hotspot cho phép các máy liên lạc. |
| Ping timeout, TCP thành công | Tiếp tục test client. TCP `8888` là phép kiểm tra cần cho ứng dụng. |
| TCP thành công, client báo lỗi TLS | Kiểm tra IP trong endpoint có khớp SAN, `.cer` được xuất từ PFX Gateway đang chạy, và JSON cùng `.cer` nằm cạnh đúng EXE. Kiểm tra client và server cùng phiên bản Gateway v3. |
| Trước đây test localhost được, chuyển LAN thì lỗi | Nếu PFX chỉ có SAN cho `127.0.0.1`/localhost, đặt IP LAN trong ServerManager rồi tạo PFX mới. Xuất và phân phối lại `.cer` cho B/C. |
| Đăng nhập được nhưng không ghép trận | Dùng hai tài khoản, chọn cùng loại cờ, kiểm tra hai worker trong tab Vận hành. Nếu phiên đăng nhập hết hạn, đăng xuất và đăng nhập lại. |
| Client báo lỗi kho lưu trận | Kiểm tra đã chạy migration `online_matches.sql` và cấp quyền `SELECT, INSERT, UPDATE` cho tài khoản ứng dụng. |
| MySQL báo `Access denied` | Thử cùng user, mật khẩu, host và cổng trong Workbench trên máy A. Kiểm tra cả quyền `INSERT` nếu chỉ đăng ký bị lỗi. |

Có thể kiểm tra địa chỉ lắng nghe ngay trên máy A:

```powershell
Get-NetTCPConnection -State Listen |
  Where-Object { $_.LocalPort -in 8888, 9001, 9002 } |
  Format-Table LocalAddress, LocalPort
```

Khi IP LAN máy A đổi, cập nhật IP bind và endpoint của client. Nếu SAN của PFX hiện tại chưa có IP mới, tạo lại PFX rồi xuất lại `.cer`. Để phát hành Internet bằng chứng chỉ CA công cộng, xem [hướng dẫn VPS](PHAT_HANH_VPS.md).
