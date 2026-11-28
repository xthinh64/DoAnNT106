# Kế hoạch giai đoạn 3 — Điều phối trận mới và kiểm chứng phục hồi

Mốc đối chiếu là `e1bc53a` trên `feat/server-chessgame`. Nhóm cần chứng minh được cam kết ban đầu: khi một worker dừng, người chơi vẫn đăng nhập qua Gateway, trận mới được chuyển sang worker còn chỗ và worker khởi động lại nhận được trận mới. Những lỗi phát hiện khi chạy lại bộ test quyết định thứ tự thực hiện bên dưới. [Mục tiêu bốn giai đoạn](../PLAN_UDP_WORKFLOW.md) · [Kết quả đánh giá giai đoạn 2](DANH_GIAI_DOAN_2.MD)

## 1. Khôi phục bộ kiểm tra phát hành

| Thứ tự | Công việc | Điều kiện nghiệm thu |
|---|---|---|
| 1 | Sửa vòng đời `GameServer`. Chỉ chạy monitor khi có match store. Hoàn tất quét và nhận quyền các ván mở trước khi nhận kết nối game. Nếu khởi động thất bại, đóng listener và worker đã mở. | Bộ Transport chạy hết. Server có và không có store đều start/dispose sạch. Dùng store cố tình trì hoãn lúc startup rồi đăng nhập từ client, xác nhận tài khoản nhận đúng ván khi dịch vụ sẵn sàng. |
| 2 | Đồng bộ điều kiện ghi trong `MySqlMatchStore`. Kiểm tra cả generation và revision đã đọc khi reclaim/hủy, ghi phiên bản cuối vào cột SQL và snapshot trong cùng lệnh. Xử lý kết quả ghi có điều kiện ở Gateway. | Trên MySQL tạm, xác nhận một nước giữa lúc đọc và reclaim. Snapshot cũ bị từ chối, nước vừa xác nhận vẫn đọc lại được. Hủy chỉ có hiệu lực một lần và revision SQL bằng revision JSON. Hai Gateway cạnh tranh chỉ một bên được công bố trạng thái mới. |
| 3 | Đưa TransportTests và test DB vào kiểm tra trước phát hành. Lưu TRX theo commit, phân biệt số đạt, trượt và bỏ qua. | Một lần chạy với `MATCH_TEST_MYSQL` trên DB tạm thực thi cả ca SQL và failover. Thiếu DB phải thể hiện ở báo cáo. Ca hồi quy đang đỏ phải chuyển xanh bằng thay đổi production. |

Mã liên quan: [GameServer](../Server/GameServer.cs), [MySqlMatchStore](../Server/MatchStore.cs), [test kho trận](../Tests/TransportTests/MySqlMatchStoreTests.cs), [workflow phát hành](../.github/workflows/build-release.yml).

## 2. Nghiệm thu điều phối hai worker

| Kịch bản | Cách chạy và bằng chứng cần lưu |
|---|---|
| Trận mới khi W1 chết | Hai worker có PID khác nhau. Ghi phân bố phòng ban đầu. Dừng đúng PID W1 trong môi trường test, đăng nhập và ghép một cặp qua Gateway khi W2 còn chỗ. Sau khi W1 có PID mới và heartbeat Ready, ghép cặp khác. Lưu trạng thái worker, số phòng giữ chỗ và nước đi hoặc tin nhắn được xác nhận. Lặp lại với W1 ở chế độ Draining. |
| Dành chỗ cho ván đang chơi | Dùng giới hạn mặc định N−1, thu nhỏ còn hai phòng/worker. Mỗi worker giữ một phòng. Dừng W1, cố ý cho một cặp mới xin phòng trong khi phòng cũ đang chờ chuyển. Cả hai ván cũ phải đi tiếp trên W2. Sau khi hết chỗ, cặp mới nhận phản hồi rõ ràng. Đếm cả phòng đang chờ chuyển khi quyết định nhận trận mới. |
| Đo tải qua đường đi thật | Dùng client TLS và MySQL tạm. Chạy mức một phòng, gần ngưỡng và đúng ngưỡng đã cấu hình, với lịch sử ván ngắn và dài. Ghi thời gian từ gửi MOVE tới nhận STATE cùng revision, thời gian xử lý luật, ghi DB, phục hồi sau lỗi và số yêu cầu thất bại. Báo p50/p95/p99 kèm số mẫu, cấu hình máy và tải; chọn ngưỡng chấp nhận sau phép đo cơ sở. |

Giới hạn hiện tại là hai tiến trình worker cùng máy, một Gateway và một MySQL. `Sessions` được suy ra từ số phòng; heartbeat đo đường Gateway–worker. Báo cáo độ trễ người chơi dùng phép đo MOVE→STATE. [Bộ chọn worker](../Server/GameWorkerPool.cs) · [Test phân tải và định tuyến](../Tests/TransportTests/GatewayTests.cs)

## 3. Bàn giao sang mục tiêu phục hồi ván

Đây là phần nối tiếp sau khi nghiệm thu định tuyến trận mới. Bản đánh giá giai đoạn 2 ưu tiên trải nghiệm vào lại ván khi Wi-Fi chập chờn. Hai tình huống cần hai phép thử độc lập.

1. **Mất một client trong khi Gateway còn chạy.** Tách rời thao tác chủ động rời ván với việc socket bị ngắt. Giữ phòng theo userId đã xác thực, thống nhất chính sách đồng hồ và thời hạn chờ giữa client/server, cấp vé mới và cho WinForms mở lại bàn ngay từ sảnh. Thay ca mô tả hành vi `ABORTED` hiện tại khi triển khai chính sách mới. Nghiệm thu trên hai máy B/C cho cả Chess và Caro: ngắt Wi-Fi một máy sau một nước đã xác nhận, nối lại trong hạn, đối chiếu roomId, nước đi, lượt, đồng hồ rồi đi thêm một nước. Thử lại quá hạn và với tài khoản thứ ba.
2. **Mất Gateway.** Giữ danh sách chờ cho đến khi gắn được worker và ghi trạng thái thành công. Dọn phiên đã ngắt của người chờ, thử lại nếu worker/DB lỗi tạm thời và phát kết quả hết hạn một lần. Thử dừng tiến trình đột ngột và tắt có kiểm soát thành hai ca riêng. Trên MySQL thật, thử hai thứ tự kết nối lại, một người vắng quá 90 giây, ván revision 0 và worker chưa sẵn sàng. So nước cuối cùng đã xác nhận, generation mới, revision kế tiếp và trạng thái ở cả hai client.

[Điều phối phục hồi](../Server/DurableMatches.cs) · [Sảnh WinForms](../Design/DashboardForm.cs) · [Test phục hồi](../Tests/TransportTests/MatchRecoveryTests.cs)

## Bằng chứng để chốt giai đoạn

Gắn kết quả với commit, phiên bản .NET/MySQL, công suất worker và loại client. Lưu TRX, log chuyển worker với roomId/generation/revision, bảng độ trễ và biên bản hai máy LAN khi thực hiện. Công bố riêng kết quả điều phối trận mới và phục hồi ván đang chơi. Khi xác nhận yêu cầu UDP của môn học, bổ sung phép đo RTT client–Gateway và cập nhật sơ đồ đường chat, TLS, worker, MySQL theo cấu hình đã demo.
