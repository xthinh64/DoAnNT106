# ĐỒ ÁN MÔN HỌC: LẬP TRÌNH MẠNG (NT106)
## ĐỀ TÀI: GAME THI ĐẤU CỜ TRỰC TUYẾN (CHESS & CARO ONLINE)

Ứng dụng desktop Windows Forms (WinForms) hiện đại phục vụ cho đồ án môn Lập trình Mạng với 2 trò chơi chiến thuật kinh điển: **Cờ Vua (Chess 8x8)** và **Cờ Caro (Gomoku 15x15)**.

---

## 1. Điểm nổi bật về Giao diện & Trải nghiệm (UI/UX)

- **Phong cách thiết kế Flat Modern**: Sử dụng font chữ tiêu chuẩn hệ thống `Segoe UI`, các khối thẻ bo góc mềm mại, viền mảnh tinh tế, phân cấp thị giác rõ ràng.
- **2 Chế độ Theme linh hoạt**:
  - **Light Theme**: Nền trắng ngà kết hợp xám nhạt (`#F3F5F9`), thẻ trắng, điểm nhấn Xanh dương hoàng gia (`#2563EB`).
  - **Dark & Green Theme**: Nền đen sâu (`#0B0F19`), thẻ Dark Slate (`#131A2A`), điểm nhấn Xanh ngọc lục bảo Neon (`#10B981` / `#34D399`) phong cách hiện đại.
  - Nút chuyển đổi Theme 1 chạm (`Theme: Sáng` ⇄ `Theme: Đen & Xanh`) được tích hợp trực tiếp trên tất cả các màn hình.
- **Tương thích 100% Visual Studio Designer**:
  - Tách bạch cấu trúc giữa logic ứng dụng (`FormName.cs`) và mã khởi tạo giao diện (`FormName.Designer.cs`).
  - Đầy đủ Constructor không tham số, hỗ trợ kéo thả, chỉnh sửa thuộc tính trực tiếp qua **Toolbox** và cửa sổ **Properties** trong Visual Studio.
- **Đồ họa GDI+ khử răng cưa & Double-Buffering**:
  - Toàn bộ bàn cờ được vẽ bằng GDI+ mượt mà (`AntiAlias`, `ClearTypeGridFit`).
  - Sử dụng cơ chế Double-Buffering chống hiện tượng giật, nháy hình (flicker) khi click nước đi.

---

## 2. Cấu trúc mã nguồn dự án

```text
Đồ án môn học/
│
├── OnlineChessGame.slnx         # File Solution mở bằng Visual Studio 2022
├── OnlineChessGame.csproj       # File cấu hình dự án .NET 8 WinForms
├── OnlineChessGame.csproj.user  # Đăng ký SubType Form cho VS Designer
├── Program.cs                   # Điểm khởi chạy (Main) & Quản lý vòng đời Form
├── ThemeColors.cs               # Bảng màu động 2 Theme, Font chữ & UI Helpers
│
├── LoginForm.cs                 # Logic xử lý Đăng nhập & Validation
├── LoginForm.Designer.cs        # Giao diện kéo thả Đăng nhập
│
├── RegisterForm.cs              # Logic xử lý Đăng ký tài khoản
├── RegisterForm.Designer.cs     # Giao diện kéo thả Đăng ký
│
├── DashboardForm.cs             # Logic Sảnh chính, Matchmaking & Lịch sử
├── DashboardForm.Designer.cs    # Giao diện kéo thả Sảnh chính
│
├── ChessBoardForm.cs            # Logic Bàn cờ Vua 8x8 (quân cờ, nước đi, bắt quân)
├── ChessBoardForm.Designer.cs   # Giao diện kéo thả Bàn cờ Vua
│
├── CaroBoardForm.cs             # Logic Bàn cờ Caro 15x15 (quân X/O, kiểm tra thắng 5 hàng)
└── CaroBoardForm.Designer.cs    # Giao diện kéo thả Bàn cờ Caro
```

---

## 3. Chi tiết từng màn hình chức năng

### 3.1. Màn hình Đăng nhập (`LoginForm`)
- Thẻ Card trung tâm tối giản, đã lược bỏ các nút thừa.
- Các trường nhập liệu: **Tên đăng nhập**, **Mật khẩu** (ẩn ký tự `•`), **Địa chỉ Server IP** (mặc định `127.0.0.1:8888`).
- Checkbox *"Ghi nhớ tài khoản và thiết lập kết nối"*.
- Nút bấm chính **ĐĂNG NHẬP** và link chuyển nhanh sang màn hình Đăng ký.
- Nút chuyển đổi Theme nhanh ở góc dưới.

### 3.2. Màn hình Đăng ký (`RegisterForm`)
- Thu thập thông tin người chơi mới: **Tên đăng nhập**, **Tên hiển thị (Biệt danh trong game)**, **Mật khẩu**, **Xác nhận mật khẩu**.
- Kiểm tra tính hợp lệ đầu vào: không để trống, mật khẩu tối thiểu 4 ký tự, xác nhận mật khẩu phải trùng khớp.
- Nút bấm **ĐĂNG KÝ TÀI KHOAN** và nút quay lại Đăng nhập.

### 3.3. Màn hình Sảnh chính (`DashboardForm`)
- **Thanh Header**: Avatar chữ cái đại diện, Tên người chơi, Huy hiệu điểm Elo (`Elo: 1200`), Nút đổi theme, Nút Đăng xuất.
- **Cột Trái - Chọn trò chơi & Chế độ thi đấu**:
  1. **Cờ Vua (Chess 8x8)**: Huy hiệu `8x8 • Chiến thuật FIDE`, 2 nút hành động: *Đấu với Máy (Solo)* và *Tìm trận Đấu Rank*.
  2. **Cờ Caro (Gomoku 15x15)**: Huy hiệu `15x15 • 5 Quân Thắng`, 2 nút hành động: *Đấu với Máy (Solo)* và *Tìm trận Đấu Rank*.
  3. **Khu vực Hàng chờ Đấu Rank (Queue Panel)**: Tự động hiển thị khi bấm Tìm trận Đấu Rank kèm đồng hồ đếm giây (`00:15`), thanh trạng thái Marquee và nút Hủy tìm trận.
- **Cột Phải - Lịch sử thi đấu gần nhất**:
  - Thẻ hiển thị các trận gần nhất kèm huy hiệu phân loại `[Cờ Vua]` / `[Cờ Caro]`, kết quả Thắng/Thua/Hòa, đối thủ, điểm Elo biến động (`+18`, `-14`) và mốc thời gian.

### 3.4. Màn hình Bàn Cờ Vua (`ChessBoardForm`)
- **Bàn cờ 8x8 chuẩn quốc tế**:
  - Ô cờ xen kẽ màu gỗ sồi (Theme Sáng) hoặc xanh ngọc lục bảo (Theme Đen & Xanh).
  - Thanh thước tọa độ: Hàng dọc `1–8` và cột ngang `a–h`.
  - Khởi tạo đầy đủ 32 quân cờ Trắng và Đen: Vua (♚), Hậu (♛), Xe (♜), Tượng (♝), Mã (♞), Tốt (♟).
  - Tương tác cơ bản: Click chọn quân cờ ➔ Ô cờ được viền highlight, các ô có thể đi/ăn quân hiện chấm tròn chỉ dẫn ➔ Click ô đích để di chuyển.
  - Quân cờ bị ăn được tự động thu về khay quân bắt (`Bắt: ...`) trên thẻ người chơi.
  - Chế độ Solo: Máy tự động phản hồi nước đi đối ứng sau 400ms.
- **Sidebar điều khiển & Chat**:
  - Thẻ thông tin Đối thủ và Người chơi kèm đồng hồ đếm ngược thời gian thực (`10:00`).
  - Banner hiển thị lượt đi hiện tại (`LƯỢT CỦA TRẮNG (BẠN)` / `LƯỢT CỦA ĐEN`).
  - Các nút hành động: **Cầu hòa**, **Đầu hàng**, **Về Sảnh**.
  - Khung Chat in-game: Lịch sử tin nhắn, ô nhập văn bản và nút **Gửi**.

### 3.5. Màn hình Bàn Cờ Caro (`CaroBoardForm`)
- **Bàn cờ 15x15 kinh điển**:
  - Lưới cờ 15x15 kèm tọa độ cột `A–O` và dòng `1–15`.
  - 5 điểm sao chuẩn (Hoshi) đánh dấu vị trí chiến thuật.
  - Quân cờ 3D đổ bóng (Quân X xanh dương/neon và Quân O đỏ), có vòng tròn highlight nước đi cuối cùng.
  - Tự động kiểm tra điều kiện thắng: 5 quân liên tiếp theo hàng ngang, dọc hoặc chéo.
- **Sidebar thông tin & Chat**: Đầy đủ đồng hồ, trạng thái lượt đi, khung chat và nút chức năng tương tự bàn cờ vua.

---

## 4. Hướng dẫn Mở & Sử dụng trong Visual Studio

### 4.1. Mở dự án
1. Khởi động **Visual Studio 2022** (hỗ trợ .NET 8.0 trở lên).
2. Chọn **Open a project or solution** ➔ Trỏ đến file `OnlineChessGame.slnx` hoặc `OnlineChessGame.csproj` trong thư mục này.

### 4.2. Mở giao diện kéo thả (Design View)
1. Trong cửa sổ **Solution Explorer** bên phải, bạn sẽ thấy các Form:
   - `LoginForm.cs`
   - `RegisterForm.cs`
   - `DashboardForm.cs`
   - `ChessBoardForm.cs`
   - `CaroBoardForm.cs`
2. **Thao tác xem giao diện**:
   - **Click đúp chuột** vào file `.cs` bất kỳ để mở cửa sổ thiết kế đồ họa (**Design View**).
   - Hoặc click chuột phải vào file `.cs` ➔ Chọn **View Designer** (Phím tắt: **`Shift + F7`**).
   - Để chuyển qua lại giữa chế độ Code và Designer: Dùng phím **`F7`** (Code) và **`Shift + F7`** (Designer).

### 4.3. Chạy ứng dụng
- Nhấn phím **`F5`** (Debug) hoặc **`Ctrl + F5`** (Run without debugging) trong Visual Studio.
- Hoặc chạy bằng dòng lệnh Terminal / Command Prompt:
  ```bash
  dotnet run
  ```

---

## 5. Hướng dẫn tích hợp Kết nối Mạng (Network / Socket TCP Client)

Mã nguồn đã được thiết kế sẵn các **Event / Delegate** tiếng Việt rõ ràng ở từng Form. Khi viết phần Backend Network, bạn chỉ cần đăng ký lắng nghe (subscribe) các Event này để gửi/nhận gói tin qua Socket TCP:

| Form | Tên Event / Delegate | Ý nghĩa & Dữ liệu truyền đi |
|---|---|---|
| `LoginForm` | `OnLoginRequested` | Kích hoạt khi bấm Đăng nhập: `(username, password, serverIP)` |
| `RegisterForm` | `OnRegisterRequested` | Kích hoạt khi bấm Đăng ký: `(username, displayName, password)` |
| `DashboardForm` | `OnRankedQueueRequested` | Kích hoạt khi bấm Tìm trận Đấu Rank: `(gameType)` ("Chess" hoặc "Caro") |
| `DashboardForm` | `OnCancelQueueRequested` | Kích hoạt khi bấm Hủy tìm trận |
| `DashboardForm` | `OnLogoutRequested` | Kích hoạt khi bấm Đăng xuất |
| `ChessBoardForm` | `OnChessMoveMade` | Kích hoạt khi đi một nước cờ vua: `(fromRow, fromCol, toRow, toCol)` |
| `ChessBoardForm` | `ReceiveOpponentMove` | Hàm gọi khi Client nhận được nước cờ vua của đối thủ từ Server |
| `CaroBoardForm` | `OnCaroMoveMade` | Kích hoạt khi click một nước cờ Caro: `(row, col)` |
| `CaroBoardForm` | `ReceiveOpponentMove` | Hàm gọi khi Client nhận được nước cờ Caro của đối thủ từ Server |
| Cả 2 bàn cờ | `OnChatSent` | Kích hoạt khi gửi tin nhắn chat: `(messageText)` |
| Cả 2 bàn cờ | `OnSurrender` | Kích hoạt khi người chơi bấm Đầu hàng |
| Cả 2 bàn cờ | `OnDrawRequest` | Kích hoạt khi người chơi gửi yêu cầu Cầu hòa |

---

Hoặc nếu nhóm muốn tinh chỉnh có thể tinh chỉnh sau

## 6. Yêu cầu hệ thống

- **Hệ điều hành**: Windows 10 / 11 (64-bit).
- **Môi trường phát triển**: Visual Studio 2022 (với workload *.NET Desktop Development*).
- **Target Framework**: `.NET 8.0-windows` (C# 12).
