# Thiết lập MySQL và chạy ứng dụng qua LAN

Hướng dẫn theo mã nguồn hiện tại, dùng **Windows PowerShell** cho các lệnh ứng dụng và mạng; SQL chạy trong **MySQL Workbench**. Máy A chạy MySQL và ServerManager, máy B/C chạy client.

**Phạm vi chạy được:** đăng ký và đăng nhập qua mạng, đọc hồ sơ/Elo từ MySQL, vào sảnh và chơi solo. Luồng hai máy cùng chơi một trận, chat liên máy và đồng bộ kết quả còn thiếu phần server. Cài database không tự hoàn thiện các chức năng đó.

## Trước khi làm: database, tài khoản và PFX là gì?

Không cần tạo tài khoản trên website MariaDB/MySQL. Bạn cài MySQL Server trên máy A, dùng Workbench kết nối vào đó rồi chạy file SQL để tạo database ngay trên máy A. Chỉ cài Workbench thì chưa có MySQL Server; SSMS cũng không thay thế Workbench cho mã nguồn MySQL hiện tại.

| Thông tin | Tạo ở đâu? | Dùng ở đâu? |
|---|---|---|
| Mật khẩu quản trị `root` | Khi cấu hình MySQL Server | Workbench để tạo schema và cấp quyền |
| Tài khoản DB `chess_auth` và mật khẩu | Chạy SQL trong Workbench | Ô chuỗi kết nối MySQL của ServerManager |
| Tài khoản người chơi | Màn hình Đăng ký của game | Màn hình Đăng nhập của game |
| Tệp PFX và mật khẩu PFX | Nhập mật khẩu rồi bấm Tạo PFX mới… trong ServerManager | Server mở chứng chỉ/khóa riêng để phục vụ TLS |
| Certificate pin | ServerManager tự tính từ chứng chỉ, dài 64 ký tự hex | Biến CHESS_CERT_PIN trên client |

PFX không lấy từ database hoặc website. Client chỉ nhận pin; PFX và mật khẩu PFX giữ trên server. Những lần chạy sau dùng lại PFX đã tạo và đúng mật khẩu. SQL Server cần chuyển đổi mã nguồn, không dùng được bằng cách chỉ đổi chuỗi kết nối.

Nếu mới làm lần đầu, nên theo [README: thử cùng máy trước rồi chuyển LAN](../README.md#bắt-đầu-từ-đâu). Hướng dẫn bên dưới là quy trình trực tiếp cho cấu hình LAN.

## 1. Sơ đồ và địa chỉ mẫu

```text
Máy B: client ──┐
               ├── TCP/TLS → Máy A: ServerManager :8888 → MySQL cục bộ :3306
Máy C: client ──┘                       ├── worker loopback :9001
                                      └── worker loopback :9002
```

| Máy | Vai trò | IP ví dụ |
|---|---|---|
| A | MySQL + ServerManager | 192.168.1.10 |
| B | Client người chơi thứ nhất | 192.168.1.11 |
| C | Client người chơi thứ hai | 192.168.1.12 |

Thay `192.168.1.10` bằng IP thật máy A trong tất cả ví dụ. Hai máy đã đủ thử đăng ký/đăng nhập qua LAN. Các máy phải kết nối được với nhau, ví dụ cùng Wi-Fi hoặc router; Wi-Fi khách có thể chặn liên lạc giữa thiết bị.

## 2. Chọn đúng terminal

Mở **Windows PowerShell**, chuyển đến thư mục mã nguồn:

```powershell
Set-Location Q:\DoAnNT106
dotnet --info
```

Đường dẫn `Q:\DoAnNT106` là của máy đang phát triển; máy khác dùng đường dẫn repository trên máy đó. Cần .NET SDK hỗ trợ target .NET 8 Windows khi chạy từ source.

Nếu dùng WSL, lệnh `dotnet` có thể gọi bản Linux và báo `[/usr/lib/dotnet/host/fxr] does not exist`. WinForms phải dùng SDK Windows:

```bash
"/mnt/c/Program Files/dotnet/dotnet.exe" run --project ServerManager/OnlineChessGame.ServerManager.csproj
```

Các đoạn `$env:...`, `Test-NetConnection`, `New-NetFirewallRule` bên dưới phải chạy bằng **Windows PowerShell**, không dán trực tiếp vào Bash/WSL.

## 3. Cài MySQL trên máy A

Nếu đã có MySQL hoạt động, dùng instance đó và chuyển sang bước 4. Nếu chưa có:

1. Cài **MySQL Community Server cho Windows** từ https://dev.mysql.com/downloads/mysql/.
2. Cài **MySQL Workbench** từ https://dev.mysql.com/downloads/workbench/ để chạy SQL bằng giao diện.
3. Trong bước cấu hình server, bật TCP/IP, chọn cổng `3306` nếu cổng còn trống, đặt mật khẩu quản trị `root` và ghi nhớ.
4. Cấu hình MySQL chạy dưới dạng Windows Service, khởi động dịch vụ. Tên dịch vụ tùy bản cài; không mặc định mọi máy đều có tên `MySQL80`.
5. Hướng dẫn này đặt MySQL cùng máy với server ứng dụng; không cần mở cổng MySQL ra LAN.

Kiểm tra dịch vụ và cổng trong PowerShell:

```powershell
Get-Service | Where-Object { $_.Name -match 'mysql|maria' }
Test-NetConnection 127.0.0.1 -Port 3306
```

Cần thấy dịch vụ `Running` và `TcpTestSucceeded : True`. Nếu MySQL dùng cổng khác, dùng cổng đó ở Workbench và chuỗi kết nối; cổng ứng dụng `8888` không thay đổi theo cổng DB.

Trong Workbench, tạo kết nối quản trị:

- Connection Method: **Standard (TCP/IP)**.
- Hostname: `127.0.0.1`.
- Port: `3306` hoặc cổng thực tế.
- Username: `root`.
- Password: mật khẩu đã đặt lúc cài MySQL.

Chọn **Test Connection**, sau đó mở kết nối.

## 4. Tạo schema từ repository

Với cơ sở dữ liệu mới, trong Workbench chọn **File → Open SQL Script**, mở:

```text
Q:\DoAnNT106\onlinedb\game_auth.sql
```

Chạy **toàn bộ script**, không chỉ dòng đang đặt con trỏ. Script tạo schema `onlinechessdb` và tám bảng. Không cần dùng màn hình Data Import cho tệp này. Nếu đã có dữ liệu sử dụng, sao lưu và kiểm tra trước; `CREATE TABLE IF NOT EXISTS` không tự nâng cấp cấu trúc bảng cũ.

Chạy các câu SQL sau để kiểm tra:

```sql
USE onlinechessdb;
SHOW TABLES;
SHOW COLUMNS FROM Users;
SHOW COLUMNS FROM UserGameStats;
```

Tám bảng mong đợi:

```text
Users
UserGameStats
Rooms
RoomMembers
Matches
MatchPlayers
RoomMessages
MatchmakingQueue
```

Không cần chèn tài khoản người chơi bằng SQL. Ứng dụng sẽ tạo tài khoản và băm mật khẩu khi đăng ký.

## 5. Tạo tài khoản MySQL cho server

Tài khoản DB này khác tài khoản người chơi. Chạy bằng kết nối quản trị Workbench, thay mật khẩu mẫu trước khi thực hiện:

```sql
CREATE USER 'chess_auth'@'127.0.0.1'
IDENTIFIED BY 'THAY_BANG_MAT_KHAU_DB_CUA_BAN';

GRANT SELECT, INSERT ON onlinechessdb.Users
TO 'chess_auth'@'127.0.0.1';

GRANT SELECT, INSERT ON onlinechessdb.UserGameStats
TO 'chess_auth'@'127.0.0.1';
```

Quyền trên phục vụ đăng ký, đăng nhập và kiểm tra schema của UI, với migration mật khẩu cũ tắt. Nếu user đã tồn tại, kiểm tra lại thay vì chạy `CREATE USER` lần nữa. Không cần quyền tạo/xóa database hoặc cấp toàn quyền để chạy auth.

Nếu MySQL báo `Access denied`, kiểm tra đúng mật khẩu/quyền trước; thông báo có host `localhost` chưa tự chứng minh nguyên nhân là host. Khi đã xác định cần tài khoản cục bộ `chess_auth`@`localhost` và tài khoản đó chưa tồn tại, tạo với cùng mật khẩu DB và quyền:

```sql
CREATE USER 'chess_auth'@'localhost'
IDENTIFIED BY 'THAY_BANG_MAT_KHAU_DB_CUA_BAN';

GRANT SELECT, INSERT ON onlinechessdb.Users
TO 'chess_auth'@'localhost';

GRANT SELECT, INSERT ON onlinechessdb.UserGameStats
TO 'chess_auth'@'localhost';
```

Có thể tạo thêm kết nối Workbench bằng `chess_auth`, host `127.0.0.1`, để xác nhận tài khoản DB đăng nhập được trước khi cấu hình ứng dụng.

## 6. Lấy IP LAN và chuẩn bị firewall máy A

Trong Windows PowerShell máy A:

```powershell
ipconfig
```

Lấy **IPv4 Address** của card Wi-Fi/Ethernet đang dùng chung với máy B/C. Không lấy IP WSL, Docker, VPN hoặc `127.0.0.1`. Ví dụ IPv4 thực tế là `192.168.1.10`.

Trên mạng nội bộ tin cậy, đặt Windows Network profile thành **Private** trong Settings của kết nối đang dùng. Sau đó mở **PowerShell bằng Run as administrator** và thêm quy tắc cho gateway:

```powershell
New-NetFirewallRule `
  -DisplayName "OnlineChess Auth TCP 8888 LAN" `
  -Direction Inbound `
  -Action Allow `
  -Protocol TCP `
  -LocalPort 8888 `
  -Profile Private `
  -RemoteAddress LocalSubnet
```

Chỉ chạy lệnh thêm rule một lần. Nếu đổi cổng gateway trên UI, tạo quy tắc đúng cổng mới. Client chỉ kết nối gateway; không cần mở cổng 9001, 9002 hoặc 3306 cho các máy chơi trong mô hình này.

## 7. Khởi động ServerManager trên máy A

Trong PowerShell thường tại repository:

```powershell
dotnet run --project ServerManager/OnlineChessGame.ServerManager.csproj
```

Mở tab **Cấu hình**, nhập:

| Trường | Giá trị ví dụ |
|---|---|
| IP gateway | `192.168.1.10` — thay bằng IPv4 thật máy A |
| Gateway | `8888` |
| Worker 1 | `9001` |
| Worker 2 | `9002` |
| Mật khẩu PFX | Tự đặt, khác mật khẩu DB |
| Chuỗi kết nối MySQL | Theo mẫu bên dưới |
| Cho phép chuyển mật khẩu cũ | Bỏ chọn |

Chuỗi kết nối mẫu:

```text
Server=127.0.0.1;Port=3306;Database=onlinechessdb;User ID=chess_auth;Password=THAY_BANG_MAT_KHAU_DB_CUA_BAN;SslMode=None;
```

`SslMode=None` ở ví dụ này chỉ áp dụng kết nối MySQL cục bộ cùng máy qua loopback. Kết nối client–server ứng dụng vẫn dùng TLS và pin chứng chỉ. Nếu DB nằm máy khác, cần cấu hình TLS MySQL riêng thay vì áp dụng mẫu này nguyên dạng.

Thực hiện lần lượt:

1. Nhập mật khẩu PFX, chọn **Tạo PFX mới…**.
2. Chọn một tệp chưa tồn tại, ví dụ trong `%LOCALAPPDATA%\ChessServer\server.pfx` sau khi tạo thư mục; giữ PFX trên máy A.
3. Nếu đã tạo PFX trước đây, chọn **Chọn PFX…** và nhập đúng mật khẩu; không tạo lại mỗi lần chạy.
4. Chọn **Kiểm tra MySQL**. Cần thấy `MySQL: schema auth sẵn sàng`.
5. Chọn **Khởi động**. Cần thấy `ĐANG CHẠY` và endpoint gateway đúng IP LAN trong tab Vận hành.
6. Quay lại tab Cấu hình, chọn **Sao chép pin**. Pin là chuỗi 64 ký tự hex để cấp cho client qua kênh tin cậy.

Hai worker vẫn chỉ mở loopback. Không chạy đồng thời server console và ServerManager trên cùng ba cổng. Giữ cửa sổ server mở trong lúc thử nghiệm.

Nếu máy A đổi IP khi chuyển Wi-Fi hoặc nhận DHCP mới, dừng server rồi cập nhật IP bind và endpoint của client. Việc đổi IP không yêu cầu tạo lại PFX. Việc đổi PFX làm thay đổi pin và cần cập nhật client.

## 8. Thử client ngay trên máy A

Mở **PowerShell khác** trên máy A. Dùng cùng IP LAN đã bind; nếu server chỉ bind IP LAN thì không thay bằng loopback khi thử:

```powershell
Set-Location Q:\DoAnNT106
$env:CHESS_AUTH_ENDPOINT = '192.168.1.10:8888'
$env:CHESS_CERT_PIN = Read-Host 'Dan pin SHA-256 tu ServerManager'

dotnet run --project Design/OnlineChessGame.csproj
```

Khi được hỏi pin, dán đúng 64 ký tự, không thêm nhãn `CHESS_CERT_PIN=`. Trên màn hình đăng nhập, kiểm tra ô địa chỉ server vẫn là `192.168.1.10:8888`.

Chọn Đăng ký, nhập tên đăng nhập mới, tên hiển thị và mật khẩu 8–128 ký tự; xác nhận mật khẩu giống nhau. Sau khi đăng ký thành công, đăng nhập bằng chính tài khoản đó. Cần vào được sảnh và thấy nhật ký đăng ký/đăng nhập trên server.

Biến `$env:` chỉ có hiệu lực trong cửa sổ PowerShell hiện tại và tiến trình con được mở từ đó. Nếu chạy client từ cửa sổ khác hoặc mở EXE bằng double-click, biến vừa đặt có thể không được kế thừa; mở lại client từ PowerShell đã cấu hình.

## 9. Chạy client từ máy B/C

Các máy client dùng cùng phiên bản source và SDK Windows phù hợp nếu chạy bằng `dotnet run`. Không cần cài MySQL hoặc mang tài khoản DB/PFX sang máy client để thực hiện auth.

Đầu tiên, mở PowerShell máy B/C và thử cổng:

```powershell
Test-NetConnection 192.168.1.10 -Port 8888
```

Chỉ tiếp tục khi thấy:

```text
TcpTestSucceeded : True
```

Sau đó, chuyển đến repository trên máy client và chạy:

```powershell
# Thay đường dẫn này bằng thư mục source thực tế trên máy B/C.
Set-Location C:\Projects\DoAnNT106
$env:CHESS_AUTH_ENDPOINT = '192.168.1.10:8888'
$env:CHESS_CERT_PIN = Read-Host 'Dan pin SHA-256 tu ServerManager'

dotnet run --project Design/OnlineChessGame.csproj
```

Pin trên B và C phải trùng pin chứng chỉ của server A. Địa chỉ server trên B/C phải là IP máy A, không phải `127.0.0.1`, `localhost` hoặc `0.0.0.0`. Tại màn hình đăng nhập, điền đúng endpoint trước khi chuyển sang Đăng ký.

## 10. Đối chiếu dữ liệu và kết quả

Từ Workbench máy A, chạy truy vấn không đọc mật khẩu:

```sql
SELECT user_id, username, display_name, created_at
FROM onlinechessdb.Users
ORDER BY user_id DESC;

SELECT u.username, s.game_type, s.elo, s.wins, s.losses, s.draws
FROM onlinechessdb.Users AS u
JOIN onlinechessdb.UserGameStats AS s ON s.user_id = u.user_id
ORDER BY u.user_id DESC, s.game_type;
```

Mỗi tài khoản mới cần có hai dòng thống kê CHESS/CARO, Elo ban đầu 1200.

Các thao tác kiểm tra mong đợi:

| Thao tác | Kết quả |
|---|---|
| B đăng ký tên mới | Thành công; xuất hiện trong Users và hai dòng UserGameStats |
| C đăng ký trùng tên B | Bị từ chối với tên đã tồn tại |
| B đăng nhập đúng | Vào sảnh, nhận tên và Elo từ DB |
| C dùng sai mật khẩu | Bị từ chối |
| B/C thử nhiều yêu cầu lần lượt | Bộ đếm cả hai worker tăng |
| Dừng server rồi đăng nhập | Client báo không kết nối được |

Kết nối auth ngắn, nên số kết nối trên UI có thể trở về 0 ngay sau khi đăng nhập; đây là bình thường. Số đó không phải số tài khoản đang ở sảnh.

## 11. Các lỗi thường gặp

| Hiện tượng | Cách kiểm tra |
|---|---|
| `/usr/lib/dotnet/host/fxr` không tồn tại | Đang gọi .NET Linux trong WSL; dùng Windows PowerShell hoặc `dotnet.exe` |
| MySQL port 3306 không kết nối | Dịch vụ chưa chạy, sai cổng hoặc MySQL chưa được cài/cấu hình |
| MySQL `Access denied` | Kiểm tra user, mật khẩu, host tài khoản và quyền; thử cùng thông tin bằng Workbench |
| UI báo không truy cập được schema auth | Kiểm tra Database=onlinechessdb, đã chạy đầy đủ script, bảng/cột và quyền SELECT |
| UI không mở được cổng | IP bind không thuộc máy A, cổng bị chiếm hoặc server console đang chạy |
| `Test-NetConnection ... -Port 8888` thất bại | Kiểm tra server đang chạy, IP/cổng đúng, firewall đúng profile và Wi-Fi không bật client isolation |
| TCP thành công nhưng client báo TLS | Kiểm tra pin đủ 64 hex và đúng chứng chỉ hiện tại; biến môi trường được đặt trước khi chạy client |
| Client vẫn trỏ `127.0.0.1` | Đặt endpoint và sửa ô địa chỉ server; khởi chạy lại từ PowerShell đã cấu hình |
| Đăng ký không tạo được tài khoản | Kiểm tra quyền INSERT trên cả Users và UserGameStats; không chỉ dựa vào nút kiểm tra MySQL vì nút đó chỉ đọc schema |
| `reset_required` | Tài khoản cũ có mật khẩu dạng rõ; thử tài khoản mới, chưa có quy trình reset tích hợp |
| Bấm tìm trận rồi không đồng bộ hai máy | Gameplay online chưa hoàn thiện; đây không chỉ là lỗi cài DB hoặc firewall |

Kiểm tra cổng trên máy A:

```powershell
Get-NetTCPConnection -State Listen |
    Where-Object { $_.LocalPort -in 8888, 9001, 9002 }
```

Cần thấy gateway trên IP LAN và hai worker trên `127.0.0.1`. Ping thất bại chưa đủ kết luận cổng ứng dụng bị chặn; ưu tiên kết quả kiểm tra TCP 8888.

## 12. Chạy client trên máy không có SDK (tùy chọn)

Trên máy phát triển, xuất bản bản tự chứa cho Windows x64:

```powershell
dotnet publish Design/OnlineChessGame.csproj `
  -c Release -r win-x64 --self-contained true `
  -o .\publish\Client
```

Sao chép **toàn bộ thư mục** `publish\Client` sang B/C. Trong PowerShell tại thư mục đã sao chép:

```powershell
$env:CHESS_AUTH_ENDPOINT = '192.168.1.10:8888'
$env:CHESS_CERT_PIN = Read-Host 'Dan pin SHA-256 tu ServerManager'
.\OnlineChessGame.exe
```

Bản này chứa runtime, không cần cài SDK trên máy client. Vẫn cần đặt endpoint và pin trước khi chạy. Luồng hàng chờ/lưu trận MySQL cũ trong client chưa được chuyển hoàn toàn sang server; bản publish không làm các luồng đó trở thành gameplay LAN đầy đủ.
