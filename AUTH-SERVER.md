---
meta:
  contentType: Reference
---

# Kiến trúc server hiện tại, cách chạy LAN và các phần còn thiếu

Tài liệu này mô tả phần đã triển khai của đồ án cờ vua/caro, tập trung vào gateway, hai worker và xác thực. Mục tiêu là giúp nhóm phân biệt chức năng chạy được với phần mới có UI hoặc schema, cấu hình thử nghiệm LAN và xác định thứ tự hoàn thiện. Đây là bản tham chiếu theo mã nguồn hiện tại, không phải cam kết toàn bộ game online đã hoàn tất.

## 1. Phạm vi và công nghệ

Đồ án giữ giao diện C# WinForms và cơ sở dữ liệu MySQL hiện có. Phần auth mới áp dụng mô hình client gửi yêu cầu, server xử lý và truy vấn DB từ [project tham khảo CS-Winform-Project-nd-Lab](https://github.com/thnhngaan/CS-Winform-Project-nd-Lab).

| Thành phần | Công nghệ và vai trò |
|---|---|
| Client | WinForms, target .NET 8 Windows; hiển thị đăng nhập, đăng ký, sảnh, bàn cờ vua và caro |
| Transport | TCP, SslStream, TLS 1.2/1.3; JSON có tiền tố độ dài |
| Gateway | Chuyển tiếp TCP, cân bằng tải round-robin theo kết nối |
| Worker | Hai listener xử lý xác thực, giải mã TLS tại worker |
| Auth service | MySqlConnector, truy vấn tham số hóa, transaction đăng ký |
| Lưu trữ mật khẩu | PBKDF2-HMAC-SHA256 có salt ngẫu nhiên |
| Database | MySQL; đã kiểm thử tích hợp bằng MariaDB tạm |

Không chuyển ứng dụng sang Unity hoặc SQL Server theo project mẫu. SQLite, UDP và các dịch vụ gameplay trong định hướng kiến trúc chưa được triển khai bởi phần auth này.

## 2. Sơ đồ thực tế đang chạy

Gateway và hai worker hiện nằm trong cùng một tiến trình, không phải ba dịch vụ độc lập.

```text
Máy B: WinForms ──┐
                  ├── TCP/TLS ──► Máy A: tiến trình OnlineChessGame.Server
Máy C: WinForms ──┘                  │
                                     ├── Gateway IP_LAN:8888
                                     │      │ round-robin từng kết nối
                                     │      ├── Worker 1: 127.0.0.1:9001
                                     │      └── Worker 2: 127.0.0.1:9002
                                     │                 │
                                     │     AuthAdmissionService dùng chung
                                     │                 │
                                     │        MySqlAuthService
                                     │                 │
                                     └────────────── MySQL
```

- **Đã có load balancer cơ bản**: gateway luân phiên chọn worker cho từng kết nối, không phải từng gói JSON
- **TLS pass-through**: gateway chuyển byte mã hóa, chỉ worker giải mã; gateway không đọc username hoặc password
- **Worker chỉ bind loopback**: thay IP gateway không làm worker công bố cổng ra LAN
- **Một điểm lỗi chung**: tiến trình server chết thì gateway và cả hai worker cùng dừng
- **DB dùng chung**: hai worker không có hai bản database riêng

### 2.1. Cổng và địa chỉ

| Điểm kết nối | Mặc định | Client LAN có truy cập không? |
|---|---|---|
| Gateway | 127.0.0.1:8888; đổi địa chỉ bằng CHESS_BIND_ADDRESS | Có, sau khi bind IP LAN và mở firewall |
| Worker 1 | 127.0.0.1:9001 | Không |
| Worker 2 | 127.0.0.1:9002 | Không |
| MySQL | Theo CHESS_DB_CONNECTION | Không cần mở cho client để xác thực |

API AuthCluster cho phép chọn cổng khi khởi tạo và dùng cổng 0 trong test. Entrypoint hiện tại dùng cố định 8888/9001/9002, chưa có biến môi trường đổi cổng.

### 2.2. Cân bằng tải và chuyển worker khi lỗi

Gateway thử kết nối tới worker được chọn. Nếu TCP connect ném SocketException, gateway thử worker còn lại trước khi chuyển dữ liệu.

Giới hạn hiện tại:

- Không có health check định kỳ hoặc danh sách worker động
- Không kiểm tra trạng thái DB trước khi chọn worker
- Không phân phối theo CPU, số yêu cầu đang xử lý hoặc độ trễ
- Không tự chuyển worker sau khi đã chuyển dữ liệu TLS
- Không phát lại yêu cầu signup/signin nếu mất phản hồi
- Không có gateway dự phòng hoặc tự khởi động lại tiến trình

Nếu DB đã commit đăng ký nhưng phản hồi bị mất, client có thể nhận lỗi mạng dù tài khoản đã tồn tại. Thử đăng nhập trước khi kết luận đăng ký thất bại; chưa có khóa idempotency để xử lý kết quả không chắc chắn.

## 3. Cấu trúc mã nguồn liên quan

Các thư mục tách giao thức, nghiệp vụ và chương trình chạy server.

| Đường dẫn | Chức năng |
|---|---|
| Network/Contracts.cs | AuthRequest, AuthResponse, PlayerProfile, IAuthService |
| Network/Transport.cs | Đóng khung JSON, AuthClient, TLS và pin chứng chỉ |
| Network/AuthCluster.cs | Gateway, hai TLS listener, vòng nhận kết nối, shutdown |
| Auth/PasswordHasher.cs | Tạo và kiểm tra PBKDF2 |
| Auth/MySqlAuthService.cs | Đăng ký, đăng nhập, đọc Elo, migration mật khẩu cũ |
| Auth/AuthAdmissionService.cs | Giới hạn đồng thời và tốc độ toàn cục |
| Server/Program.cs | Đọc cấu hình, tạo chứng chỉ, chạy cluster, Ctrl+C |
| Design/AuthConnection.cs | Endpoint, pin và profile trong bộ nhớ client |
| Design/LoginForm.cs | Gửi signin bất đồng bộ |
| Design/RegisterForm.cs | Gửi signup bất đồng bộ |
| onlinedb/game_auth.sql | Schema MySQL của đồ án |
| Tests/TransportTests/ | Kiểm thử TCP/TLS, pin, binding, phản hồi và shutdown |
| Tests/AuthTests/ | Kiểm thử hashing, admission, nghiệp vụ và DB tạm |
| Tests/test_*.py | Kiểm tra bổ sung quy ước nối mã nguồn UI/entrypoint |

## 4. Giao thức xác thực

Mỗi lần gọi AuthClient.SendAsync mở một kết nối, gửi một yêu cầu, nhận một phản hồi rồi đóng. Chưa có kết nối phiên dài hạn cho gameplay.

### 4.1. Đóng khung dữ liệu

Mỗi thông điệp có cấu trúc:

```text
[4 byte độ dài big-endian][N byte JSON UTF-8]
```

- Độ dài hợp lệ: 1 đến 16.384 byte, không tính header
- ReadExactlyAsync đọc đủ header và payload; không giả định một lần TCP read là một gói
- JSON sai định dạng được chuyển thành InvalidDataException
- JSON null và độ dài không hợp lệ bị từ chối
- Client kiểm tra Code/Message khác null và yêu cầu Player khi Success=true
- JSON dùng tên thuộc tính mặc định của record .NET, như Action, Username, Password; chưa có trường phiên bản giao thức

### 4.2. Hợp đồng yêu cầu và phản hồi

| Kiểu | Các trường |
|---|---|
| AuthRequest | Action, Username, Password, DisplayName tùy chọn |
| AuthResponse | Success, Code, Message, Player tùy chọn |
| PlayerProfile | UserId, Username, DisplayName, ChessElo, CaroElo |

Action hợp lệ là `signin` và `signup`, phân biệt hoa/thường. Không có lệnh create_room, join_room, move, chat hoặc logout qua giao thức auth hiện tại.

### 4.3. Kiểm tra dữ liệu phía server

Server kiểm tra lại dữ liệu dù UI đã kiểm tra trước.

| Trường | Quy tắc hiện tại |
|---|---|
| Username | Trim đầu/cuối; sau trim từ 1 đến 50 ký tự; chuỗi đầu vào tối đa 256 ký tự |
| Password đăng ký | Từ 8 đến 128 ký tự; không trim hoặc chuẩn hóa |
| Password đăng nhập | Từ 1 đến 128 ký tự để hỗ trợ kiểm tra tài khoản cũ |
| DisplayName | Tối đa 100 ký tự đầu vào; trim; thiếu/rỗng thì service dùng username |
| Action | Chỉ signin hoặc signup |

UI đăng ký yêu cầu tên hiển thị không rỗng, dù service cho phép dùng username thay thế. Username trùng phụ thuộc unique index và collation MySQL; chưa có chính sách chuẩn hóa chữ hoa, dấu và Unicode độc lập với DB.

### 4.4. Mã phản hồi

Các mã hiện tại chưa hoàn toàn thống nhất chữ hoa/thường.

| Code | Ý nghĩa |
|---|---|
| ok | Đăng ký hoặc đăng nhập thành công |
| invalid_request | Auth service từ chối action hoặc dữ liệu không hợp lệ |
| INVALID_REQUEST | Worker phát hiện thiếu action, username hoặc password |
| invalid_credentials | Không có tài khoản hoặc sai mật khẩu; dùng cùng thông báo |
| username_taken | Trùng username khi đăng ký |
| reset_required | Mật khẩu cũ đúng nhưng chưa được phép migration |
| service_unavailable | Lỗi MySQL đã được chuyển thành thông báo không chứa chi tiết DB |
| service_busy | Vượt giới hạn admission dùng chung |

Lỗi TCP/TLS, timeout hoặc JSON có thể là exception phía client, không phải AuthResponse. Một số thông báo service còn bằng tiếng Anh; cần bản địa hóa và thống nhất mã lỗi trước khi chốt giao thức.

## 5. Luồng đăng ký

Server chỉ báo thành công sau khi transaction hoàn tất.

1. UI lấy username/display name, giữ nguyên password và kiểm tra xác nhận mật khẩu.
2. UI khóa nút gửi/chuyển màn hình trong lúc chờ.
3. AuthClient kết nối gateway và bắt tay TLS với worker qua gateway.
4. Worker nhận một AuthRequest có Action=signup.
5. Admission kiểm tra giới hạn dùng chung trước khi chạy nghiệp vụ.
6. Service kiểm tra dữ liệu và tạo hash mật khẩu.
7. Transaction thêm Users và hai UserGameStats cho CHESS/CARO.
8. Service commit rồi trả profile với Elo khởi tạo 1200 cho mỗi game.
9. UI xóa ô mật khẩu và chuyển về màn hình đăng nhập khi thành công.

Unique index xử lý cả đăng ký trùng đồng thời. Nếu thêm thống kê thất bại trước commit, transaction không để lại tài khoản tạo dở. Hành vi đã được kiểm thử bằng DB tạm.

## 6. Luồng đăng nhập và trạng thái UI

Đăng nhập đọc profile từ DB, không còn gán cố định Elo trong luồng auth.

1. UI gửi Action=signin qua TLS.
2. Service tìm Users và LEFT JOIN thống kê CHESS/CARO.
3. Nếu không tìm thấy tài khoản, service vẫn thực hiện kiểm tra hash giả để trả cùng dạng lỗi đăng nhập.
4. Nếu có hash PBKDF2 hợp lệ, service xác minh bằng phép so sánh thời gian cố định.
5. Service trả UserId, Username, DisplayName và hai Elo; thiếu hàng thống kê thì fallback Elo 1200.
6. UI giữ profile trong AuthConnection.Player rồi mở Dashboard.

Các giới hạn UI cần lưu ý:

- Dashboard hiện được mở với username và ChessElo; việc có CaroElo trong profile không đồng nghĩa mọi màn hình đã sử dụng đúng chỉ số đó
- Profile trong bộ nhớ không phải session/token và không chứng minh quyền hạn cho yêu cầu gameplay
- Checkbox ghi nhớ mật khẩu bị vô hiệu hóa; không lưu mật khẩu xuống máy
- Dispose form hủy yêu cầu đang chờ
- Có bắt lỗi mạng/TLS/cấu hình và hiển thị thông báo chung; chưa tự động kiểm thử thao tác GUI thực tế

## 7. Bảo mật và giới hạn tài nguyên

TLS bảo vệ dữ liệu khi truyền; PBKDF2 bảo vệ mật khẩu khi lưu. Hai cơ chế có mục đích khác nhau.

### 7.1. Chứng chỉ và pin

Lệnh tạo chứng chỉ dùng RSA 3072 bit, SHA-256, chứng chỉ self-signed cho server và thời hạn một năm. File PFX chứa private key, có mật khẩu bảo vệ và phải giữ trên server.

Client so sánh SHA-256 của toàn bộ DER certificate bằng phép so sánh thời gian cố định. Pin phải được cấp qua kênh tin cậy, không lấy tự động từ endpoint chưa xác thực.

Callback hiện dùng exact pin thay cho kiểm tra CA/hostname/expiry thông thường. Entrypoint server kiểm tra thời hạn chứng chỉ lúc khởi động, nhưng client không có kiểm tra expiry riêng trong callback. Khi thay chứng chỉ phải cập nhật pin client; chưa có cơ chế chấp nhận đồng thời pin hiện tại và pin kế tiếp.

### 7.2. Băm mật khẩu

PasswordHasher dùng các tham số cố định:

- PBKDF2-HMAC-SHA256 với 600.000 vòng
- Salt ngẫu nhiên 16 byte cho từng lần tạo hash
- Khóa dẫn xuất 32 byte
- Định dạng có phiên bản: pbkdf2-sha256$v1$600000$...
- Xác minh bằng CryptographicOperations.FixedTimeEquals

Đây không phải mã hóa có thể giải ngược. Chưa có chính sách nâng tham số/rehash tự động khi đăng nhập thành công.

### 7.3. Giới hạn tải

| Giới hạn | Giá trị hiện tại |
|---|---|
| Kết nối được xử lý đồng thời | 128 cho từng listener, bộ đếm riêng |
| Thời hạn phía client | 10 giây cho một lần SendAsync |
| Thời hạn gateway/worker | 10 giây cho mỗi kết nối của listener tương ứng |
| Nghiệp vụ auth đồng thời | Tối đa 4, dùng chung cho hai worker |
| Hàng chờ admission | Không xếp hàng; từ chối ngay khi quá tải |
| Token bucket toàn cục | Burst 20, nạp 5 yêu cầu/giây |

Admission nằm trước nghiệp vụ để giới hạn cả băm đồng bộ. Timeout không ngắt ngay phép PBKDF2 đồng bộ đang chạy; giới hạn đồng thời giúp chặn số công việc đó. Các giới hạn này không thay thế chống DDoS, hạn mức từng IP/tài khoản hoặc chính sách chống đoán mật khẩu.

## 8. Database và ranh giới truy cập

Auth dùng trực tiếp hai bảng Users và UserGameStats. Schema đồ án còn có Rooms, RoomMembers, Matches, MatchPlayers, RoomMessages và MatchmakingQueue.

Việc tồn tại các bảng không chứng minh đã có server quản lý phòng hoặc trận đấu. Truy cập MySQL trực tiếp từng tồn tại trong Dashboard/ChessBoard đã được loại khỏi client; nút Rank hiện thông báo chưa có server gameplay thay vì tạo một hàng chờ không đồng bộ.

Yêu cầu triển khai:

- Chỉ cấp tài khoản DB riêng, quyền tối thiểu cho server auth
- Không đặt DB credentials trong client mới hoặc tài liệu công khai
- Nếu DB qua mạng, cấu hình TLS MySQL có xác thực chứng chỉ
- Chuyển dần truy vấn gameplay cũ sang server rồi thu hồi quyền truy cập DB của client
- Kiểm tra và thay thông tin DB từng được hardcode trong mã nguồn, không chỉ xóa khỏi bản mới
- Sao lưu trước khi đổi schema hoặc chuyển dữ liệu mật khẩu

## 9. Cấu hình và chạy thử LAN trên ba máy

Cấu hình khuyến nghị là máy A chạy server/MySQL, máy B và C chạy client. Các lệnh dưới dùng PowerShell trên Windows; không gửi bí mật qua chat hoặc commit vào Git.

### 9.1. Biến môi trường

| Biến | Nơi đặt | Bắt buộc và ý nghĩa |
|---|---|---|
| CHESS_DB_CONNECTION | Server | Bắt buộc khi chạy server; chuỗi MySQL |
| CHESS_CERT_PATH | Server | Bắt buộc; đường dẫn PFX |
| CHESS_CERT_PASSWORD | Server | Bắt buộc, không rỗng; mật khẩu PFX |
| CHESS_BIND_ADDRESS | Server | Tùy chọn; mặc định 127.0.0.1; chỉ đổi gateway |
| AUTH_ALLOW_LEGACY_MIGRATION | Server | Tùy chọn; chỉ true mới bật migration; mặc định tắt |
| CHESS_AUTH_ENDPOINT | Client | Tùy chọn; mặc định 127.0.0.1:8888 |
| CHESS_CERT_PIN | Client | Bắt buộc; 64 ký tự hex SHA-256 DER |
| AUTH_TEST_MYSQL | Tiến trình test | Chỉ DB dùng một lần; không dùng DB hiện hữu |

Biến đặt bằng $env: chỉ có hiệu lực trong PowerShell hiện tại và tiến trình con. Mở cửa sổ mới cần đặt lại. Không ghi toàn bộ biến môi trường ra log.

### 9.2. Chuẩn bị MySQL trên máy A

Cài MySQL, khởi động dịch vụ và kết nối bằng Workbench. Với DB test mới, mở và chạy onlinedb/game_auth.sql; schema được đặt tên onlinechessdb. Không import lại vào DB đang sử dụng nếu chưa sao lưu và kiểm tra.

Kiểm tra schema bằng SQL:

```sql
USE onlinechessdb;
SHOW TABLES;
SHOW COLUMNS FROM Users;
SHOW COLUMNS FROM UserGameStats;
```

Tạo user riêng bằng tài khoản quản trị; thay mật khẩu mẫu khi chạy trong công cụ quản trị và không chia sẻ lịch sử SQL chứa bí mật:

```sql
CREATE USER 'chess_auth'@'127.0.0.1'
IDENTIFIED BY 'mat_khau_db_rieng_can_thay';
GRANT SELECT, INSERT ON onlinechessdb.Users
TO 'chess_auth'@'127.0.0.1';
GRANT SELECT, INSERT ON onlinechessdb.UserGameStats
TO 'chess_auth'@'127.0.0.1';
```

Nếu user đã tồn tại, kiểm tra lại tài khoản hoặc quản lý mật khẩu, không chạy CREATE USER lặp lại. Thử kết nối Workbench bằng chính user này qua 127.0.0.1 và cổng MySQL thực tế trước khi chạy server.

### 9.3. Cấu hình chuỗi kết nối mà không gõ password vào lệnh

Mở PowerShell tại thư mục repo và nhập tài khoản qua hộp nhắc:

```powershell
Set-Location Q:\DoAnNT106
$dbCredential = Get-Credential -UserName 'chess_auth'
$dbBuilder = [System.Data.Common.DbConnectionStringBuilder]::new()
$dbBuilder['Server'] = '127.0.0.1'
$dbBuilder['Port'] = 3306
$dbBuilder['Database'] = 'onlinechessdb'
$dbBuilder['User ID'] = $dbCredential.UserName
$dbBuilder['Password'] = $dbCredential.GetNetworkCredential().Password
$dbBuilder['SslMode'] = 'None'
$env:CHESS_DB_CONNECTION = $dbBuilder.ConnectionString
$dbBuilder.Clear()
Remove-Variable dbCredential, dbBuilder
```

SslMode=None chỉ dùng cho DB test cùng máy qua loopback trong ví dụ này. Không áp dụng cho DB đặt ở máy khác. Biến môi trường chứa mật khẩu trong bộ nhớ; đây chưa phải cơ chế lưu secrets lâu dài.

### 9.4. Tạo PFX một lần

Tạo thư mục ngoài repo và nhập mật khẩu PFX riêng với mật khẩu MySQL:

```powershell
$certDirectory = Join-Path $env:LOCALAPPDATA 'ChessServer'
New-Item -ItemType Directory -Path $certDirectory -Force | Out-Null
$env:CHESS_CERT_PATH = Join-Path $certDirectory 'server.pfx'
$pfxSecret = Read-Host 'Nhap mat khau bao ve PFX' -AsSecureString
$pfxCredential = [System.Net.NetworkCredential]::new('', $pfxSecret)
$env:CHESS_CERT_PASSWORD = $pfxCredential.Password
Remove-Variable pfxSecret, pfxCredential
```

Hạn chế quyền đọc thư mục bằng quyền truy cập Windows. Không đặt PFX ở thư mục chia sẻ, không gửi cho client và lưu mật khẩu trong trình quản lý mật khẩu.

Chạy lệnh tạo chứng chỉ:

```powershell
dotnet run --project Server/OnlineChessGame.Server.csproj -- --create-cert
Test-Path $env:CHESS_CERT_PATH
```

Lệnh in CHESS_CERT_PIN, là pin công khai để cấp cho client. File tồn tại thì lệnh từ chối ghi đè; dùng lại file với đúng password thay vì tạo mới mỗi lần. Không cần cài vào Windows Trusted Root vì client hiện dùng pinning.

### 9.5. Chạy server và mở gateway ra LAN

Dùng ipconfig để lấy IPv4 của card mạng đang hoạt động. Ví dụ máy A có IP 192.168.1.10:

```powershell
$env:CHESS_BIND_ADDRESS = '192.168.1.10'
dotnet run --project Server/OnlineChessGame.Server.csproj
```

Cho phép inbound TCP 8888 trong Windows Firewall, ưu tiên profile Private và giới hạn mạng nội bộ. Không mở worker 9001/9002 hoặc MySQL cho client chỉ để test auth. Ba máy phải cùng mạng, không có chế độ Wi-Fi client isolation.

Dòng Auth ready xác nhận listener đã khởi động, không xác nhận DB đã kết nối vì service mở DB khi xử lý yêu cầu. Giữ cửa sổ mở; Ctrl+C đóng listener và kết nối. Bộ đếm yêu cầu mỗi worker được in lúc dừng, không phải hệ thống giám sát đầy đủ.

### 9.6. Chạy client trên máy B/C

Đặt endpoint máy A và pin được quản trị viên cung cấp qua kênh đáng tin cậy:

```powershell
$env:CHESS_AUTH_ENDPOINT = '192.168.1.10:8888'
$env:CHESS_CERT_PIN = 'pin_sha256_64_ky_tu_hex_can_thay'
dotnet run --project Design/OnlineChessGame.csproj
```

Thay placeholder pin trước khi chạy. Không nhập 127.0.0.1 trên máy B/C vì địa chỉ đó trỏ về chính client. Không chuyển PFX, mật khẩu PFX hoặc chuỗi DB cho hai máy này để thực hiện auth.

### 9.7. Kịch bản chấp nhận LAN

Thực hiện lần lượt để phân biệt lỗi cổng, TLS và DB:

1. Trên B/C chạy Test-NetConnection 192.168.1.10 -Port 8888; kiểm tra TcpTestSucceeded=True.
2. Tạo hai tài khoản thử mới, mỗi máy một tài khoản.
3. Đăng nhập và đối chiếu profile/Elo bằng công cụ quản trị DB, không xuất hash ra log.
4. Thử sai mật khẩu, đăng ký trùng và pin sai; phải bị từ chối mà UI không văng.
5. Dừng server và thử lại để kiểm tra thông báo lỗi mạng.

Bài test này chứng minh auth LAN khi thực hiện thành công, không chứng minh hai client chơi chung được một trận. Chưa thực hiện kiểm thử LAN ba máy thật trong đợt kiểm chứng tự động đã ghi nhận.

## 10. Tài khoản cũ và migration

Mặc định, mật khẩu rõ được service nhận diện là legacy và xác minh đúng sẽ nhận reset_required. UI chưa có quy trình reset bằng email/OTP hoặc màn hình đặt lại mật khẩu.

Giới hạn nhận diện: service chỉ xem giá trị lưu trữ không bắt đầu bằng `pbkdf2-` là mật khẩu rõ. Mật khẩu legacy vô tình bắt đầu bằng tiền tố này sẽ đi vào nhánh kiểm tra hash và nhận invalid_credentials, kể cả khi nhập đúng và bật migration. Hash PBKDF2 sai định dạng, khác phiên bản hoặc tham số cũng không được chuyển đổi qua nhánh legacy. Các tài khoản này cần quản trị viên xử lý/reset riêng, không thử sửa tiền tố trực tiếp trong DB.

Nếu quản trị viên chấp thuận migration có kiểm soát:

1. Sao lưu DB và giới hạn người được kết nối gateway. Cấp tạm quyền cập nhật riêng cột mật khẩu bằng tài khoản quản trị:

   ```sql
   GRANT UPDATE (password_hash) ON onlinechessdb.Users
   TO 'chess_auth'@'127.0.0.1';
   ```

2. Đặt AUTH_ALLOW_LEGACY_MIGRATION=true chỉ ở server rồi khởi động lại.
3. Cho từng người đăng nhập bằng mật khẩu cũ qua TLS.
4. Service chỉ thay mật khẩu rõ bằng PBKDF2 khi xác minh thành công; UPDATE có điều kiện chống ghi đè dữ liệu đã thay đổi.
5. Kiểm tra định dạng lưu trữ bằng công cụ quản trị, không sao chép hash vào báo cáo.
6. Gỡ biến migration, khởi động lại và thu hồi quyền cập nhật tạm sau đợt chuyển đổi:

   ```sql
   REVOKE UPDATE (password_hash) ON onlinechessdb.Users
   FROM 'chess_auth'@'127.0.0.1';
   ```

   Nếu tài khoản đã được cấp UPDATE ở cấp bảng/schema từ trước, quyền đó vẫn có hiệu lực. Kiểm tra SHOW GRANTS và thu hồi quyền rộng tương ứng bằng tài khoản quản trị; chỉ REVOKE quyền cột không loại bỏ quyền cấp ở cấp cao hơn.

Không bật migration ngầm hoặc vô thời hạn. Tài khoản không thể xác minh mật khẩu cần quy trình reset riêng chưa được triển khai. Thử nghiệm LAN ban đầu nên dùng tài khoản mới.

## 11. Chức năng đồ án ngoài auth

Các chức năng cục bộ và dữ liệu schema không đồng nghĩa với tính năng online hoàn chỉnh.

| Nhóm | Đã có | Chưa hoàn chỉnh |
|---|---|---|
| Giao diện | Login, Register, Dashboard, ChessBoard, CaroBoard; theme sáng/tối | Kiểm thử GUI tự động, hiển thị trạng thái mạng/phiên thống nhất |
| Cờ vua cục bộ | Bàn 8×8, di chuyển cơ bản, phong hậu, bot đơn giản, đồng hồ | Bộ luật quốc tế đầy đủ: king safety, chiếu/chiếu bí, nhập thành, en passant, các trường hợp hòa |
| Caro cục bộ | Bàn 15×15, kiểm tra chuỗi thắng, bot đơn giản | Luật và kết quả được server xác nhận, tích hợp kết quả DB thống nhất |
| Sảnh và matchmaking | UI và schema dự kiến; nút Rank báo đang phát triển | Ghép hai client vào cùng trận qua server, phân màu, ready và thông báo hai phía |
| Chat và nước đi | Có các điểm nối/event phía form | Transport gameplay/chat và đồng bộ giữa hai máy |
| Lịch sử và Elo | Schema; một phần lưu kết quả và cập nhật Elo cũ | Lịch sử sảnh còn dữ liệu mẫu; kết quả mọi nhánh/game chưa thống nhất |

Không dùng các phần này làm bằng chứng rằng server đã quản lý gameplay. Cần kiểm chứng lại từng nhóm khi triển khai server game.

## 12. Các phần framework còn thiếu

Các hạng mục dưới mô tả thiếu gì, hậu quả và tiêu chí hoàn thành để nhóm lập kế hoạch.

### 12.1. Tách gateway và worker thành tiến trình riêng

Hiện cả ba listener có cùng vòng đời và chia sẻ đối tượng dịch vụ trong bộ nhớ. Chưa có khả năng dừng, triển khai hoặc khởi động lại riêng một worker.

Cần bổ sung:

- Chế độ chạy gateway/worker riêng, cấu hình endpoint upstream và vòng đời độc lập
- Quản lý secrets và chứng chỉ ở từng worker
- Health/readiness check, loại và đưa worker trở lại vòng phân phối
- Quản lý tiến trình, tự khởi động lại và kịch bản worker lỗi
- Ngân sách admission dùng chung hoặc phân bổ rõ khi chạy nhiều tiến trình

Tiêu chí hoàn thành: dừng một worker không làm gateway/worker kia chết; yêu cầu mới đi tới worker còn sẵn sàng, không phát lại yêu cầu đã gửi dở. Vẫn cần gateway dự phòng nếu muốn loại điểm lỗi duy nhất ở gateway.

### 12.2. Session và phân quyền sau đăng nhập

Hiện server chỉ trả profile rồi đóng kết nối. Chưa có token, logout, thời hạn phiên, thu hồi phiên hoặc quản lý người chơi online.

Cần session ngẫu nhiên lưu ở kho dùng chung hoặc token được xác minh đúng, gắn với người chơi và thời hạn. Mọi thao tác phòng/trận phải lấy danh tính từ phiên, không từ username/user ID client tự khai. Tiêu chí hoàn thành là cả hai worker nhận biết cùng phiên và từ chối phiên hết hạn/đã thu hồi.

### 12.3. Dịch vụ phòng và matchmaking

Schema và logic hàng chờ DB cũ chưa tạo ra luồng server online đầy đủ. Cần create/join/leave room, phòng công khai/riêng, mã phòng, ready, tìm trận, hủy tìm, ghép cặp và phân màu.

Cần transaction hoặc cơ chế đồng bộ để hai yêu cầu không chiếm cùng chỗ. Tiêu chí hoàn thành là hai client cùng nhận một match ID và vai trò đối nghịch; hủy/mất kết nối không để lại phòng hoặc hàng chờ treo.

### 12.4. Điều phối và xác nhận gameplay trên server

Chưa có server giữ trạng thái bàn cờ và xác minh nước đi. Chuyển tiếp sự kiện UI đơn thuần không đủ chống sai lượt hoặc giả kết quả.

Cần giao thức nước đi có match ID, số thứ tự, danh tính từ session và phiên bản trạng thái. Server kiểm tra lượt, quyền tham gia, luật, đồng hồ và điều kiện kết thúc rồi phát trạng thái cho hai phía. Tiêu chí hoàn thành là client sửa dữ liệu hoặc gửi lại gói cũ không làm sai trận.

### 12.5. Kết quả, Elo và lịch sử có thẩm quyền

Logic lưu cũ chưa thống nhất mọi nhánh kết thúc và cả hai game. Cần server chốt thắng/thua/hòa, hết giờ, đầu hàng, mất kết nối và cầu hòa được chấp nhận.

Cập nhật Matches, MatchPlayers và UserGameStats phải nhất quán, chỉ một lần cho mỗi trận. Không cho client tự quyết định người thắng hoặc Elo. Lịch sử và bảng xếp hạng phải đọc dữ liệu thật thay vì mẫu.

### 12.6. Chat, heartbeat và reconnect

Chưa có transport chat liên máy, heartbeat phiên hoặc quy trình khôi phục trận. Deadline auth 10 giây không phải heartbeat gameplay.

Cần giới hạn chat, kiểm tra thành viên phòng, ping/pong hoặc cơ chế tương đương, thời gian ân hạn mất mạng và snapshot khi kết nối lại. Nếu dùng UDP theo sơ đồ, phải quy định vai trò, định danh phiên, chống giả mạo và xử lý mất gói; không cần chọn UDP chỉ để có đủ tên thành phần.

### 12.7. Loại truy cập DB trực tiếp từ client

Truy vấn DB và credential hardcode cũ đã được loại khỏi Dashboard/ChessBoard. Client hiện không tham chiếu MySQL; ranh giới này cần được giữ khi bổ sung gameplay.

Cần API server cho hàng chờ, lịch sử, thống kê và kết quả; không đưa connection string/dependency DB trở lại client. Client hiện chạy không có DB credential, nhưng các API gameplay tương ứng chưa được triển khai.

### 12.8. Hoàn thiện bảo mật và vận hành

Global admission chỉ hạn chế tải, chưa ngăn đoán mật khẩu theo tài khoản hoặc nguồn. Cần hạn mức tại gateway theo peer thật; worker sau proxy chỉ thấy kết nối loopback, không được tin forwarding metadata client tự gửi.

Các phần khác còn thiếu gồm:

- Đổi/quên mật khẩu, reset có hết hạn và chống dùng lại
- Chính sách phiên và khóa tạm phù hợp, tránh khóa tài khoản bằng tấn công từ ngoài
- Phiên bản giao thức, mã lỗi thống nhất, bản địa hóa thông báo
- Luân chuyển chứng chỉ/pin, quyền private key, kho secrets và thu hồi credential cũ
- Log đã loại dữ liệu nhạy cảm, phân loại lỗi, metrics, health endpoint và cảnh báo
- Migration schema có phiên bản, sao lưu/khôi phục, kiểm thử restore
- Đóng gói phát hành, khởi động như dịch vụ và hướng dẫn firewall

RunAsync hiện nuốt exception để giữ listener sống; cần thêm thống kê lỗi an toàn để vận hành, không ghi nguyên gói auth hoặc connection string.

## 13. Kiểm thử đã có và giới hạn bằng chứng

Kết quả dưới được ghi nhận trong đợt triển khai trước khi viết lại tài liệu, không phải cam kết mọi lần checkout đều có cùng số lượng test.

| Hạng mục | Kết quả ghi nhận | Phạm vi |
|---|---|---|
| AuthTests với MariaDB tạm | 29 đạt, 0 lỗi, 0 bỏ qua | Hash, admission, transaction, trùng đồng thời, migration, profile/Elo |
| TransportTests bản cuối | 27 đạt, 0 lỗi, 0 bỏ qua | TLS thật qua loopback, pin sai, binding, JSON lỗi, shutdown và metric kết nối |
| Kiểm tra nguồn Python | 8 đạt | Quy ước nối UI/entrypoint và cấm client truy cập DB; không phải GUI test |
| Build server | 0 lỗi, 0 cảnh báo | Biên dịch server và thư viện |
| Build WinForms | 0 lỗi, 2 cảnh báo event chưa dùng | Biên dịch sau khi loại MySql.Data khỏi client; không chứng minh thao tác UI |
| LAN ba máy vật lý | Chưa xác minh | Cần thực hiện kịch bản mục 9.7 |
| Game online hai người | Chưa có luồng đầy đủ | Không nằm trong bằng chứng test auth |

Test tích hợp đã chạy AuthClient → gateway → cả hai TLS worker → MySqlAuthService → MariaDB 11.4.5 tạm. Test kiểm tra signup/signin, sai mật khẩu, hash lưu trữ, cập nhật Elo từ DB và cả hai worker nhận yêu cầu. DB thử chỉ bind loopback, đã dừng sau test; không truy cập DB hiện hữu.

Chạy lại build và test thường bằng các lệnh:

```powershell
dotnet build Server/OnlineChessGame.Server.csproj --nologo
dotnet build Design/OnlineChessGame.csproj --nologo
dotnet test Tests/TransportTests/TransportTests.csproj --nologo
dotnet test Tests/AuthTests/AuthTests.csproj --nologo
python -m unittest discover -s Tests -p 'test_*.py'
```

Không có AUTH_TEST_MYSQL thì các test DB chủ động bỏ qua. Muốn xác nhận DB, cấp instance dùng một lần theo ràng buộc fixture; không trỏ vào DB thật vì test cần tạo/xóa database và kiểm tra rollback. Kiểm tra số test bị bỏ qua thay vì chỉ nhìn dòng Passed.

## 14. Xử lý lỗi cấu hình

Entrypoint cố ý không in chi tiết bí mật, vì vậy cần đối chiếu từng lớp cấu hình.

| Hiện tượng | Kiểm tra |
|---|---|
| Tạo PFX lỗi IOException | File đã tồn tại, thiếu thư mục hoặc quyền ghi |
| CryptographicException | Password PFX, file hỏng, quyền đọc hoặc private key |
| InvalidOperationException lúc chạy | Biến bắt buộc trống hoặc chứng chỉ ngoài thời hạn |
| SocketException lúc chạy | IP không thuộc máy A hoặc cổng bị chiếm |
| Test-NetConnection thất bại | Bind gateway, firewall, IP LAN, Wi-Fi isolation |
| Cổng mở nhưng TLS bị từ chối | Pin, endpoint và biến môi trường của tiến trình client |
| service_unavailable | MySQL, user/password, cổng, schema, quyền và cấu hình TLS DB |
| service_busy | Hạn mức đồng thời hoặc token bucket toàn cục; đợi rồi thử lại |
| reset_required | Tài khoản legacy; dùng quy trình migration có kiểm soát |
| Đăng ký lỗi mạng rồi username_taken | Có thể lần trước đã commit nhưng mất phản hồi; thử đăng nhập |

## 15. Thứ tự hoàn thiện đề xuất

Ưu tiên ranh giới tin cậy và trạng thái dùng chung trước khi bổ sung khả năng chịu lỗi.

1. Xác minh auth trên LAN ba máy và quản lý cấu hình/secrets.
2. Thiết kế session và hợp đồng phòng/trận; thống nhất phiên bản và mã lỗi.
3. Tách tiến trình gateway/worker nếu yêu cầu đồ án cần độc lập vòng đời; thêm health check và ngân sách tải phù hợp.
4. Triển khai phòng, matchmaking và dữ liệu dùng chung giữa worker.
5. Triển khai gameplay có thẩm quyền phía server, chat và xử lý mất mạng.
6. Chốt kết quả/Elo/lịch sử bằng transaction và loại DB credential khỏi client.
7. Hoàn thiện luật game, reset mật khẩu, quan sát hệ thống, đóng gói và kiểm thử lỗi.

Mốc hoàn thành hiện tại là xác thực có bảo vệ qua gateway và hai TLS listener. Chưa được ghi nhận đồ án đã hoàn thiện game online, worker đa tiến trình hoặc khả năng sẵn sàng cao.
