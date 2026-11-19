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

## Bắt đầu từ đâu?

Nếu mới chạy đồ án lần đầu, làm theo thứ tự **cài MySQL → tạo database → tạo tài khoản DB → tạo PFX → chạy server → chạy client cùng máy → thử LAN**. Mỗi bước bên dưới ghi rõ thao tác ở đâu và kết quả cần thấy trước khi chuyển sang bước tiếp theo.

Bạn không cần tạo tài khoản trên website MySQL/MariaDB để tạo database. Database của hướng dẫn này nằm trên máy Windows của bạn.

- **Máy A**: cài MySQL, lưu database và chạy giao diện `ServerManager`.
- **Máy B/C**: chạy ứng dụng game, kết nối đến máy A để đăng ký/đăng nhập.
- Khi mới thiết lập, dùng **một máy A chạy cả server và client** trước. Sau khi thành công mới mở kết nối từ máy khác.

```text
Ứng dụng game trên A/B/C
        │ kết nối bảo mật đến cổng 8888
        ▼
ServerManager trên máy A
        │ đọc/ghi dữ liệu qua cổng 3306 trên chính máy A
        ▼
MySQL trên máy A → database onlinechessdb → các bảng tài khoản/thống kê
```

### Phân biệt các phần mềm và thông tin đăng nhập

| Tên | Nghĩa và cách sử dụng |
|---|---|
| MySQL Server | Chương trình chạy nền để lưu và xử lý dữ liệu; phải cài và khởi động trên máy A |
| MySQL Workbench | Cửa sổ quản lý MySQL, dùng để mở kết nối và chạy SQL; chỉ cài Workbench thì chưa có database server |
| MariaDB | Một hệ quản trị DB khác, tương thích phần auth của dự án; hướng dẫn này chọn MySQL để thống nhất thao tác |
| SQL Server và SSMS | Bộ phần mềm của Microsoft; mã nguồn hiện tại chưa hỗ trợ bằng cách chỉ đổi chuỗi kết nối |
| Database `onlinechessdb` | Nơi chứa dữ liệu của đồ án, được tạo khi chạy file SQL trong repository |
| Tài khoản MySQL `root` | Tài khoản quản trị do bạn đặt mật khẩu lúc cấu hình MySQL; dùng tạo database và cấp quyền |
| Tài khoản MySQL `chess_auth` | Tài khoản tạo bằng SQL để chương trình server đọc/ghi dữ liệu |
| Tài khoản người chơi | Do người chơi tạo trong màn hình Đăng ký của game; không dùng tài khoản `root` hoặc `chess_auth` để đăng nhập game |
| Tệp `.pfx` và mật khẩu PFX | Chứng chỉ/khóa riêng của server và mật khẩu bảo vệ tệp; tạo ngay trong ServerManager |
| Certificate pin | Dấu vân tay SHA-256 của chứng chỉ, dài 64 ký tự hex; chương trình tự tính, client dùng để nhận diện server |

Mật khẩu `root`, mật khẩu `chess_auth`, mật khẩu PFX và mật khẩu người chơi có mục đích riêng. Tự đặt mật khẩu cho từng loại; pin được phần mềm tạo từ chứng chỉ, bạn không tự nghĩ ra pin.

### Nếu chỉ cài Microsoft SQL Server thì sao?

Bạn cần cài thêm MySQL để làm theo hướng dẫn này. Dự án hiện dùng `MySqlConnector` tại server và script MySQL; **không chạy `game_auth.sql` trong SSMS** và không nhập chuỗi kết nối SQL Server vào ô MySQL. Chuyển sang SQL Server cần sửa lớp truy cập dữ liệu, schema và kiểm tra DB trên UI. PFX/TLS vẫn độc lập với việc chọn loại database.

## Bước 1 — Mở đúng terminal và kiểm tra .NET

**Thực hiện trên máy A, trong Windows PowerShell.** Mở Start, tìm `Windows PowerShell` và mở cửa sổ thường; các bước đầu chưa cần quyền Administrator.

```powershell
Set-Location Q:\DoAnNT106
dotnet --info
```

`Q:\DoAnNT106` là đường dẫn trên máy đang phát triển. Nếu bạn lưu source ở chỗ khác, thay bằng đường dẫn có file `README.md` và các thư mục `Design`, `ServerManager`, `onlinedb`.

Cần Windows và .NET SDK hỗ trợ build .NET 8 Windows. Nếu chỉ cài runtime mà không có SDK, chưa chạy được lệnh `dotnet run`. Có thể dùng Visual Studio với workload **.NET desktop development** hoặc cài .NET SDK cho Windows.

**Nếu đang dùng Bash/WSL** và thấy lỗi `[/usr/lib/dotnet/host/fxr] does not exist`, lệnh `dotnet` đang gọi bản Linux thiếu host/runtime. Với ứng dụng WinForms, dùng SDK Windows:

```bash
cd /mnt/q/DoAnNT106
"/mnt/c/Program Files/dotnet/dotnet.exe" --info
"/mnt/c/Program Files/dotnet/dotnet.exe" run --project ServerManager/OnlineChessGame.ServerManager.csproj
```

Để dễ theo dõi các bước tiếp theo, chuyển về **Windows PowerShell**. Các lệnh `$env:...`, `Test-NetConnection` và `New-NetFirewallRule` phía dưới dùng cú pháp PowerShell; không dán nguyên vào Bash.

**Kết quả cần thấy:** `dotnet --info` hiển thị SDK Windows và không báo thiếu `host/fxr`.

### Cài Stockfish cho chế độ Cờ vua — Đấu với máy

Thực hiện một lần trong Windows PowerShell tại thư mục gốc dự án:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\setup-stockfish.ps1
dotnet build .\Design\OnlineChessGame.csproj
```

Script tải Stockfish Windows x64 chính thức và tạo file
`Engines\Stockfish\stockfish.exe`. Khi build, engine được sao chép tới
`Design\bin\Debug\net8.0-windows\Engines\Stockfish\stockfish.exe`.

Khi vào sảnh và chọn **Đấu với Máy (Solo)** ở phần Cờ vua, ứng dụng cho chọn
bốn mức: Dễ, Trung bình, Khó và Chuyên gia. Nếu ứng dụng báo không tìm thấy
Stockfish, kiểm tra file trên đã tồn tại rồi build/chạy lại client:

```powershell
dotnet run --project .\Design\OnlineChessGame.csproj
```

Nếu muốn đặt engine ở thư mục khác:

```powershell
$env:STOCKFISH_PATH = "C:\duong-dan\stockfish.exe"
dotnet run --project .\Design\OnlineChessGame.csproj
```

Stockfish chạy cục bộ, không cần tài khoản, API key, database hoặc Internet
trong lúc chơi. Khi phân phối binary, giữ kèm `Copying.txt`, `AUTHORS` và mã
nguồn/liên kết mã nguồn đúng phiên bản theo GPL v3.

## Bước 2 — Cài và khởi động MySQL

**Thực hiện trên máy A. Nếu MySQL Server đã chạy, kiểm tra cổng rồi sang bước 3.**

1. Tải và cài [MySQL Community Server cho Windows](https://dev.mysql.com/downloads/mysql/).
2. Cài [MySQL Workbench](https://dev.mysql.com/downloads/workbench/) để quản lý bằng giao diện. Database được tạo trên máy bạn sau khi cài; không cần đăng ký tài khoản website để tạo database.
3. Trong bước cấu hình MySQL Server, bật TCP/IP và chọn cổng `3306` nếu cổng còn trống. Nếu dùng cổng khác, ghi lại để thay trong các ví dụ MySQL phía dưới.
4. Đặt mật khẩu cho tài khoản quản trị **`root`**. Đây là mật khẩu dùng để mở kết nối quản trị trong Workbench, chưa phải mật khẩu game hoặc PFX.
5. Cấu hình MySQL chạy dưới dạng **Windows Service** và khởi động dịch vụ. Tên dịch vụ tùy bản cài, có thể khác `MySQL80`.

Trong Windows PowerShell, kiểm tra:

```powershell
Get-Service | Where-Object { $_.Name -match 'mysql|maria' }
Test-NetConnection 127.0.0.1 -Port 3306
```

**Kết quả cần thấy:** dịch vụ MySQL ở trạng thái `Running`, và kiểm tra cổng có `TcpTestSucceeded : True`.

Nếu dịch vụ đang dừng, mở ứng dụng **Services** của Windows, tìm đúng dịch vụ MySQL và chọn **Start**. Nếu không có dịch vụ, kiểm tra bạn đã cài/cấu hình MySQL Server hay mới cài Workbench. Nếu dùng cổng khác `3306`, sửa lệnh kiểm tra theo cổng thực tế.

## Bước 3 — Kết nối Workbench và tạo database

**Thực hiện trong MySQL Workbench trên máy A. Các lệnh SQL ở bước này không chạy trong PowerShell.**

### 3.1. Mở kết nối quản trị

1. Mở Workbench. Tại màn hình **MySQL Connections**, chọn dấu **+** để tạo kết nối.
2. Nhập các trường:

| Trường Workbench | Giá trị |
|---|---|
| Connection Name | `Chess local admin` — tên để bạn nhận biết, tùy chọn |
| Connection Method | `Standard (TCP/IP)` |
| Hostname | `127.0.0.1` — nghĩa là MySQL trên chính máy A |
| Port | `3306` hoặc cổng đã cấu hình |
| Username | `root` |
| Password | Mật khẩu `root` đã đặt lúc cài MySQL |

3. Chọn **Test Connection** và nhập mật khẩu khi được hỏi. Khi kết nối thành công, lưu và mở kết nối vừa tạo.

### 3.2. Chạy file tạo bảng

1. Trong cửa sổ kết nối Workbench, chọn **File → Open SQL Script**.
2. Mở [onlinedb/game_auth.sql](onlinedb/game_auth.sql), ví dụ `Q:\DoAnNT106\onlinedb\game_auth.sql`.
3. Chạy **toàn bộ script** bằng nút Execute dành cho phần được chọn/toàn bộ nội dung. Có thể chọn toàn bộ nội dung trong vùng soạn SQL rồi bấm Execute; tránh chỉ thực thi một câu tại vị trí con trỏ.
4. Xem vùng **Action Output** để xác nhận các câu lệnh tạo database/bảng không báo lỗi. Làm mới danh sách **Schemas** nếu chưa thấy `onlinechessdb`.

Script tạo database tên `onlinechessdb`, sau đó tạo các bảng trong database đó. Đây là bước khởi tạo database; không thực hiện trên website MySQL/MariaDB. Với DB đã có dữ liệu sử dụng, sao lưu và kiểm tra trước khi chạy: `CREATE TABLE IF NOT EXISTS` không tự nâng cấp cấu trúc bảng cũ.

### 3.3. Kiểm tra kết quả

Mở tab SQL trong Workbench, dán rồi chạy:

```sql
USE onlinechessdb;
SHOW TABLES;
```

**Kết quả cần thấy:** tám bảng `Users`, `UserGameStats`, `Rooms`, `RoomMembers`, `Matches`, `MatchPlayers`, `RoomMessages`, `MatchmakingQueue`.

Bảng `Users` chưa có người chơi ở thời điểm này là bình thường. Không chèn mật khẩu người chơi thủ công; màn hình Đăng ký sẽ tạo tài khoản với mật khẩu đã băm.

## Bước 4 — Tạo tài khoản DB cho chương trình server

**Vẫn thực hiện trong Workbench, bằng kết nối `root` ở bước 3. Chỉ cần tạo tài khoản DB này một lần.**

`root` dùng để quản trị. Tạo thêm `chess_auth` để chương trình server có quyền đọc và tạo tài khoản người chơi.

Trước khi chạy, thay `THAY_BANG_MAT_KHAU_DB_CUA_BAN` bằng mật khẩu DB bạn tự đặt. Mật khẩu sẽ được dùng lại ở bước 5; đây không phải mật khẩu `root`.

```sql
CREATE USER 'chess_auth'@'127.0.0.1'
IDENTIFIED BY 'THAY_BANG_MAT_KHAU_DB_CUA_BAN';

GRANT SELECT, INSERT ON onlinechessdb.Users
TO 'chess_auth'@'127.0.0.1';

GRANT SELECT, INSERT ON onlinechessdb.UserGameStats
TO 'chess_auth'@'127.0.0.1';
```

Trong SQL trên, `127.0.0.1` giới hạn tài khoản DB cho kết nối từ máy cục bộ. Client LAN sẽ liên lạc với server ứng dụng; không cần tạo tài khoản DB cho mỗi máy chơi. Các quyền trên dùng với chuyển mật khẩu cũ **tắt**.

**Kiểm tra trước khi tiếp tục:** tạo một kết nối Workbench khác với Hostname=`127.0.0.1`, Port=`3306`, Username=`chess_auth`, Password=mật khẩu vừa đặt. Chọn **Test Connection**. Nếu thành công, thông tin DB để nhập vào ServerManager đã đúng.

Nếu báo user đã tồn tại, không chạy lại `CREATE USER`. Nếu báo `Access denied`, kiểm tra mật khẩu và tài khoản/host; xem phần xử lý lỗi ở cuối README trước khi tạo thêm tài khoản.

## Bước 5 — Mở ServerManager và kết nối database

**Thực hiện trong PowerShell trên máy A, tại thư mục source.**

```powershell
dotnet run --project ServerManager/OnlineChessGame.ServerManager.csproj
```

Cửa sổ quản trị mở ra chưa có nghĩa listener đã chạy. Chọn tab **Cấu hình** và nhập:

| Trường trên UI | Giá trị cho lần thử cùng máy |
|---|---|
| IP gateway | `127.0.0.1` |
| Gateway | `8888` |
| Worker 1 | `9001` |
| Worker 2 | `9002` |
| Cho phép chuyển mật khẩu cũ | Bỏ chọn |

Trong ô **Chuỗi kết nối MySQL**, nhập một dòng:

```text
Server=127.0.0.1;Port=3306;Database=onlinechessdb;User ID=chess_auth;Password=THAY_BANG_MAT_KHAU_DB_CUA_BAN;SslMode=None;
```

Thay đúng mật khẩu đã tạo ở bước 4. Ví dụ chỉ là mẫu, không giữ nguyên phần `THAY_BANG...`. Nếu mật khẩu có dấu chấm phẩy, giá trị `Password` phải được đặt trong dấu nháy kép của cú pháp connection string, chẳng hạn `Password="gia_tri_co_dau;cham_phay";`.

| Phần trong chuỗi | Ý nghĩa |
|---|---|
| `Server=127.0.0.1` | MySQL nằm trên chính máy chạy ServerManager |
| `Port=3306` | Cổng của MySQL; khác cổng game `8888` |
| `Database=onlinechessdb` | Database đã tạo từ file SQL |
| `User ID=chess_auth` | Tài khoản DB ở bước 4 |
| `Password=...` | Mật khẩu của `chess_auth` |
| `SslMode=None` | Mẫu dùng cho kết nối DB cục bộ qua loopback; không áp dụng nguyên dạng cho DB trên máy khác |

Chọn **Kiểm tra MySQL**.

**Kết quả cần thấy:** `MySQL: schema auth sẵn sàng`. Nếu lỗi, đọc thông báo ở tab **Vận hành**. Nút này kiểm tra mở kết nối và đọc cấu trúc bảng; chưa kiểm tra quyền ghi. Kết nối giữa game và server vẫn dùng TLS dù ví dụ MySQL cục bộ đặt `SslMode=None`.

## Bước 6 — Hiểu và tạo PFX ngay trên UI

### 6.1. Vì sao cần PFX?

Database lưu tài khoản. PFX phục vụ **kết nối bảo mật TLS giữa client và server**. Đây là hai phần độc lập.

Tệp `.pfx` chứa chứng chỉ server và khóa riêng. Mật khẩu PFX bảo vệ nội dung tệp khi lưu trên máy. Client kiểm tra **pin**, tức dấu vân tay SHA-256 của chứng chỉ, để nhận diện đúng server.

Bạn không cần mua hoặc tải PFX cho lần thử này. ServerManager có nút tạo chứng chỉ tự ký, dùng được với cơ chế pinning hiện có; không cần cài chứng chỉ vào Windows Trusted Root.

### 6.2. Lần đầu chưa có PFX

1. Trên máy A, mở File Explorer, vào thư mục **Documents** và tạo thư mục **ChessServer** để lưu chứng chỉ.
2. Trở lại tab **Cấu hình** của ServerManager.
3. Nhập một mật khẩu bạn tự đặt vào ô **Mật khẩu PFX**. Dùng mật khẩu riêng cho tệp này và ghi nhớ để mở lại lần sau.
4. Bấm **Tạo PFX mới…**. Phải nhập mật khẩu trước khi bấm nút.
5. Trong hộp lưu, chọn thư mục `Documents\ChessServer`, đặt tên `server.pfx`, rồi bấm **Save**. Chọn tên chưa tồn tại vì chương trình không ghi đè chứng chỉ cũ.
6. Sau khi tạo thành công, ô **Chứng chỉ PFX** tự có đường dẫn và ô **SHA-256 certificate pin** có chuỗi 64 ký tự.
7. Bấm **Sao chép pin**. Giữ chuỗi này để nhập cho client ở bước 8.

**Kết quả cần thấy:** có tệp `server.pfx` trong thư mục đã chọn và UI hiển thị pin. Đường dẫn PFX là một tệp trên máy, không phải URL hoặc khóa lấy từ website.

### 6.3. Những lần sau đã có PFX

Chọn **Chọn PFX…**, mở lại tệp `server.pfx`, nhập đúng mật khẩu đã dùng để tạo tệp, rồi khởi động. Pin sẽ hiển thị sau khi khởi động thành công. Không cần tạo PFX mới mỗi lần chạy.

| Thông tin | Đặt ở đâu? |
|---|---|
| Tệp `server.pfx` và khóa riêng bên trong | Giữ trên máy A |
| Mật khẩu PFX | Nhập vào ServerManager để mở tệp |
| Pin 64 ký tự | Cấp cho máy client qua kênh tin cậy để đặt `CHESS_CERT_PIN` |

Chứng chỉ do UI tạo có hiệu lực một năm. Khi đổi chứng chỉ, pin thay đổi và client phải cập nhật; khi chỉ đổi IP máy A, không cần tạo lại PFX.

## Bước 7 — Khởi động server và đọc trạng thái

Trong ServerManager, chọn **Khởi động**. UI kiểm tra MySQL/schema auth và PFX trước khi mở gateway cùng hai worker.

**Kết quả cần thấy:**

- Trạng thái **ĐANG CHẠY**.
- Tab **Vận hành** hiển thị gateway `127.0.0.1:8888` và worker `127.0.0.1:9001`, `127.0.0.1:9002`.
- Nhật ký có thông báo server đã khởi động.

Giữ cửa sổ mở khi chạy client. Muốn sửa cấu hình, chọn **Dừng server** trước. Đóng cửa sổ sẽ dừng listener và các kết nối đang xử lý. Có thể khởi động lại trong cùng cửa sổ; không chạy thêm server console trên cùng bộ cổng.

Nếu thất bại, xem nhật ký: chứng chỉ sai mật khẩu, DB chưa kết nối được hoặc cổng bị chiếm đều có thể làm server không khởi động. Nếu một cổng lỗi, các listener đã mở trong lần khởi động đó được đóng lại.

## Bước 8 — Đăng ký và đăng nhập ngay trên máy A

**Mở cửa sổ Windows PowerShell thứ hai trên máy A**, để nguyên server đang chạy. Thực hiện:

```powershell
Set-Location Q:\DoAnNT106
$env:CHESS_AUTH_ENDPOINT = '127.0.0.1:8888'
$env:CHESS_CERT_PIN = Read-Host 'Dan pin SHA-256 tu ServerManager roi nhan Enter'
dotnet run --project Design/OnlineChessGame.csproj
```

Khi dòng `Read-Host` chờ nhập, dán **chỉ chuỗi pin 64 ký tự** từ UI, không dán nhãn `CHESS_CERT_PIN=` hoặc mật khẩu PFX. Đây là một dòng nhập tương tác, không phải lệnh bị treo.

Sau khi client mở:

1. Kiểm tra ô địa chỉ server là `127.0.0.1:8888`.
2. Chọn **Đăng ký**, nhập tên đăng nhập chưa có, tên hiển thị, mật khẩu 8–128 ký tự và xác nhận giống nhau.
3. Sau thông báo tạo tài khoản thành công, đăng nhập bằng tài khoản người chơi vừa tạo.
4. Kiểm tra đã vào sảnh và server ghi nhận yêu cầu đăng ký/đăng nhập.

**Kết quả cần thấy:** đăng ký thành công, đăng nhập vào sảnh; tài khoản mới có Elo mặc định 1200. Nếu chưa đạt, xử lý lỗi ngay trên máy A trước khi chuyển sang LAN.

`$env:...` chỉ đặt biến trong PowerShell hiện tại và tiến trình con. Luôn mở client bằng lệnh từ cửa sổ đã đặt endpoint/pin; mở bằng double-click hoặc từ Visual Studio đang chạy trước đó có thể không nhận các biến vừa đặt.

## Bước 9 — Mở server ra LAN và chạy client máy B/C

### 9.1. Lấy IP máy A

Trên máy A, chạy `ipconfig` trong PowerShell. Lấy **IPv4 Address** của Wi-Fi/Ethernet đang kết nối chung với B/C, ví dụ `192.168.1.10`. Tránh IP của WSL, Docker hoặc VPN. Thay IP ví dụ trong tất cả lệnh bên dưới bằng IP thật của A.

### 9.2. Đổi địa chỉ bind trên ServerManager

1. Chọn **Dừng server**.
2. Trong tab Cấu hình, đổi **IP gateway** từ `127.0.0.1` sang IP LAN máy A, ví dụ `192.168.1.10`.
3. Giữ gateway `8888`, worker `9001/9002` và chuỗi DB `Server=127.0.0.1` vì MySQL vẫn ở máy A.
4. Giữ nguyên PFX, chọn **Khởi động** lại.

**Kết quả cần thấy:** gateway `192.168.1.10:8888`; worker vẫn `127.0.0.1:9001/9002`. Máy A chỉ thử client qua IP LAN khi gateway đã bind riêng IP LAN.

### 9.3. Cho phép kết nối qua Windows Firewall

Trên mạng nội bộ tin cậy, vào Settings của kết nối Wi-Fi/Ethernet máy A, đặt Network profile thành **Private**. Mở **Windows PowerShell bằng Run as administrator** và chạy một lần:

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

Chỉ cần mở cổng gateway `8888` cho client. Không cần mở MySQL `3306` hoặc worker `9001/9002` ra LAN trong mô hình này. Nếu chọn cổng gateway khác, sửa rule và endpoint client tương ứng.

### 9.4. Kiểm tra từ máy B/C

Máy B/C kết nối cùng mạng có thể liên lạc với A. Trong **Windows PowerShell trên B/C**:

```powershell
Test-NetConnection 192.168.1.10 -Port 8888
```

**Kết quả cần thấy:** `TcpTestSucceeded : True`. Nếu `False`, kiểm tra IP, server đã chạy, firewall và việc Wi-Fi khách có chặn liên lạc giữa các thiết bị hay không.

### 9.5. Khởi chạy client trên B/C

Nếu chạy từ source, máy B/C cần .NET SDK Windows phù hợp và cùng phiên bản mã nguồn. Không cần MySQL/Workbench trên B/C để đăng ký/đăng nhập.

```powershell
# Thay bằng thư mục source thực tế trên máy B/C.
Set-Location C:\Projects\DoAnNT106
$env:CHESS_AUTH_ENDPOINT = '192.168.1.10:8888'
$env:CHESS_CERT_PIN = Read-Host 'Dan pin SHA-256 tu ServerManager may A'
dotnet run --project Design/OnlineChessGame.csproj
```

Kiểm tra ô server trong màn hình đăng nhập là `192.168.1.10:8888` trước khi chuyển sang Đăng ký. Trên B/C, `127.0.0.1` và `localhost` trỏ về chính máy client; phải nhập IP máy A. `0.0.0.0` có thể dùng làm địa chỉ bind server nhưng không dùng làm địa chỉ đích client.

Pin của B và C phải giống pin chứng chỉ server A. Nếu A đổi IP do DHCP hoặc chuyển Wi-Fi, cập nhật IP bind và endpoint trên client; PFX có thể giữ nguyên.

## Bước 10 — Xác nhận dữ liệu đã lưu

Trong Workbench máy A, mở kết nối quản trị và chạy:

```sql
SELECT user_id, username, display_name, created_at
FROM onlinechessdb.Users
ORDER BY user_id DESC;

SELECT u.username, s.game_type, s.elo, s.wins, s.losses, s.draws
FROM onlinechessdb.Users AS u
JOIN onlinechessdb.UserGameStats AS s ON s.user_id = u.user_id
ORDER BY u.user_id DESC, s.game_type;
```

Mỗi tài khoản mới có một dòng trong `Users` và hai dòng thống kê `CHESS`/`CARO`, Elo ban đầu 1200. Thử thêm đăng ký trùng tên và đăng nhập sai mật khẩu để xác nhận server từ chối đúng trường hợp.

Kết nối đang xử lý trên UI có thể trở về 0 ngay khi đăng nhập xong: mỗi yêu cầu auth dùng một kết nối ngắn. Đây chưa phải số người chơi online. Số yêu cầu của cả hai worker sẽ tăng khi gửi nhiều yêu cầu lần lượt.

## Những lần chạy sau cần làm gì?

Không cần tạo lại database, tài khoản DB hoặc PFX. Các dữ liệu đó đã nằm trên máy A.

1. Kiểm tra dịch vụ MySQL đang chạy.
2. Mở ServerManager; nhập lại cấu hình nếu không nạp bằng biến môi trường. UI chưa tự lưu cấu hình/mật khẩu vào tệp.
3. Chọn PFX cũ và nhập mật khẩu đúng, kiểm tra MySQL rồi khởi động.
4. Trên client, đặt endpoint và pin trong PowerShell trước khi mở game.

## Khi gặp lỗi, kiểm tra ở đâu?

| Lỗi/hiện tượng | Cách kiểm tra |
|---|---|
| Không biết chạy SQL ở đâu | Mở kết nối MySQL Workbench và dán vào tab SQL; không dán SQL vào PowerShell |
| Chỉ cài SSMS/SQL Server | Cần MySQL cho mã nguồn hiện tại; không chạy script MySQL trong SSMS |
| `host/fxr` không tồn tại | Đang gọi .NET Linux trong WSL; chuyển PowerShell Windows hoặc gọi `dotnet.exe` |
| `dotnet run` không tìm thấy project | Chuyển về thư mục source có `Design`/`ServerManager`; sửa đường dẫn `Set-Location` |
| Cổng MySQL 3306 chưa kết nối | Kiểm tra đã cài MySQL Server, dịch vụ đang chạy và dùng đúng cổng |
| `Access denied` | Kiểm tra mật khẩu, user và host qua Workbench; không mặc định lỗi này chỉ do host |
| `CREATE USER` báo đã tồn tại | Không tạo lặp lại; kiểm tra user/quyền hoặc quản lý mật khẩu tài khoản đã có |
| Không thấy bảng hoặc UI báo schema chưa sẵn sàng | Chạy đầy đủ script, kiểm tra Database=onlinechessdb và quyền SELECT hai bảng auth |
| MySQL test đạt nhưng đăng ký lỗi | Kiểm tra quyền INSERT cả Users và UserGameStats; nút kiểm tra MySQL chỉ đọc schema |
| Chưa biết lấy PFX ở đâu | Nhập Mật khẩu PFX rồi bấm Tạo PFX mới… trong tab Cấu hình |
| Tạo PFX báo lỗi ghi tệp | Chọn thư mục có quyền ghi và tên chưa tồn tại; chương trình không ghi đè PFX |
| Không nạp được PFX | Kiểm tra đã chọn đúng tệp và nhập đúng mật khẩu dùng lúc tạo |
| Không mở được cổng server | IP bind không thuộc máy A, cổng bị chiếm hoặc console/UI khác đang dùng cổng |
| Client LAN không tới cổng 8888 | Kiểm tra server bind IP LAN, firewall đúng profile Private và mạng cho thiết bị liên lạc |
| TCP đạt nhưng báo không kết nối TLS | Kiểm tra pin đúng chứng chỉ, đủ 64 hex; mở lại client từ PowerShell đã đặt biến |
| Tài khoản cũ trả `reset_required` | Mật khẩu cũ dạng rõ; thử tài khoản mới, chưa có quy trình reset tích hợp |
| Hai máy vào sảnh nhưng tìm trận không đồng bộ | Server gameplay còn thiếu; cài DB/LAN không tự hoàn thiện chức năng này |

Nếu đã kiểm tra đúng mật khẩu/quyền nhưng kết nối cục bộ vẫn không khớp host, quản trị viên có thể kiểm tra tài khoản bằng kết nối `root`:

```sql
SELECT User, Host FROM mysql.user WHERE User = 'chess_auth';
```

Nếu cần tài khoản cho host `localhost` và tài khoản đó chưa tồn tại, tạo với cùng mật khẩu DB rồi cấp quyền tương ứng:

```sql
CREATE USER 'chess_auth'@'localhost'
IDENTIFIED BY 'THAY_BANG_MAT_KHAU_DB_CUA_BAN';
GRANT SELECT, INSERT ON onlinechessdb.Users TO 'chess_auth'@'localhost';
GRANT SELECT, INSERT ON onlinechessdb.UserGameStats TO 'chess_auth'@'localhost';
```

Hướng dẫn mở rộng, gồm publish client tự chứa runtime cho máy không cài SDK: [HUONG_DAN_CHAY_LAN.md](docs/HUONG_DAN_CHAY_LAN.md).

## Chức năng hiện có

| Phần | Đã triển khai | Giới hạn hiện tại |
|---|---|---|
| Tài khoản | Đăng ký, đăng nhập qua TCP/TLS; lưu tài khoản và đọc hồ sơ/Elo từ MySQL | Chưa có token/session, thu hồi phiên hoặc quy trình quên mật khẩu |
| Gateway và worker | Round-robin theo kết nối, gateway chuyển tiếp TLS tới hai worker loopback; fallback nếu chưa kết nối TCP được tới worker | Ba listener cùng một tiến trình; chưa có health check định kỳ hoặc gateway dự phòng |
| Quản trị server | WinForms: cấu hình IP/cổng, PFX và MySQL; tạo PFX; kiểm tra schema auth; khởi động/dừng; sao chép certificate pin; bộ đếm và nhật ký | Quản lý instance do chính cửa sổ khởi chạy; chưa có điều khiển server console từ xa hoặc CRUD tài khoản/phòng/trận |
| Client | Đăng nhập, đăng ký, sảnh, bàn cờ vua/caro; giao diện sáng/tối; không giữ thông tin DB | Gameplay online chưa có session và transport riêng |
| Cờ vua cục bộ | Luật hợp lệ đầy đủ bằng Gera.Chess; chiếu/chiếu bí, nhập thành, en passant, chọn quân phong cấp, các luật hòa; Stockfish 4 mức khó | Stockfish phải được cài một lần bằng script; gameplay Rank/LAN vẫn đang phát triển |
| Caro cục bộ | Bàn 15×15, kiểm tra từ 5 quân liên tiếp theo bốn hướng, bot đơn giản và đồng hồ | Chưa có xác nhận luật/kết quả trên server |
| Hàng chờ và trận | Có nút UI và schema dự kiến | Nút Rank hiện báo đang phát triển; chưa có hàng chờ hoặc trận được server quản lý |
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
Chess/                     Luật cờ vua chuẩn, cấu hình độ khó và UCI Stockfish
Engines/Stockfish/         Binary local, giấy phép và hướng dẫn cài engine
scripts/setup-stockfish.ps1 Tải Stockfish Windows x64 chính thức
Server/                    Chương trình server console
ServerManager/             Giao diện vận hành/quản trị server WinForms
Network/                   TCP/TLS, AuthClient, AuthCluster, hợp đồng JSON
Auth/                      MySQL, băm mật khẩu, admission
onlinedb/game_auth.sql      Schema onlinechessdb
Tests/AuthTests/           Test nghiệp vụ xác thực, hash, admission và MySQL
Tests/TransportTests/      Test transport, TLS, phân tải, shutdown và bộ đếm kết nối
Tests/ServerManagerTests/  Test bộ đếm/nhật ký của giao diện server
Tests/ChessTests/          Test luật cờ và bắt tay Stockfish UCI
Tests/test_*.py            Kiểm tra cấu trúc nối UI và entrypoint
BaoCao/                    Báo cáo Word đã tạo
AUTH-SERVER.md             Tài liệu kỹ thuật nền: auth, LAN và các phần cần hoàn thiện
```

`Design/OnlineChessGame.slnx` vẫn là solution riêng cho client. Solution `.slnx` cần Visual Studio hỗ trợ định dạng này (Visual Studio 2022 17.12+); có thể mở từng `.csproj` hoặc dùng CLI. README gốc này mô tả cả phần ServerManager mới; một số tài liệu thành phần có thể mô tả phạm vi trước khi bổ sung UI.

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
| `STOCKFISH_PATH` | Client | Tùy chọn: đường dẫn đầy đủ tới `stockfish.exe` nếu không dùng thư mục Engines |
| `AUTH_TEST_MYSQL` | Test | Instance MySQL dùng một lần cho test tích hợp |

Cổng của UI được chọn trực tiếp trên form. Server console vẫn dùng cố định 8888/9001/9002. Không có biến môi trường đổi cổng console.

## Chạy server console thay cho UI (tùy chọn)

Nếu mới thiết lập, dùng ServerManager theo các bước trên. Console dùng cùng thư viện auth nhưng đọc cấu hình từ biến môi trường; không tự lấy giá trị đang nhập trên UI. Với PFX và tài khoản DB đã có, mở Windows PowerShell tại repository:

```powershell
$env:CHESS_BIND_ADDRESS = '127.0.0.1'  # Đổi sang IP LAN thật để nhận client khác máy.
$env:CHESS_CERT_PATH = Read-Host 'Nhap duong dan day du den PFX da tao'
$pfxSecret = Read-Host 'Nhap mat khau PFX' -AsSecureString
$env:CHESS_CERT_PASSWORD = [System.Net.NetworkCredential]::new('', $pfxSecret).Password
Remove-Variable pfxSecret
$dbCredential = Get-Credential -UserName 'chess_auth'
$dbBuilder = [System.Data.Common.DbConnectionStringBuilder]::new()
$dbBuilder['Server'] = '127.0.0.1'
$dbBuilder['Port'] = 3306
$dbBuilder['Database'] = 'onlinechessdb'
$dbBuilder['User ID'] = $dbCredential.UserName
$dbBuilder['Password'] = $dbCredential.GetNetworkCredential().Password
$dbBuilder['SslMode'] = 'None'
$env:CHESS_DB_CONNECTION = $dbBuilder.ConnectionString
Remove-Variable dbBuilder, dbCredential
$env:AUTH_ALLOW_LEGACY_MIGRATION = 'false'
dotnet run --project Server/OnlineChessGame.Server.csproj
```

Dừng bằng `Ctrl+C`. Console dùng cổng cố định 8888/9001/9002, không chạy cùng UI trên bộ cổng này. Dòng `Auth ready` của console cho biết listener đã mở; console chưa kiểm tra DB trước khi nhận request như UI. Máy client vẫn cần endpoint và pin đúng như hướng dẫn chính.

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

Kiểm tra gần nhất ngày 19/11/2026: client build đạt, 0 lỗi và 2 cảnh báo event chưa dùng; Python: 8 đạt, gồm kiểm tra client không còn provider/credential DB và Rank không mở hàng chờ giả. Đợt kiểm tra ServerManager trước đó ghi nhận transport 27 đạt; auth 24 đạt, 5 test DB bỏ qua; giám sát 4 đạt. Kiểm tra trực tiếp WinForms đã xác nhận mở hai tab, báo IP sai/cổng trùng, khôi phục trạng thái nút sau lỗi và đóng cửa sổ sạch. Chưa nghiệm thu gameplay online hoặc LAN ba máy đầy đủ.

## Những phần cần hoàn thiện

1. Session/token, thời hạn và thu hồi phiên; lấy danh tính từ phiên cho mọi thao tác sau đăng nhập.
2. Dịch vụ phòng/ghép trận trên server; hai client cùng match ID, phân màu và trạng thái ready.
3. Bộ luật đầy đủ và trạng thái bàn cờ/đồng hồ do server xác nhận.
4. Chốt trận và cập nhật Elo/lịch sử một lần, thống nhất cả hai trò chơi.
5. Chat, heartbeat, reconnect và đồng bộ snapshot khi mất mạng.
6. Xây dựng API server cho hàng chờ, lịch sử và xếp hạng; client không truy cập DB trực tiếp.
7. Mở rộng UI quản trị với người chơi, phòng và trận khi các dịch vụ tương ứng đã có; tách gateway/worker thành tiến trình riêng nếu cần khả năng triển khai độc lập.

Tài liệu chi tiết về auth và các giới hạn nền: [AUTH-SERVER.md](AUTH-SERVER.md). Repository tham khảo: [CS-Winform-Project-nd-Lab](https://github.com/thnhngaan/CS-Winform-Project-nd-Lab); dự án hiện tại giữ client WinForms và MySQL, không chuyển sang Unity/SQL Server.
