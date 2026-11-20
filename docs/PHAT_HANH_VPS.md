# Phát hành ứng dụng cho người chơi và VPS Linux

## Gói người chơi

GitHub Actions tại `.github/workflows/build-release.yml` tạo file
`OnlineChessGame-Windows-x64.zip`. File này đã bao gồm .NET runtime,
`OnlineChessGame.exe`, Stockfish Windows x64, giấy phép Stockfish và
`client-config.json`. Người chơi giải nén rồi mở `OnlineChessGame.exe`.
Người chơi không cần chạy PowerShell, cài .NET SDK hay tải Stockfish riêng.

Stockfish chạy trên máy người chơi cho chế độ Solo. VPS hiện chỉ phục vụ
đăng ký/đăng nhập; đấu Rank/LAN giữa hai người chưa có gameplay server.

## Chuẩn bị VPS trước khi phát hành client

1. Trên VPS Linux x64, chuẩn bị MySQL và schema theo README chính. Tạo thư mục
   server riêng, chẳng hạn `/opt/onlinechess`, và đưa gói server vào đó. Có thể
   tạo gói ban đầu bằng `dotnet publish Server/OnlineChessGame.Server.csproj
   -c Release -r linux-x64 --self-contained true -o release/server` trên máy
   phát triển; VPS không cần .NET SDK.
2. Sao chép `docs/server.env.example` thành `.env` cạnh
   `OnlineChessGame.Server`, rồi thay mật khẩu DB, đường dẫn PFX và mật khẩu PFX.
   `.env` là file riêng của VPS, không đưa lên GitHub hay vào gói tải công khai.
3. Tạo thư mục chứa PFX và chạy `./OnlineChessGame.Server --create-cert` một
   lần. Lệnh sẽ tạo PFX và in `CHESS_CERT_PIN` gồm 64 ký tự. Giữ PFX và mật khẩu
   trên VPS, ghi lại pin để tạo gói client. Nếu đã có PFX từ ServerManager,
   bạn có thể dùng lại file đó và lấy đúng pin của chứng chỉ ấy.
4. Chạy `./OnlineChessGame.Server`; kiểm tra dòng `Auth ready`. Mở cổng TCP
   gateway (mặc định 8888) trên firewall VPS và nhà cung cấp. Cổng MySQL 3306
   chỉ cần truy cập nội bộ nếu MySQL nằm trên cùng VPS.

## Tạo GitHub Release

Vào GitHub → Actions → **Build downloadable client and Linux server** →
**Run workflow**, điền:

- `release_tag`: ví dụ `v1.0.0` (mỗi lần phát hành dùng tag mới).
- `server_endpoint`: IP hoặc domain công khai của VPS, dạng `host:8888`.
- `certificate_pin`: pin SHA-256 64 ký tự vừa lấy từ server.

Workflow tải Stockfish chính thức trong môi trường GitHub Actions, publish
Windows client và Linux server, kiểm tra engine và giấy phép, rồi tạo GitHub
Release với hai file tải về. Để người ngoài tải được, repository cần ở chế độ
public. Không chạy `.ps1` trên VPS hay máy người chơi. Repo cần cho phép GitHub
Actions tạo Release với quyền `contents: write`.

Nếu thay chứng chỉ PFX, cần phát hành lại client với pin mới. Nếu đổi IP/domain
VPS, phát hành lại client hoặc cập nhật `client-config.json` cạnh file `.exe`.

## Giấy phép và phạm vi

Stockfish được phân phối theo GPLv3; bản client giữ `Copying.txt`, `AUTHORS` và
`README.md` từ gói chính thức. Mã nguồn chính xác của engine tải từ
`https://github.com/official-stockfish/Stockfish/tree/sf_19`.

Gói server Linux chứa template cấu hình, không chứa mật khẩu hoặc PFX. Dữ liệu
đăng ký/đăng nhập cần VPS đang chạy; chế độ chơi với máy chạy trên client sau
khi người dùng đăng nhập.
