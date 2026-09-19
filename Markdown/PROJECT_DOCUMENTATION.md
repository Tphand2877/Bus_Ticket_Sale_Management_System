# BusTicketSaleSystem

## 1. Tổng quan dự án

BusTicketSaleSystem là ứng dụng bán vé xe khách trên nền tảng Windows Desktop. Người dùng có thể đăng ký, đăng nhập, tìm chuyến xe, chọn ghế, đặt vé, thanh toán, xem vé và lịch sử chuyến đi. Nhân viên quản trị có thể quản lý tuyến đường, đội xe, lịch chạy và tài khoản người dùng.

Mục tiêu hiện tại là xây dựng hệ thống bán vé có quy trình nghiệp vụ tương đối đầy đủ, dùng SQL Server làm cơ sở dữ liệu trung tâm và áp dụng mô hình MVVM cho giao diện WPF.

Ngày đánh giá: **15/09/2026**

Mức độ hoàn thiện đánh giá lại sau hardening:

- **Khoảng 88%** đối với mục tiêu đồ án/demo có thể chạy được.
- **Khoảng 65–70%** nếu đánh giá theo tiêu chuẩn sản phẩm production.

Đã kiểm chứng lại bằng build, test, migration trên SQL Server thật và smoke test UI: đăng ký/đăng nhập, đặt vé, admin xác nhận BankTransfer, thanh toán chuyển sang Success/Paid và báo cáo doanh thu. Các giới hạn production còn lại gồm secret store, CI/CD, installer, backup/restore và kiểm thử VNPay sandbox thực tế.

## 2. Kiến trúc kỹ thuật

Project chính sử dụng mô hình gần với MVVM:

    XAML View
        ↓ Data Binding / Command
    ViewModel
        ↓
    Service / Data Access
        ↓
    SQL Server

### Các lớp chính

| Thành phần | Vai trò |
|---|---|
| Views/ | Các cửa sổ và màn hình WPF. |
| ViewModels/ | Trạng thái giao diện, command, điều hướng và logic nghiệp vụ ở tầng UI. |
| Models/ | Model người dùng, chuyến xe, ghế, vé, thanh toán và quản trị. |
| Data/ | Kết nối SQL Server, truy vấn nghiệp vụ, migration, seed data và dịch vụ quản trị database. |
| Common/ | Base ViewModel, command và converter của WPF. |
| Services/ | Payment state machine, in-process VNPay callback listener, background lifecycle jobs, email, printing, navigation and logging. |
| Data/*.sql | Schema, migration, index, payment ledger và dữ liệu mẫu. |

## 3. Công nghệ sử dụng

### Nền tảng và ngôn ngữ

- C#.
- .NET 10.
- WPF Desktop, target framework net10.0-windows.
- VNPay callback uses an in-process HttpListener on localhost; no separate Backend project is shipped.
- Microsoft SQL Server.

### Thư viện chính

- Microsoft.Data.SqlClient 7.0.2: kết nối và truy vấn SQL Server.
- Microsoft.Extensions.Configuration.Json 10.0.0: đọc appsettings.json.
- QRCoder 1.6.0: hỗ trợ QR.
- QuestPDF 2025.12.3: xuất vé PDF.
- Serilog 4.3.0 và Serilog.Sinks.File 7.0.0: ghi log.
- xUnit 2.9.3: unit test.
- Microsoft.NET.Test.Sdk 17.14.1: chạy test.
- coverlet.collector 6.0.4: thu thập coverage.
- Xunit.SkippableFact 1.5.23: skip test cần database fixture.

### API và dịch vụ ngoài

- VNPay Sandbox cho thanh toán trực tuyến.
- VietQR/chuyển khoản thông qua ảnh QR cấu hình sẵn.
- Brevo SMTP API để gửi email vé PDF.
- Open-Meteo và Nominatim cho dữ liệu thời tiết/vị trí.
- OpenStreetMap để mở bản đồ tuyến đường.
- Provinces Open API để lấy danh sách tỉnh/thành.

## 4. Chức năng đã triển khai

### 4.1. Người dùng và tài khoản

- Đăng ký tài khoản.
- Đăng nhập bằng username hoặc email.
- Kiểm tra username/email trùng.
- Kiểm tra độ dài và tính hợp lệ của mật khẩu.
- Mã hóa mật khẩu bằng PBKDF2 với salt ngẫu nhiên.
- Cập nhật họ tên, email và số điện thoại.
- Đổi mật khẩu.
- Lưu session người dùng hiện tại.
- Đăng xuất và quay lại màn hình đăng nhập.

### 4.2. Trang khách hàng

- Dashboard người dùng.
- Thống kê số vé, chuyến sắp tới và tổng tiền đã chi.
- Hiển thị hạng thành viên và điểm thưởng.
- Hiển thị các tuyến phổ biến.
- Tìm kiếm chuyến theo điểm đi, điểm đến và ngày.
- Xem toàn bộ chuyến khả dụng.
- Hoán đổi điểm đi và điểm đến.

### 4.3. Đặt vé

Quy trình chính:

    Tìm chuyến
        → Chọn chuyến
        → Chọn ghế
        → Nhập thông tin hành khách
        → Chọn phương thức thanh toán
        → Tạo vé và giao dịch
        → Xem vé hoặc chờ xác minh

Các phần đã làm:

- Tải danh sách điểm đi/điểm đến từ database.
- Tìm chuyến theo ngày khởi hành.
- Hiển thị giá, giờ đi, giờ đến, loại xe và số ghế trống.
- Tải sơ đồ ghế theo từng chuyến.
- Cho phép chọn nhiều ghế còn trống trong một lần đặt.
- Chặn chọn ghế đã được giữ hoặc đã thanh toán.
- Đặt toàn bộ các ghế đã chọn trong một transaction `IsolationLevel.Serializable`.
- Kiểm tra ghế thuộc đúng xe của chuyến.
- Unique filtered index chống đặt trùng ghế.
- Sinh mã vé riêng cho từng ghế dạng `BT-YYYYMMDDHHmmss-XXXX`.
- Lưu thông tin hành khách riêng trên từng vé.

### 4.4. Vé và lịch sử

- Xem danh sách vé đang hoạt động, bao gồm cả vé đang chờ payment được duyệt.
- Với vé pending, nút **View E-Ticket** được thay bằng nút **Pending** và không thể thao tác.
- Chỉ ticket đã thanh toán mới mở được chi tiết e-ticket.
- Xem chi tiết vé.
- Hủy vé và ghi thời điểm hủy.
- Xem vé đã hủy hoặc đã sử dụng trong lịch sử.
- Tính tổng số chuyến hoàn thành và tổng kilomet.
- Book Again từ lịch sử.
- Xuất vé PDF bằng QuestPDF.
- Gửi vé PDF qua email bằng Brevo khi đã cấu hình.

### 4.5. Thanh toán

Các phương thức trên giao diện:

- VNPay Sandbox — chỉ khả dụng khi có `VNPAY_TMN_CODE` và `VNPAY_HASH_SECRET`.
- Tiền mặt khi lên xe.
- Chuyển khoản/VietQR.

Đã triển khai:

- Payment ledger trong bảng Payments.
- BankTransfer được admin xác nhận trên màn hình Payments & Settlement.
- Cash giữ ghế đến giờ khởi hành và được admin xác nhận thu tiền.
- VNPay tạo URL, xác minh HMAC-SHA512 và nhận callback qua listener localhost trong app.
- Thời hạn giữ BankTransfer là 15 phút; payment pending tự hết hạn qua background scheduler.
- Ticket paid có thể check-in và chuyển sang `Đã sử dụng`.
- Admin có reject payment, approve refund, báo cáo doanh thu theo tuyến.
- VietQR vẫn dùng ảnh QR tĩnh từ `QR.jpg`; chưa tự động đối soát ngân hàng.

## 5. Khu vực quản trị

Tài khoản có role Admin được chuyển vào AdminWindow.

### Dashboard quản trị

- Tổng số tuyến đường.
- Tổng số xe.
- Số chuyến sắp tới.
- Số vé đang hoạt động.
- Tổng giá trị vé.
- Số lượng tài khoản khách hàng.
- Danh sách đặt vé gần đây.
- Danh sách chuyến sắp tới.
- Tình trạng đội xe và tỷ lệ sử dụng ghế.

### Quản lý dữ liệu

- Thêm, sửa và xóa tuyến đường.
- Cập nhật khoảng cách tuyến.
- Thêm xe và tự động sinh sơ đồ ghế.
- Xóa xe chưa được sử dụng.
- Thêm, sửa và xóa chuyến xe.
- Kiểm tra chuyến bị trùng thời gian trên cùng một xe.
- Tìm kiếm tài khoản.
- Chuyển role Customer/Admin.
- Không cho admin tự hạ quyền của chính mình.

## 6. Thiết kế cơ sở dữ liệu

| Bảng | Nội dung |
|---|---|
| Accounts | Tài khoản, liên hệ, password hash và role. |
| Routes | Điểm đi, điểm đến và khoảng cách. |
| Buses | Biển số, số ghế và loại xe. |
| Trips | Lịch chạy, tuyến, xe, giờ đi, giờ đến và giá vé. |
| Seats | Sơ đồ ghế theo xe. |
| Tickets | Vé, hành khách, ghế, chuyến và trạng thái. |
| Payments | Giao dịch, provider, số tiền và trạng thái. |
| __DatabaseMigrations | Theo dõi migration đã chạy. |

### Trạng thái vé

- Đã đặt.
- Đã thanh toán.
- Đã hủy.
- Đã sử dụng.

### Trạng thái thanh toán

- Chưa thanh toán.
- Đã thanh toán.
- Hoàn tiền.

### Migration và seed

1. Schema1.sql: schema ban đầu.
2. Migration_001.sql: bổ sung trường dữ liệu vé.
3. Migration_002_Admin.sql: bổ sung role admin.
4. Schema_Payments.sql: payment ledger và index.
5. Migration_003_PerformanceIndexes.sql: index tối ưu truy vấn.
6. Migration_004_PaymentExpiry.sql: thời hạn giữ vé.
7. SeedData.sql: dữ liệu mẫu phát triển.

SeedData.sql phù hợp cho môi trường development/demo, không nên dùng nguyên trạng cho production.

## 7. Tiến trình kỹ thuật

### Giai đoạn 1 – Nền tảng

**Trạng thái: Hoàn thành**

- Tạo solution và project WPF.
- Cấu hình .NET 10 và WPF.
- Thiết lập cấu trúc MVVM.
- Tạo theme, converter, command và base ViewModel.

### Giai đoạn 2 – Xác thực và tài khoản

**Trạng thái: Hoàn thành**

- Xây dựng bảng Accounts.
- Hoàn thiện đăng ký/đăng nhập.
- Thêm PBKDF2 password hashing.
- Thêm profile, đổi mật khẩu, session và logout.

### Giai đoạn 3 – Tuyến, xe và chuyến

**Trạng thái: Hoàn thành**

- Xây dựng Routes, Buses, Trips, Seats.
- Thêm migration và seed data.
- Kiểm tra trùng tuyến và trùng lịch xe.
- Thêm index cho truy vấn thường dùng.

### Giai đoạn 4 – Đặt vé và ghế

**Trạng thái: Hoàn thành ở mức nghiệp vụ chính**

- Tìm chuyến.
- Chọn ghế.
- Giữ ghế tạm thời.
- Đặt vé trong transaction.
- Chống đặt trùng.
- Hủy vé và hiển thị lịch sử.

### Giai đoạn 5 – Xuất vé và API ngoài

**Trạng thái: Đã triển khai**

- Xuất PDF, PNG, QR và in vé.
- Gửi vé PDF qua Brevo khi cấu hình biến môi trường.
- Mở bản đồ tuyến đường từ màn hình vé.

### Giai đoạn 6 – Thanh toán

**Trạng thái: Hoàn thành luồng chính; còn thiếu đối soát ngân hàng tự động**

- Payment ledger và state machine settlement.
- Admin xác nhận BankTransfer/Cash, reject payment và approve refund.
- VNPay Sandbox có callback listener trong WPF, polling trạng thái và HMAC-SHA512.
- Background scheduler hết hạn payment và đánh dấu vé đã đi là `Đã sử dụng`.
- VietQR hiển thị QR tĩnh từ `QR.jpg`.

### Giai đoạn 7 – Quản trị

**Trạng thái: Hoàn thành giao diện, CRUD chính, settlement và báo cáo**

- Dashboard doanh thu chỉ tính payment Success trừ refund.
- Payments & Settlement: confirm/reject/refund/check-in.
- Revenue Reports theo tuyến và khoảng ngày.
- Quản lý tuyến, xe, chuyến và tài khoản với validation.

### Giai đoạn 8 – Kiểm thử

**Trạng thái: Đang hoàn thiện**

- Có test password hashing.
- Có test model và định dạng dữ liệu.
- Có test NavigationService.
- Có test input thanh toán.
- Có test concurrency booking nhưng đang skip nếu chưa có fixture SQL Server.

## 8. Kết quả build và test

Đã kiểm tra trên môi trường .NET SDK 10:

    dotnet build BusTicketSaleSystem.csproj --no-restore

Kết quả:

    Build succeeded.
    0 Warning(s)
    0 Error(s)

Kết quả test:

    Passed: 16
    Failed: 0
    Skipped: 1
    Total: 17

Test bị skip là test đặt cùng một ghế đồng thời. Test này yêu cầu:

- BUS_TICKET_TEST_TRIP_ID
- BUS_TICKET_TEST_SEAT_ID
- BUS_TICKET_TEST_ACCOUNT_IDS

Kết quả trên xác nhận compile và unit test hiện có, nhưng chưa thay thế cho kiểm thử tích hợp toàn bộ với SQL Server.

## 9. Cấu hình chạy

File cấu hình mặc định là appsettings.json:

    Server=localhost;Database=BusTicketSaleSystem;Integrated Security=True;TrustServerCertificate=True;Encrypt=False;

Có thể ghi đè bằng các biến môi trường:

- BUS_TICKET_CONNECTION_STRING.
- BUS_TICKET_MASTER_CONNECTION_STRING.
- VNPAY_TMN_CODE.
- VNPAY_HASH_SECRET.
- VNPAY_RETURN_URL.
- BREVO_API_KEY.
- EMAIL_SENDER_NAME.
- EMAIL_SENDER_ADDRESS.

Điều kiện chạy:

1. Windows Desktop Runtime phù hợp với .NET 10.
2. SQL Server đang hoạt động.
3. Tài khoản có quyền tạo database hoặc database đã tồn tại.
4. Cấu hình VNPay nếu muốn thử thanh toán.
5. Cấu hình Brevo nếu muốn gửi email.

## 10. Hạn chế hiện tại

### Thanh toán

- VietQR vẫn dùng QR tĩnh từ `QR.jpg`; chưa tự động nhận biết giao dịch chuyển khoản.
- VNPay callback listener chạy trong WPF qua localhost; cần kiểm thử thêm với tài khoản Sandbox thật.
- MoMo chưa có tích hợp và không còn xuất hiện như phương thức khả dụng.
- Refund đã có request từ khách và approve từ admin, nhưng chưa có hoàn tiền tự động qua cổng thanh toán.

### Kiểm thử

- Đã smoke test UI đăng ký, đăng nhập, đặt vé, admin settlement và báo cáo.
- Chưa có UI automation test lâu dài trong solution.
- Chưa có integration test fixture SQL Server tự tạo/xóa cho CI.
- Chưa kiểm thử đầy đủ timeout, mất mạng và chữ ký VNPay với gateway thật.
- Test concurrency cần SQL Server fixture riêng.

### Production readiness

- Connection string mặc định dùng SQL Server local và Windows Integrated Security.
- Secret thanh toán phụ thuộc biến môi trường, chưa có secret store chuyên dụng.
- Chưa có pipeline CI/CD.
- Chưa có installer hoặc quy trình publish chính thức.
- Chưa tự động hóa backup/restore database.
- Cần thêm retry/cancellation policy cho một số truy vấn và API ngoài.

### Backend

- Backend ASP.NET Core cũ đã được loại bỏ khỏi solution.
- VNPay callback hiện dùng listener localhost trong ứng dụng desktop; mô hình production nhiều máy cần chuyển callback sang service HTTPS công khai trước khi thương mại hóa.

## 11. Việc nên làm tiếp theo

### Ưu tiên cao

1. Tích hợp đối soát VietQR tự động hoặc cổng thanh toán ngân hàng.
2. Kiểm thử VNPay Sandbox thực tế với callback HTTPS công khai.
3. Viết integration fixture SQL Server và chạy concurrency test trong CI.

### Ưu tiên trung bình

1. Thêm retry, timeout và cancellation thống nhất cho database/API.
2. Thêm secret store, cấu hình Development/Staging/Production.
3. Bổ sung workflow hoàn tiền thật khi provider hỗ trợ.

### Ưu tiên triển khai

1. Tạo pipeline build/test tự động.
2. Tạo installer hoặc publish profile chính thức.
3. Thiết lập backup/restore database, logging tập trung và giám sát lỗi.

## 12. Kết luận

BusTicketSaleSystem đã vượt qua giai đoạn prototype đơn giản và có nền tảng tương đối đầy đủ cho một hệ thống bán vé xe: giao diện WPF, MVVM, database có migration, đặt vé có kiểm soát concurrency, quản trị hệ thống và payment ledger.

Luồng demo/đồ án hiện đã hoàn chỉnh cho đặt vé, settlement thủ công, check-in, hoàn tiền yêu cầu và báo cáo. Phần còn thiếu chủ yếu là đối soát ngân hàng tự động, VNPay production qua HTTPS công khai, CI/CD, installer, secret store và backup/restore. Project đủ nền tảng để phát triển tiếp thành sản phẩm; không nên triển khai thương mại trước khi xử lý các giới hạn production này.
