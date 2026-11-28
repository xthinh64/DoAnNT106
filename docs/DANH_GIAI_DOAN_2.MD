# Đánh giá framework sau giai đoạn 2

## 1. Phạm vi và kết luận hiện tại

Tài liệu phản ánh working tree của nhánh `feat/server-chessgame` sau đợt sửa hỗ trợ LAN/Internet ngày 28/11/2026, bao gồm các thay đổi chưa commit. Không coi kết quả tại commit `e1bc53a` là kết quả của mã hiện tại.

Framework có Gateway TCP/TLS phục vụ xác thực và gameplay, hai game worker chạy bằng tiến trình riêng, cùng MySQL lưu trạng thái trận. Đã có chia tải, giữ chỗ, heartbeat, khởi động lại worker và phục hồi trận khi một worker mất kết nối. Gateway hiện mặc định nhận kết nối trên mọi giao diện IPv4 thay vì chỉ localhost. Việc truy cập được từ Internet còn phụ thuộc firewall, NAT, DNS và chứng chỉ; thay đổi bind không tự giải quyết các điều kiện triển khai đó.

Phục hồi sau khi Gateway khởi động lại đã có ở tầng giao thức và được kiểm tra với kho giả lập. Tuy nhiên, chưa thể kết luận trải nghiệm vào lại ván bằng hai client WinForms đã hoàn chỉnh. Mất kết nối client khi Gateway vẫn hoạt động vẫn có thể chốt ván `ABORTED`; đây là giới hạn khác với lỗi worker.

Nguồn đối chiếu: [Gateway](../Server/GameServer.cs), [worker pool](../Server/GameWorkerPool.cs), [điều phối trận bền vững](../Server/DurableMatches.cs), [kho trận MySQL](../Server/MatchStore.cs), [test phục hồi](../Tests/TransportTests/MatchRecoveryTests.cs).

## 2. Kiến trúc thực tế

| Thành phần | Hành vi hiện tại | Phạm vi và giới hạn |
| --- | --- | --- |
| Client → Gateway | Một cổng TCP/TLS mặc định `8888` cho đăng ký, đăng nhập và gameplay. Gateway kết thúc TLS, giữ kết nối và điều phối phòng. | Gateway tham gia đường xử lý từng nước đi, không chỉ chuyển tiếp TCP. Đây vẫn là điểm lỗi chung của các phòng. |
| Game worker 1 và 2 | Hai tiến trình con lắng nghe `127.0.0.1:9001/9002`, kiểm tra người gửi, luật chơi và trạng thái kế tiếp. Gateway kết nối bằng bí mật riêng của từng tiến trình. | Loopback là chủ ý bảo vệ kênh nội bộ, không ngăn client LAN/Internet đi qua Gateway. Chưa hỗ trợ bố trí hai worker trên hai máy riêng. |
| Chia tải và dự phòng | Chọn worker theo tải giữ chỗ/heartbeat; khi bằng tải, xét yêu cầu đang xử lý, thời gian xử lý luật trung bình, CPU rồi độ trễ probe. Có chế độ ngừng nhận trận mới. | Mặc định 32 phòng/worker, ngưỡng nhận phòng mới toàn pool là 32, không phải 64. Khả năng phục hồi còn phụ thuộc tải thực và sức chứa lúc xảy ra lỗi. |
| Heartbeat và đồng bộ | Gateway thăm dò từng worker. Phục hồi lấy snapshot từ kho trận rồi cấp generation mới; worker không đồng bộ trực tiếp với nhau. | Sơ đồ đúng với mã là Gateway ↔ từng worker và Gateway ↔ MySQL. |
| Ghép trận và phân xử | Gateway ghép hai tài khoản khác nhau theo loại cờ; worker phân xử nước đi; Gateway lưu trạng thái/kết quả. | Chưa có ghép theo khoảng Elo, cập nhật Elo cuối trận và lịch sử trận trong sảnh. |
| Lưu trạng thái | `OnlineMatches` trong MySQL lưu lịch sử nước đi, lượt, đồng hồ, generation, revision và kết quả. Cập nhật nước đi thông thường có điều kiện theo generation/revision. | Không dùng SQLite WAL. Hai thao tác reclaim/abort sau khởi động lại còn vấn đề đã ghi nhận ở mục 5. |
| TLS và danh tính | Client chính dùng `GatewayClient`, xác minh tên/IP và nguồn tin cậy hệ điều hành hoặc `.cer` phân phối cho LAN. Sau đăng nhập có token phiên và vé game một lần. | `AuthCluster`/`AuthClient` là luồng auth cũ dùng SHA-256 pin; không được nhầm với entrypoint Gateway v3 của ứng dụng. |
| Chat và telemetry | Chat dùng TCP/TLS; heartbeat có CPU, số yêu cầu đang xử lý và thời gian xử lý luật trung bình. | Chưa có UDP latency probe, UDP telemetry hay P2P chat. Số ms heartbeat không phải RTT client–Gateway hoặc độ trễ MOVE→STATE. |
| Solo | Cờ vua có Stockfish và engine C# dự phòng; Caro có engine C# cục bộ. | Luồng UI thông thường vẫn yêu cầu đăng nhập trước khi vào sảnh; chưa có lối vào Solo trực tiếp khi mất mạng. |

Nguồn: [GameServer](../Server/GameServer.cs), [GameWorker](../Server/GameWorker.cs), [GameWorkerPool](../Server/GameWorkerPool.cs), [MatchRules](../Server/MatchRules.cs), [RoomManager](../Server/RoomManager.cs), [GatewayProtocol](../Network/GatewayProtocol.cs), [LoginForm](../Design/LoginForm.cs).

## 3. LAN, Internet và chứng chỉ sau đợt sửa

### 3.1. Địa chỉ lắng nghe và địa chỉ client

- Server console, ServerManager và gateway của `AuthCluster` mặc định bind `0.0.0.0`, nhận kết nối trên mọi giao diện IPv4. Mặc định này không đồng nghĩa đã bật IPv6.
- `CHESS_BIND_ADDRESS` cho phép chọn IP cục bộ cụ thể, IPv6 hoặc `127.0.0.1` nếu chỉ chạy trên máy. Giá trị trống được hiểu là mặc định `0.0.0.0`; địa chỉ không hợp lệ bị từ chối.
- Client dùng IP LAN, tên miền hoặc IP public thực tế của server. `0.0.0.0` là địa chỉ bind, không phải endpoint để phân phối cho người chơi.
- Worker vẫn bind loopback và không cần mở cổng `9001/9002` ra LAN/Internet. MySQL cũng không cần công khai nếu chạy cùng máy server.

### 3.2. Danh tính TLS độc lập với bind

Server console khi chạy `--create-cert` và ServerManager khi tạo PFX dùng chung `ServerNetworkOptions`. Chứng chỉ thử nghiệm có SAN cho localhost, loopback IPv4/IPv6, IP bind cụ thể và các tên/IP bổ sung. Địa chỉ wildcard không được tự đưa vào SAN.

`CHESS_CERT_HOSTS` hoặc trường **Tên/IP chứng chỉ** trong ServerManager nhận danh sách DNS/IP phân cách bằng dấu phẩy, chẳng hạn `192.168.1.10,chess.example.com`. Danh sách không nhận URL, cổng hoặc địa chỉ wildcard. Khi bind `0.0.0.0`, cần khai báo tên/IP mà client thực sự dùng để chứng chỉ phục vụ được địa chỉ đó.

PFX cũ chỉ có localhost không tự được cập nhật. Nếu đổi danh tính chứng chỉ, cần tạo/cấp lại PFX và cập nhật nguồn tin cậy tương ứng ở client. Giữ PFX/khóa riêng trên server; chỉ phân phối chứng chỉ công khai qua kênh tin cậy. Với Internet production, ưu tiên chứng chỉ CA công cộng có SAN khớp tên miền.

### 3.3. Điều kiện triển khai chưa thể thay thế bằng sửa mã

Chỉ mở cổng TCP Gateway cần thiết, mặc định `8888`, với phạm vi firewall phù hợp. Chạy sau router cần port forwarding tới IP LAN server; CGNAT có thể yêu cầu IP public, VPS hoặc VPN có định tuyến phù hợp. Không bind IP public của router nếu IP đó không thuộc card mạng server.

Kiểm thử hiện có xác nhận kết nối tới địa chỉ ngoài loopback trên chính máy chạy test; **chưa nghiệm thu từ máy LAN khác hoặc qua Internet thực tế**. Gateway bind mọi card IPv4 cũng có thể nhận lưu lượng từ card VPN/public, nên cần rà soát firewall, giới hạn truy cập và theo dõi tải trước khi công khai.

Nguồn: [cấu hình mạng dùng chung](../Server/ServerNetworkOptions.cs), [server console](../Server/Program.cs), [ServerManager](../ServerManager/ServerManagerForm.cs), [test LAN/TLS](../Tests/TransportTests/LanGatewayTests.cs), [test bind/SAN](../Tests/TransportTests/ServerNetworkOptionsTests.cs). Hướng dẫn vận hành: [LAN](HUONG_DAN_CHAY_LAN.md), [VPS và NAT](PHAT_HANH_VPS.md).

## 4. Kết quả kiểm chứng gần nhất

Môi trường: Windows, SDK .NET `10.0.401`, dự án server/test `net8.0`; gọi công cụ Windows từ WSL. Đây là lượt chạy sau khi sửa cấu hình mạng và khối điều kiện trong `GameServer.Start`.

| Bộ kiểm tra | Đạt | Trượt | Bỏ qua |
| --- | ---: | ---: | ---: |
| TransportTests | 75 | 0 | 5 |
| ServerManagerTests | 4 | 0 | 0 |
| **Tổng hai bộ vừa chạy** | **79** | **0** | **5** |

Build `ServerManager/OnlineChessGame.ServerManager.csproj` thành công với **0 warning, 0 error**, đồng thời build các project phụ thuộc gồm server. Đây là kiểm tra biên dịch, không phải nghiệm thu thao tác UI WinForms.

Các bằng chứng mạng mới:

- Test gateway auth mặc định kết nối tới IP ngoài loopback từng thất bại với `connection refused` trước khi đổi mặc định bind; đã đạt sau sửa.
- `GatewayClient` kết nối TLS tới `GameServer` trên IP ngoài loopback: chứng chỉ được tạo bằng helper chung có SAN đúng thì được chấp nhận; thiếu SAN cho IP đó thì bị từ chối.
- Test cấu hình kiểm tra mặc định/ghi đè bind IPv4–IPv6, danh tính SAN và từ chối các giá trị tên/IP chứng chỉ không hợp lệ.
- Năm test dùng Gateway không có match store từng lỗi `NullReferenceException` do thiếu dấu ngoặc quanh khối `durable is not null`. Đã sửa và chạy lại bộ transport; cả năm không còn trượt.

Năm ca bị bỏ qua vì chưa cấu hình MySQL thử nghiệm trong lượt chạy này:

1. `AbortPersistsTerminalRevisionInRowAndSnapshot`.
2. `StartupListsOnlyOpenMatchesAndAllowsOneReclaimAndOneAbort`.
3. `ReclaimRejectsSnapshotSupersededByAcknowledgedMove`.
4. `MigrationPersistsSnapshotAndFencesStaleAndDuplicateTerminalWrites`.
5. `ConfirmedChessMovesSurviveWorkerFailoverWithMySql`.

Không diễn giải **75 test đạt** thành “MySQL đã hết lỗi”. AuthTests, ChessTests, CaroTests, UiSmoke và test Python chưa được chạy lại trong đợt sửa mạng này.

### Mốc kiểm chứng trước đợt sửa mạng

Bản ghi trước đó ngày 28/11/2026 dùng mã ứng dụng tại `e1bc53a` với test đã cập nhật trong working tree, MySQL thật và hai worker tiến trình con. Transport có 41 đạt, 7 trượt; bảy ca trượt gồm năm lỗi vòng đời Gateway vừa sửa và hai lỗi MySQL tại mục 5. Bản ghi cũng nêu AuthTests 29 đạt, ChessTests 10 đạt, CaroTests 39 đạt, ServerManagerTests 4 đạt, UiSmoke 15 đạt và test Python 15 đạt. Các số này là **kết quả lịch sử**, không cộng với bảng hiện tại và không coi là lượt kiểm chứng mới.

Lượt trước đã thử tiếp tục ván cờ vua sau khi dừng worker với MySQL thật: nước `e2e4` được giữ, worker còn lại nhận phòng với generation mới, rồi hai client tiếp tục `e7e5`. Bằng chứng này giới hạn ở cấu hình đã thử, không chứng minh tải lớn hay truy cập Internet.

## 5. Độ bền dữ liệu và phục hồi: phần đạt, phần còn thiếu

| Tình huống | Hiện trạng | Điều cần phân biệt |
| --- | --- | --- |
| Một worker mất kết nối giữa ván | Có đường nạp lại snapshot, tăng generation và gán worker còn sức chứa. Test kho giả lập cho Chess/Caro nằm trong bộ transport đã đạt. | Đồng hồ vẫn tính khi chuyển worker; không cam kết phục hồi tức thời nếu thiếu sức chứa. |
| Gateway khởi động lại | Đọc các ván chưa kết thúc; hủy ván revision 0; reclaim ván đã đi và chờ hai user quay lại trong 90 giây. Test giao thức với snapshot nạp sẵn/kho giả lập đã đạt. | Chưa thử đầy đủ kill/restart Gateway với MySQL và hai WinForms. Luồng hiện xóa mục chờ trước khi mở worker thành công; client ở sảnh chỉ mở kết nối game khi tìm trận. |
| Client mất kết nối khi Gateway còn sống | Có thể gỡ phòng, báo `OPPONENT_LEFT` và lưu `ABORTED`. | Test tái hiện hành vi này đạt không có nghĩa tính năng reconnect đã đạt. Chưa có khoảng chờ nối lại ván đang hoạt động. |
| Reclaim gặp snapshot cũ | Lượt MySQL trước ghi nhận `TryReclaimAsync` có thể nhận snapshot cũ sau khi một nước mới đã được xác nhận. | SQL hiện kiểm generation và `result IS NULL`, chưa kiểm revision mong đợi. Đợt sửa mạng không sửa lỗi này; test tương ứng hiện skipped. |
| Hủy ván qua `AbortMatchAsync` | Lượt MySQL trước ghi nhận snapshot terminal revision tăng nhưng cột revision SQL không tăng cùng. | SQL hiện chỉ cập nhật result/snapshot. Đợt sửa mạng không sửa lỗi này; cần chạy lại test với DB sau khi sửa. |

Nguồn: [DurableMatches](../Server/DurableMatches.cs), [MatchStore](../Server/MatchStore.cs), [MatchRecoveryTests](../Tests/TransportTests/MatchRecoveryTests.cs), [MySqlMatchStoreTests](../Tests/TransportTests/MySqlMatchStoreTests.cs), [DashboardForm](../Design/DashboardForm.cs).

### Sức chứa dự phòng

Mặc định `GameWorkerPool` đặt ngưỡng nhận mới toàn pool bằng sức chứa một worker: 32 phòng tổng với hai worker 32 phòng/worker. Đây là chủ đích dành khả năng chịu mất một worker, khác với giả định nhận 48 hoặc 64 phòng trong đánh giá cũ. API có thể cấu hình ngưỡng khác, nhưng server console và ServerManager hiện dùng mặc định này.

Vẫn còn cửa sổ cần kiểm thử: phép tính tải nhận mới bỏ qua tiến trình vừa thoát trong khi phòng của nó còn chờ phục hồi. Nếu có cặp mới đến đồng thời, chỗ dự phòng có thể bị cạnh tranh. Cần đo ở mức tải mặc định và lúc failover, không suy từ test ít phòng thành bảo đảm N−1 trong mọi tình huống.

### Độ trễ và điểm lỗi chung

Mỗi nước đi đi qua Gateway → worker → Gateway → MySQL rồi mới phát snapshot cho hai client. Worker dựng lại bàn từ lịch sử; chi phí tăng theo độ dài ván. Heartbeat đã có CPU, yêu cầu đang xử lý và thời gian xử lý luật trung bình, nhưng chưa thay thế benchmark MOVE→STATE với p95/p99 theo số phòng, độ dài ván và độ chậm DB.

Gateway và MySQL vẫn là điểm lỗi chung. Hai worker trên cùng máy không bảo vệ được sự cố cả máy. Khả năng đọc ván mở sau restart là bước phục hồi ứng dụng, không phải mô hình nhiều Gateway hoạt động dự phòng hoàn chỉnh. Công khai Internet cũng cần kiểm tra khả năng chống chiếm dụng slot kết nối/ngân sách auth; giới hạn tải toàn cục không đồng nghĩa có kiểm soát theo nguồn kết nối.

## 6. Ưu tiên tiếp theo

1. **Sửa và kiểm chứng hai lỗi MySQL còn mở.** Reclaim phải từ chối snapshot đã bị nước mới vượt qua; abort phải giữ revision cột SQL và snapshot nhất quán. Chạy lại cả năm ca DB đang bị bỏ qua bằng instance thử nghiệm riêng.
2. **Hoàn thiện reconnect và phục hồi Gateway.** Gắn quyền trở lại phòng với userId xác thực, quy định khoảng chờ/đồng hồ và giữ khả năng thử lại nếu chưa mở được worker. Nghiệm thu hai WinForms + MySQL thật khi ngắt Wi-Fi và khi kill/restart Gateway.
3. **Nghiệm thu LAN/Internet thật.** Dùng máy A/B/C, kiểm tra TLS/SAN, đăng nhập, ghép trận, nước đi, chat và failover. Với Internet, kiểm tra firewall/NAT/CGNAT, chứng chỉ CA và hạn chế truy cập; không chỉ kiểm tra TCP mở cổng.
4. **Kiểm tra dự phòng dưới tải.** Tạo phòng mới đồng thời với worker sập, đo chỗ dự phòng, thời gian phục hồi và p95/p99 MOVE→STATE; bổ sung trường hợp DB chậm/mất kết nối.
5. **Đồng bộ sơ đồ và phạm vi giao thức.** Gắn nhãn luồng auth pin cũ, mô tả đúng Gateway v3/TLS/MySQL. Nếu môn học yêu cầu UDP, ưu tiên probe RTT client–Gateway có nonce; P2P chat là hạng mục riêng cần giải quyết NAT và xác thực.

[Kế hoạch và tiêu chí nghiệm thu giai đoạn 3](PLAN_GIAI_DOAN_3.md).

## 7. Chạy lại từ thư mục gốc

Dùng PowerShell Windows với SDK hỗ trợ .NET 8. Để chạy ca DB, đặt `MATCH_TEST_MYSQL` và `AUTH_TEST_MYSQL` trỏ tới MySQL loopback **dành riêng cho test**, với quyền tạo/xóa schema thử nghiệm. Không dùng database vận hành. Thiếu biến sẽ làm các ca liên quan bị bỏ qua. Đặt Stockfish tại `Engines/Stockfish/stockfish.exe` nếu muốn kiểm tra engine ngoài.

```powershell
dotnet test Tests/TransportTests/TransportTests.csproj --nologo --logger trx
dotnet test Tests/ServerManagerTests/ServerManagerTests.csproj --nologo
dotnet build ServerManager/OnlineChessGame.ServerManager.csproj --nologo

# Các bộ kiểm tra bổ sung, chưa chạy lại trong đợt sửa mạng:
dotnet test Tests/AuthTests/AuthTests.csproj --logger trx
dotnet test Tests/ChessTests/ChessTests.csproj
dotnet test Tests/CaroTests/CaroTests.csproj
dotnet run --project Tests/UiSmoke/UiSmoke.csproj
python -m unittest discover -s Tests -p "test_*.py"
```

Khi báo cáo nghiệm thu, ghi riêng kết quả build, test tự động, test MySQL, thao tác UI và kiểm tra qua các máy thật. Không dùng một loại bằng chứng để thay thế loại còn lại.
