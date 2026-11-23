# Phát hành ứng dụng cho người chơi và VPS Linux

## Gói người chơi

GitHub Actions tại `.github/workflows/build-release.yml` tạo file
`OnlineChessGame-Windows-x64.zip`. File này đã bao gồm .NET runtime,
`OnlineChessGame.exe`, Stockfish Windows x64, giấy phép Stockfish và
`client-config.json`. Người chơi giải nén rồi mở `OnlineChessGame.exe`.
Người chơi không cần chạy PowerShell, cài .NET SDK hay tải Stockfish riêng.

Stockfish chạy trên máy người chơi cho chế độ Solo. Game server đã có hàng chờ,
chuyển nước đi và chat. Gateway TCP/TLS `8888` phục vụ cả auth và gameplay.
Hai worker chạy nội bộ trên loopback. Cập nhật client và server cùng phiên bản.
Worker xác nhận nước đi và Gateway lưu trạng thái, kết quả trong MySQL. Ghép theo Elo và cập nhật Elo sau trận chưa có.

## Chuẩn bị VPS trước khi phát hành client

1. Trên VPS Linux x64, chuẩn bị MySQL, schema và quyền `OnlineMatches` theo [hướng dẫn database](HUONG_DAN_CHAY_LAN.md#1-máy-a-chuẩn-bị-mysql). Tạo thư mục
   server riêng, chẳng hạn `/opt/onlinechess`, và đưa gói server vào đó. Có thể
   tạo gói ban đầu bằng `dotnet publish Server/OnlineChessGame.Server.csproj
   -c Release -r linux-x64 --self-contained true -o release/server` trên máy
   phát triển; VPS không cần .NET SDK.
2. Sao chép `docs/server.env.example` thành `.env` cạnh
   `OnlineChessGame.Server`, rồi thay mật khẩu DB, đường dẫn PFX và mật khẩu PFX.
   `.env` là file riêng của VPS, không đưa lên GitHub hay vào gói tải công khai.
3. Dùng tên miền trỏ tới VPS và chứng chỉ từ CA công cộng có SAN khớp tên miền.
   Cấu hình PFX chứa khóa riêng và chứng chỉ trung gian cần thiết. Giữ PFX và
   mật khẩu trên VPS.
4. Chạy `./OnlineChessGame.Server` và kiểm tra dòng `Gateway ready`. Mở cổng TCP
   gateway (mặc định 8888) trên firewall VPS và nhà cung cấp. Cổng MySQL 3306
   chỉ cần truy cập nội bộ nếu MySQL nằm trên cùng VPS.

## Tạo GitHub Release

Vào GitHub → Actions → **Build downloadable client and Linux server** →
**Run workflow**, điền:

- `release_tag`: ví dụ `v1.0.0` (mỗi lần phát hành dùng tag mới).
- `server_endpoint` là tên miền đã cấu hình chứng chỉ, dạng `host:8888`.

Workflow tải Stockfish chính thức trong môi trường GitHub Actions, publish
Windows client và Linux server, kiểm tra engine và giấy phép, rồi tạo GitHub
Release với hai file tải về. Để người ngoài tải được, repository cần ở chế độ
public. Không chạy `.ps1` trên VPS hay máy người chơi. Repo cần cho phép GitHub
Actions tạo Release với quyền `contents: write`.

Khi gia hạn chứng chỉ, cập nhật PFX trên VPS và khởi động lại server. Nếu đổi
tên miền, cập nhật `client-config.json` cạnh file `.exe`.

## Giấy phép và phạm vi

Stockfish được phân phối theo GPLv3; bản client giữ `Copying.txt`, `AUTHORS` và
`README.md` từ gói chính thức. Mã nguồn chính xác của engine tải từ
`https://github.com/official-stockfish/Stockfish/tree/sf_19`.

Gói server Linux chứa template cấu hình, không chứa mật khẩu hoặc PFX. Dữ liệu
đăng ký/đăng nhập cần VPS đang chạy; chế độ chơi với máy chạy trên client sau
khi người dùng đăng nhập.
