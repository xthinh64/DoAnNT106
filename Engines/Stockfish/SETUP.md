# Cài Stockfish cho chế độ chơi với máy

Từ Windows PowerShell tại thư mục gốc dự án:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\setup-stockfish.ps1
```

Script tải bản Windows x64 chính thức, đặt tại
`Engines/Stockfish/stockfish.exe` và giữ lại giấy phép GPL v3. Khi build,
project WinForms tự sao chép thư mục engine sang output.

Nếu đã có Stockfish ở chỗ khác, đặt biến môi trường `STOCKFISH_PATH` bằng
đường dẫn đầy đủ tới file `.exe` trước khi chạy client.
