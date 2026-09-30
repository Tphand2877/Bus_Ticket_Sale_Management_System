# 04 — Dữ liệu, ràng buộc và ngữ nghĩa lưu trữ hiện tại

## 1. Phạm vi và cách đọc bằng chứng

- Đây là **phân tích tĩnh hiện trạng implementation**, không phải thiết kế web mới, không phải kết quả introspection database đang chạy. Không chạy ứng dụng, SQL, migration, build, formatter hay test. Tất cả kết luận runtime vẫn **chưa xác minh runtime**; các test bên dưới chỉ là inventory mã kiểm thử đã có, không được tuyên bố pass.
- Phạm vi: toàn bộ `BusTicketSaleSystem/Data/*.sql`, `DatabaseMigrator.cs`, `DatabaseHelper.cs`, `SqlResilience.cs`, `AuditLog.cs`, toàn bộ ba file C# trong `Models`, và các test dữ liệu/concurrency/security. Đọc thêm phần liên quan của `AdminDatabaseService` và `PaymentService` để phân biệt ràng buộc DB với kiểm tra service. Không thay đổi code hay nghiệp vụ.
- Đường dẫn chứng cứ đều tương đối root repository. Trong phần dictionary, **NN** = `NOT NULL`, **NULL** = nullable, **—** = không có DEFAULT khai báo, **identity** = IDENTITY(1,1), **PK** = khóa chính. Các PK/UNIQUE khai báo không đặt tên trong `Schema1.sql` được SQL Server tự đặt tên; không tự đặt một tên giả trong báo cáo.
- Dictionary dưới đây là **kết quả dự kiến trên database tạo mới đi hết 18 bước**. Database legacy có thể khác: initial schema có thể được ghi nhận nhưng bỏ qua chỉ vì đã có Accounts; một số CHECK chỉ thêm khi dữ liệu cũ hợp lệ; nhiều migration chỉ kiểm tra tên/column tồn tại chứ không sửa lại definition. Vì vậy history không thay thế khảo sát `sys.columns/sys.indexes/sys.foreign_keys/sys.check_constraints` ở môi trường triển khai. Bằng chứng: `BusTicketSaleSystem/Data/DatabaseMigrator.cs:169-208`, `BusTicketSaleSystem/Data/Migration_005_Settlement.sql:41-54`.

## 2. Inventory và đồ thị quan hệ

Có **10 bảng nghiệp vụ**: Accounts, Routes, Buses, Trips, Seats, Tickets, TicketSeats, Payments, BusCompanies, AuditLog; thêm **1 bảng kỹ thuật** `__DatabaseMigrations`. Có **1 view có schemabinding/index** `UX_ActiveTicketSeats`; **2 trigger còn hiệu lực sau migration** trên TicketSeats. Không có stored procedure hay reporting view được định nghĩa trong tập `Data/*.sql` đã đọc; reporting đang là SQL inline trong C#.

```text
Accounts 1 ── n Tickets n ── 1 Trips n ── 1 Routes
    │              │             │
    │              │             └── n:1 Buses 1 ── n Seats
    │              └── 1:n TicketSeats n ─────────── 1 Seats
    └── 1:n Payments (ConfirmedBy / RefundApprovedBy, optional)
Tickets 1 ── n Payments

Buses.CompanyName ... BusCompanies.CompanyName  (join tên, KHÔNG FK)
Buses.RouteId ...... Routes.RouteId             (tham chiếu logic, KHÔNG FK)
AuditLog.ActorAccountId ... Accounts.AccountId  (KHÔNG FK)
Tickets.SeatId ...... legacy                    (NULL, FK đã bị gỡ)
```

Các FK được khai báo không có `ON DELETE` hoặc `ON UPDATE`, nên dùng hành vi SQL Server mặc định **NO ACTION**, không cascade. `Tickets.SeatId` là ngoại lệ lịch sử đã drop FK; không được xem là quan hệ bảo toàn hiện tại. Bằng chứng: `BusTicketSaleSystem/Data/Schema1.sql:36-69`, `BusTicketSaleSystem/Data/Schema_Payments.sql:7-27`, `BusTicketSaleSystem/Data/Migration_005_Settlement.sql:33-38`, `BusTicketSaleSystem/Data/Migration_006_MultiSeatTicket.sql:15-24,96-133`.

Không có bảng riêng cho station, driver, passenger, operator-user membership, review, amenities, route stops, booking group, refund transaction, session/token. Các khái niệm tương ứng hoặc nằm dạng text/column, hoặc không có mô hình lưu trữ trong schema này. Company không phải actor có tài khoản riêng; role DB chỉ Customer/Admin (`Schema1.sql:7-17`).

## 3. Data dictionary đầy đủ

### 3.1 Accounts — tài khoản đăng nhập

Nguồn: `BusTicketSaleSystem/Data/Schema1.sql:7-19`; phần mở rộng `BusTicketSaleSystem/Data/Migration_010_LoginThrottling.sql:15-21`.

| Cột | SQL type | NULL | DEFAULT | Khóa / nghĩa |
|---|---|---|---|---|
| AccountId | INT identity | NN | identity | PK |
| Username | NVARCHAR(50) | NN | — | UNIQUE; định danh đăng nhập |
| PasswordHash | NVARCHAR(255) | NN | — | Chuỗi hash tự mô tả; DB không CHECK format |
| FullName | NVARCHAR(100) | NN | — | Tên hồ sơ, khác PassengerName snapshot trên vé |
| Phone | NVARCHAR(20) | NULL | — | Không UNIQUE |
| Email | NVARCHAR(100) | NULL | — | Unique filtered khi khác NULL |
| Role | NVARCHAR(20) | NN | `N'Customer'` | CHECK Customer/Admin |
| CreatedAt | DATETIME2 | NN | SYSUTCDATETIME() | UTC theo quy ước |
| FailedLoginCount | INT | NN | 0 (`DF_Accounts_FailedLoginCount`) | Số sai liên tiếp; không CHECK >=0 |
| LockoutEndsAt | DATETIME2 | NULL | — | Hạn khóa đăng nhập |

- PK(AccountId); UNIQUE(Username); `UX_Accounts_Email(Email) WHERE Email IS NOT NULL`; `CK_Accounts_Role` IN(Customer,Admin). Trên fresh schema DEFAULT Role không có tên cố định; migration thêm Role cho legacy dùng tên `DF_Accounts_Role` (`Migration_002_Admin.sql:3-7`).
- Không CHECK độ mạnh mật khẩu/email/phone ở DB. Không quy định COLLATE riêng trong các script: case/accent sensitivity của username/email phụ thuộc collation DB thực tế, chưa xác minh.
- UNIQUE email không coi chuỗi rỗng là NULL. Service chuyển **null** thành DBNull nhưng không chuẩn hóa mọi chuỗi trắng/rỗng thành NULL (`DatabaseHelper.cs:157-188`).

### 3.2 Routes — tuyến

Nguồn: `BusTicketSaleSystem/Data/Schema1.sql:21-27`; `BusTicketSaleSystem/Data/Migration_007_BusCompanyAndRegistration.sql:26-31`.

| Cột | SQL type | NULL | DEFAULT | Nghĩa |
|---|---|---|---|---|
| RouteId | INT identity | NN | identity | PK |
| Origin | NVARCHAR(100) | NN | — | Điểm đầu dạng text |
| Destination | NVARCHAR(100) | NN | — | Điểm cuối dạng text |
| DistanceKm | DECIMAL(6,1) | NULL | — | Khoảng cách; DB không CHECK không âm |
| IntermediateStops | NVARCHAR(500) | NULL | — | Danh sách dừng dạng text, thường phân tách dấu phẩy |
| DefaultSchedule | NVARCHAR(100) | NULL | — | Ghi chú lịch dạng text; không phải bảng giờ chạy |

- `CK_Routes_DifferentStops CHECK (Origin <> Destination)`; không UNIQUE cặp Origin/Destination; `IX_Routes_Origin_Destination(Origin,Destination)` là index thường (`Migration_003_PerformanceIndexes.sql:3-4`).
- Không FK địa phương/province. Khác biệt chữ hoa/dấu theo SQL collation; service lại dùng `OrdinalIgnoreCase` khi kiểm tra bằng C# (`AdminDatabaseService.cs:321-328`).

### 3.3 Buses — xe và hồ sơ đăng ký

Nguồn baseline `BusTicketSaleSystem/Data/Schema1.sql:29-34`; các cột mở rộng `BusTicketSaleSystem/Data/Migration_007_BusCompanyAndRegistration.sql:34-84`; giá chặng `BusTicketSaleSystem/Data/Migration_014_StagePrice.sql:16-17`.

| Cột | SQL type | NULL | DEFAULT | Nghĩa / quan hệ |
|---|---|---|---|---|
| BusId | INT identity | NN | identity | PK |
| LicensePlate | NVARCHAR(20) | NN | — | UNIQUE biển số |
| SeatCount | INT | NN | 40 | CHECK >0 |
| BusType | NVARCHAR(50) | NULL | — | Chuỗi loại xe, không CHECK enum |
| CompanyName | NVARCHAR(100) | NULL | — | Tên nhà xe, **không FK** |
| DriverName | NVARCHAR(100) | NULL | — | Tên tài xế, không bảng Driver |
| DriverPhone | NVARCHAR(20) | NULL | — | Liên hệ tài xế |
| DepartureTimeNote | NVARCHAR(100) | NULL | — | Khung giờ dạng text |
| RouteId | INT | NULL | — | Tuyến đăng ký, **không FK** |
| IntermediateStops | NVARCHAR(500) | NULL | — | Ghi chú điểm dừng ở cấp xe |
| Make | NVARCHAR(50) | NULL | — | Hãng sản xuất |
| Model | NVARCHAR(50) | NULL | — | Dòng xe |
| ManufactureYear | INT | NULL | — | Không CHECK phạm vi năm |
| Color | NVARCHAR(50) | NULL | — | Màu xe |
| RegistrationNumber | NVARCHAR(50) | NULL | — | Số đăng ký; không UNIQUE |
| InspectionNumber | NVARCHAR(50) | NULL | — | Số kiểm định; không UNIQUE |
| RegistrationDate | DATE | NULL | — | Ngày đăng ký |
| InspectionExpiryDate | DATE | NULL | — | Hạn kiểm định; không CHECK so với ngày hiện tại |
| InsuranceExpiryDate | DATE | NULL | — | Hạn bảo hiểm; không CHECK thứ tự thời gian |
| RegisteredEntity | NVARCHAR(150) | NULL | — | Pháp nhân đăng ký dạng text |
| OperationType | NVARCHAR(50) | NULL | — | Loại hình khai thác, không CHECK enum |
| StagePrice | DECIMAL(10,0) | NULL | — | Giá chặng; không CHECK >=0 |

PK BusId, UNIQUE LicensePlate, CHECK SeatCount>0 là toàn bộ ràng buộc trực tiếp trên Buses trong nguồn schema. Không có cột `FullPrice`/`FarePrice` ở Buses: những tên này có ở DTO/input, không được nhầm với persisted column (`Models/AdminModels.cs:34-35,68-73`). Số dòng Seats không được DB ràng buộc bằng SeatCount.

### 3.4 BusCompanies — danh mục nhà xe

Nguồn: `BusTicketSaleSystem/Data/Migration_007_BusCompanyAndRegistration.sql:7-23`; `BusTicketSaleSystem/Data/Migration_015_StationOperationsAndCommission.sql:40-45`.

| Cột | SQL type | NULL | DEFAULT | Nghĩa |
|---|---|---|---|---|
| CompanyId | INT identity | NN | identity | PK_BusCompanies |
| CompanyName | NVARCHAR(100) | NN | — | UQ_BusCompanies_Name |
| Hotline | NVARCHAR(20) | NULL | — | Hotline |
| Rating | DECIMAL(2,1) | NN | 4.8 | DF_BusCompanies_Rating; không CHECK 0..5 |
| ReviewCount | INT | NN | 1250 | DF_BusCompanies_ReviewCount; không CHECK >=0 |
| OperatingArea | NVARCHAR(200) | NN | `Hà Nội - Thái Nguyên - Lào Cai` | DF_BusCompanies_Area; text mô tả vùng |
| Description | NVARCHAR(MAX) | NULL | — | Nội dung giới thiệu |
| Amenities | NVARCHAR(500) | NULL | — | Tiện ích phân tách dấu phẩy |
| LogoText | NVARCHAR(10) | NN | `SV` | DF_BusCompanies_Logo |
| BrandColor | NVARCHAR(20) | NN | `#0F4C81` | DF_BusCompanies_Color; không CHECK mã màu |
| CreatedAt | DATETIME2 | NN | SYSUTCDATETIME() | DF_BusCompanies_CreatedAt |
| CommissionRate | DECIMAL(5,4) | NN | 0.1000 | DF_BusCompanies_CommissionRate; **không CHECK 0..1** |

Không FK từ Buses vào CompanyId/CompanyName. Quan hệ nhà xe được join bằng tên (có query TRIM và có query không TRIM), nên rename/khác khoảng trắng có thể làm mapping khác nhau: `DatabaseHelper.cs:387,494,628`; `AdminDatabaseService.cs:284`.

### 3.5 Trips — chuyến chạy cụ thể

Nguồn: `BusTicketSaleSystem/Data/Schema1.sql:36-44`; `BusTicketSaleSystem/Data/Migration_014_StagePrice.sql:20-21`; `BusTicketSaleSystem/Data/Migration_015_StationOperationsAndCommission.sql:19-37`.

| Cột | SQL type | NULL | DEFAULT | Nghĩa / khóa |
|---|---|---|---|---|
| TripId | INT identity | NN | identity | PK |
| RouteId | INT | NN | — | FK Routes(RouteId) |
| BusId | INT | NN | — | FK Buses(BusId) |
| DepartureTime | DATETIME2 | NN | — | Giờ khởi hành, service quy ước UTC |
| ArrivalTime | DATETIME2 | NN | — | Giờ đến |
| Price | DECIMAL(10,0) | NN | — | Đơn giá toàn tuyến; CHECK >=0 |
| StagePrice | DECIMAL(10,0) | NULL | — | Giá chặng; không CHECK |
| Status | NVARCHAR(20) | NN | Scheduled | DF_Trips_Status; CK_Trips_Status |

- `CK_Trips_TimeOrder`: ArrivalTime > DepartureTime.
- `CK_Trips_Status`: Scheduled/Departed/Cancelled. Không trigger tự cập nhật trạng thái theo đồng hồ trong schema; migration 015 có UPDATE một lần những chuyến Scheduled đã quá giờ thành Departed.
- `IX_Trips_DepartureTime(DepartureTime)`; `UX_Trips_Bus_Departure(BusId,DepartureTime)` UNIQUE không filter (`Migration_003_PerformanceIndexes.sql:5-6`; `Migration_011_PaymentIntegrity.sql:30-33`). Index trùng giờ vẫn áp dụng cho chuyến Cancelled.
- DB chỉ cấm **cùng xe, cùng giờ bắt đầu**; không có exclusion constraint cho mọi khoảng thời gian chồng lấn. Không CHECK DepartureTime ở tương lai, không ràng buộc Status khớp đồng hồ.

### 3.6 Seats — sơ đồ ghế vật lý của xe

Nguồn: `BusTicketSaleSystem/Data/Schema1.sql:46-51`.

| Cột | SQL type | NULL | DEFAULT | Nghĩa |
|---|---|---|---|---|
| SeatId | INT identity | NN | identity | PK |
| BusId | INT | NN | — | FK Buses(BusId), NO ACTION |
| SeatNumber | NVARCHAR(10) | NN | — | Nhãn ghế dạng text |

`UQ_Seats_Bus_Number UNIQUE(BusId,SeatNumber)`. Không CHECK nhãn ghế là số/nhỏ hơn SeatCount. SeatId là identity toàn bảng, không phải số ghế hiển thị. Seat map query sắp xếp SeatId, không sort số SeatNumber (`DatabaseHelper.cs:669-689`).

### 3.7 Tickets — một booking nhiều ghế, một hành khách đại diện

Nguồn baseline `BusTicketSaleSystem/Data/Schema1.sql:53-69`; timestamp `Migration_005_Settlement.sql:17-19`; multi-seat `Migration_006_MultiSeatTicket.sql:28-40,87-109`.

| Cột | SQL type | NULL | DEFAULT | Nghĩa |
|---|---|---|---|---|
| TicketId | INT identity | NN | identity | PK |
| TicketCode | NVARCHAR(30) | NN | — | UNIQUE; mã booking |
| TripId | INT | NN | — | FK Trips(TripId) |
| SeatId | INT | NULL | — | Legacy; FK bị drop, code booking mới không ghi |
| AccountId | INT | NN | — | FK Accounts(AccountId), chủ booking |
| PassengerName | NVARCHAR(100) | NN | — | Snapshot tên hành khách; không FK hồ sơ |
| PassengerPhone | NVARCHAR(20) | NULL | — | Snapshot phone hành khách |
| Price | DECIMAL(10,0) | NN | — | **Đơn giá mỗi ghế tại lúc đặt**, CHECK >=0 |
| Status | NVARCHAR(20) | NN | Đã đặt | CK_Tickets_Status |
| PaymentStatus | NVARCHAR(20) | NN | Chưa thanh toán | CK_Tickets_PaymentStatus |
| PaymentExpiresAt | DATETIME2 | NULL | — | Hạn giữ chỗ chưa thanh toán |
| BookingTime | DATETIME2 | NN | SYSUTCDATETIME() | UTC |
| CancelledAt | DATETIME2 | NULL | — | Timestamp hủy |
| UsedAt | DATETIME2 | NULL | — | Timestamp dùng vé/check-in/xuất bến |
| SeatCount | INT | NN | 1 | DF_Tickets_SeatCount; **không CHECK >0**, không CHECK bằng số dòng TicketSeats |

- `CK_Tickets_Status`: Đã đặt / Đã thanh toán / Đã hủy / Đã sử dụng.
- `CK_Tickets_PaymentStatus`: Chưa thanh toán / Đã thanh toán / Hoàn tiền.
- Không CHECK liên cột bắt Status/PaymentStatus/CancelledAt/UsedAt/PaymentExpiresAt luôn đồng bộ. DB không yêu cầu booking có ít nhất một TicketSeats.
- Fresh schema có UNIQUE TicketCode tự sinh tên; migration 001 thêm tiếp index `UX_Tickets_TicketCode` vì kiểm tra đúng tên đó, tạo hai cơ chế uniqueness cùng cột trên fresh DB (`Schema1.sql:55`; `Migration_001.sql:18`).
- Index thường: `IX_Tickets_Account_Status(AccountId,Status,BookingTime DESC)`; `IX_Tickets_PaymentExpiresAt(PaymentExpiresAt) WHERE PaymentExpiresAt IS NOT NULL`; `IX_Tickets_Trip_Status(TripId,Status) INCLUDE(PaymentStatus,PaymentExpiresAt)` (`Migration_003_PerformanceIndexes.sql:7-8`; `Migration_005_Settlement.sql:103-107`; `Migration_013_SeatAvailabilityIndexes.sql:19-22`).
- `UX_Tickets_Active_Trip_Seat` baseline/settlement **đã bị drop**, không còn là protection hiện tại (`Migration_006_MultiSeatTicket.sql:43-49`).
- Legacy migration tạo mã `LEGACY-` + TicketId, lấy PassengerName từ Accounts.FullName và Price từ Trips.Price cho giá trị NULL rồi đổi sang NOT NULL. Đây là backfill, không phải dữ liệu gốc lịch sử được phục hồi chính xác (`Migration_001.sql:10-17`).

### 3.8 TicketSeats — ghế thuộc booking

Nguồn: `BusTicketSaleSystem/Data/Migration_006_MultiSeatTicket.sql:12-34`.

| Cột | SQL type | NULL | DEFAULT | Nghĩa |
|---|---|---|---|---|
| TicketSeatId | INT identity | NN | identity | PK_TicketSeats |
| TicketId | INT | NN | — | FK_TicketSeats_Ticket → Tickets(TicketId) |
| SeatId | INT | NN | — | FK_TicketSeats_Seat → Seats(SeatId) |

- `UQ_TicketSeats_TicketSeat UNIQUE(TicketId,SeatId)` cấm lặp cùng ghế trong booking.
- `IX_TicketSeats_Seat(SeatId) INCLUDE(TicketId)` (`Migration_013_SeatAvailabilityIndexes.sql:25-27`).
- Không có cột TripId: trip lấy qua Tickets. Không có cột giá/khách riêng từng ghế: tất cả ghế trong một booking dùng PassengerName, Price và PaymentStatus chung.
- Hai trigger hiện tại và indexed view được trình bày tại mục 5.

### 3.9 Payments — sổ thanh toán

Nguồn: `BusTicketSaleSystem/Data/Schema_Payments.sql:5-38`; `BusTicketSaleSystem/Data/Migration_005_Settlement.sql:22-38`.

| Cột | SQL type | NULL | DEFAULT | Nghĩa |
|---|---|---|---|---|
| PaymentId | INT identity | NN | identity | PK_Payments |
| TicketId | INT | NN | — | FK_Payments_Tickets → Tickets |
| TransactionNo | NVARCHAR(100) | NULL | — | Tham chiếu nội bộ/gateway, UNIQUE khi khác NULL |
| Provider | NVARCHAR(30) | NN | VNPay | DF_Payments_Provider; **không CHECK enum** |
| ProviderTransactionNo | NVARCHAR(100) | NULL | — | Tham chiếu phía provider; không UNIQUE |
| OrderInfo | NVARCHAR(255) | NULL | — | Mô tả đơn |
| PaymentMethod | NVARCHAR(50) | NN | — | CHECK VNPay/MoMo/BankTransfer/Cash |
| Amount | DECIMAL(18,2) | NN | — | CHECK >=0 |
| Status | NVARCHAR(20) | NN | Pending | DF_Payments_Status; CHECK Pending/Success/Failed/Refunded |
| ResponseCode | NVARCHAR(20) | NULL | — | Mã kết quả provider |
| VerifiedAt | DATETIME2 | NULL | — | Thời điểm xác minh |
| RawResponse | NVARCHAR(MAX) | NULL | — | Dữ liệu callback; không phải bảng event riêng |
| RefundRequestedAt | DATETIME2 | NULL | — | Yêu cầu hoàn, chưa đồng nghĩa đã trả tiền |
| CreatedAt | DATETIME2 | NN | SYSUTCDATETIME() | DF_Payments_CreatedAt |
| PaidAt | DATETIME2 | NULL | — | Thời điểm settlement |
| ConfirmedByAccountId | INT | NULL | — | FK_Payments_ConfirmedBy → Accounts |
| RefundApprovedByAccountId | INT | NULL | — | FK_Payments_RefundApprovedBy → Accounts |
| RefundedAt | DATETIME2 | NULL | — | Thời điểm đánh dấu đã hoàn |
| Notes | NVARCHAR(500) | NULL | — | Ghi chú xử lý |

Ràng buộc/index:
- `CK_Payments_Method`, `CK_Payments_Amount`, `CK_Payments_Status` như trên.
- `IX_Payments_TicketId(TicketId,CreatedAt DESC)`; `UX_Payments_TransactionNo(TransactionNo) WHERE TransactionNo IS NOT NULL` (`Schema_Payments.sql:31-37`).
- `IX_Payments_Status(Status,CreatedAt DESC)` (`Migration_005_Settlement.sql:97-100`).
- `UX_Payments_Live_Ticket(TicketId) WHERE Status IN ('Pending','Success')`: một booking tối đa một payment đang sống, nhưng có thể có nhiều dòng Failed/Refunded lịch sử (`Migration_011_PaymentIntegrity.sql:23-27`).
- Không CHECK Amount = Tickets.Price × SeatCount. Không constraint provider transaction uniqueness hoặc mapping Provider↔PaymentMethod; không CHECK timestamp theo Status; role của ConfirmedByAccountId/RefundApprovedByAccountId không được FK kiểm tra.
- **Có trong schema không đồng nghĩa channel enabled**: CHECK cho MoMo/VNPay chỉ là miền giá trị lưu trữ; trạng thái khả dụng của luồng người dùng phải đọc báo cáo payment/UI, không suy ra từ bảng.

### 3.10 AuditLog — dấu vết hành động

Nguồn: `BusTicketSaleSystem/Data/Migration_012_AuditLog.sql:15-32`.

| Cột | SQL type | NULL | DEFAULT | Nghĩa |
|---|---|---|---|---|
| AuditId | BIGINT identity | NN | identity | PK_AuditLog |
| OccurredAt | DATETIME2 | NN | SYSUTCDATETIME() | DF_AuditLog_OccurredAt |
| ActorAccountId | INT | NULL | — | Actor từ CurrentUser; **không FK** |
| Action | NVARCHAR(60) | NN | — | Tên hành động, không CHECK |
| TargetType | NVARCHAR(40) | NN | — | Loại đích dạng text |
| TargetId | NVARCHAR(60) | NULL | — | ID đích dạng text, không FK |
| Detail | NVARCHAR(400) | NULL | — | Chi tiết; helper cắt tối đa 400 ký tự |

Index `IX_AuditLog_OccurredAt(OccurredAt DESC) INCLUDE(Action,ActorAccountId)`. Append-only chỉ là **quy ước**, không trigger/permission DDL cấm UPDATE/DELETE trong schema. Actor NULL không bị DB hạn chế chỉ dành background, dù comment nói như vậy (`Migration_012_AuditLog.sql:8-10,21`).

### 3.11 __DatabaseMigrations — lịch sử áp dụng script

Nguồn: `BusTicketSaleSystem/Data/DatabaseMigrator.cs:125-149`.

| Cột | SQL type | NULL | DEFAULT | Nghĩa |
|---|---|---|---|---|
| MigrationId | INT identity | NN | identity | PK_DatabaseMigrations |
| ScriptName | NVARCHAR(255) | NN | — | UQ_DatabaseMigrations_ScriptName; logical name, không nhất thiết filename |
| AppliedAt | DATETIME2 | NN | SYSUTCDATETIME() | DF_DatabaseMigrations_AppliedAt |
| Checksum | CHAR(64) | NULL | — | SHA-256 hex script đã normalize; NULL history cũ hoặc initial bị skip |

Bảng tạo ngoài transaction từng migration. Không FK tới file hay migration dependency. Checksum NULL được chấp nhận, không tự backfill bằng checksum hiện tại (`DatabaseMigrator.cs:154-166,261-266`).

## 4. Migration history theo đúng thứ tự thực thi

Thứ tự authoritative là `BusTicketSaleSystem/Data/DatabaseMigrator.cs:33-50`, **không phải chỉ sort số trong filename**.

| Bước / ScriptName | File | Hiệu ứng thực tế và dữ liệu bị tác động |
|---|---|---|
| 001_InitialSchema | Schema1.sql | Tạo Accounts/Routes/Buses/Trips/Seats/Tickets, PK/FK/check/index/trigger baseline. Nếu đã có Accounts: không chạy script, chỉ record history checksum NULL (`DatabaseMigrator.cs:177-185`). |
| 002_TicketFields | Migration_001.sql | Thêm các cột TicketCode/PassengerName/PassengerPhone/Price/PaymentStatus/CancelledAt thiếu; backfill mã, tên, giá, trạng thái; siết bốn cột NOT NULL; thêm UX_Tickets_TicketCode (`:3-18`). |
| 003_AdminRole | Migration_002_Admin.sql | Thêm Role mặc định Customer nếu thiếu; thêm CHECK; câu UPDATE promote admin chỉ là comment, **không seed admin** (`:3-14`). |
| 004_Payments | Schema_Payments.sql | Tạo ledger nếu chưa có, check method/amount/status; index theo ticket và transaction ref (`:5-37`). |
| 005_PerformanceIndexes | Migration_003_PerformanceIndexes.sql | Ba index Routes endpoints, Trips departure, Tickets account/status/booking (`:3-8`). |
| 006_PaymentExpiry | Migration_004_PaymentExpiry.sql | Thêm PaymentExpiresAt + các metadata Payments thiếu. Vé Đã đặt/Chưa thanh toán thiếu hạn được đặt BookingTime+15 phút, kể cả hạn đã qua (`:3-15`). |
| 007_DevelopmentSeedData | SeedData.sql | Bổ sung 7 tuyến, 3 xe, ghế cho xe hiện có; không account/payment/ticket, không chuyến trong script này (`:5-41`). Tên 'Development' không đi kèm nhánh if-environment ở MigrateAsync. |
| 008_SettlementHardening | Migration_005_Settlement.sql | UsedAt; 4 cột settlement/refund; 2 account FK; CHECK vé chỉ khi dữ liệu cũ hợp lệ; thay uniqueness cũ UQ_Trip_Seat/UX_Tickets_Active_Trip_Seat bằng filtered unique gồm Used; thêm trigger thiếu; index ledger và expiry (`:17-107`). |
| 009_MultiSeatTicket | Migration_006_MultiSeatTicket.sql | Tạo TicketSeats; chép SeatId cũ vào junction, thêm SeatCount default1; drop index uniqueness trên Tickets; tạo 2 trigger; drop seat-bus trigger cũ và FK Tickets.SeatId; đổi SeatId nullable. **Không có lệnh re-add FK mặc dù comment nhắc re-add** (`:12-133`). |
| 010_BusCompanyAndRegistration | Migration_007_BusCompanyAndRegistration.sql | Tạo BusCompanies, thêm route stops/schedule, 17 cột hồ sơ xe; seed 5 công ty; backfill route và bus text/giấy tờ (`:7-286`). |
| 011_SeatUniqueness | Migration_008_SeatUniqueness.sql | Nếu đã có hai vé active cùng trip/seat → THROW 51020, không tự xóa/sửa dữ liệu; tạo schemabound view và unique clustered index chống bán trùng (`:29-57`). |
| 012_TriggerRollbackFix | Migration_009_TriggerRollbackFix.sql | Drop/recreate hai trigger TicketSeats, giữ validation nhưng bỏ ROLLBACK TRANSACTION trước THROW; caller chịu rollback (`:24-72`). |
| 013_LoginThrottling | Migration_010_LoginThrottling.sql | FailedLoginCount default0 + LockoutEndsAt nullable (`:15-21`). |
| 014_PaymentIntegrity | Migration_011_PaymentIntegrity.sql | Unique filtered payment live/ticket; unique bus/departure. Duplicate sẵn có làm tạo index thất bại, không tự dọn (`:23-33`). |
| 015_AuditLog | Migration_012_AuditLog.sql | Tạo bảng AuditLog và index thời gian (`:15-32`). |
| 016_SeatAvailabilityIndexes | Migration_013_SeatAvailabilityIndexes.sql | Covering indexes Tickets trip/status và TicketSeats seat (`:19-27`). |
| 017_StagePrice | Migration_014_StagePrice.sql | Thêm nullable DECIMAL(10,0) StagePrice vào Buses/Trips nếu chưa có (`:16-21`). Không backfill giá. |
| 018_StationOperationsAndCommission | Migration_015_StationOperationsAndCommission.sql | Trips.Status default Scheduled + CHECK; đổi Scheduled đã qua DepartureTime thành Departed; CommissionRate default0.1000 (`:19-45`). Không trigger SQL tự hủy vé chỉ vì đổi Trips.Status. |

### 4.1 Transaction, checksum, replay và rollback

- Migrator có SemaphoreSlim một process; không có distributed migration lock trong implementation đã đọc (`DatabaseMigrator.cs:18,24-31`).
- Ensure database lấy catalog từ cấu hình, escape tên; không phải cứ file ghi `USE BusTicketSaleSystem` là chạy vào DB đó. LoadScript loại các dòng USE và mẫu IF DB_ID...CREATE DATABASE rồi tách GO theo regex (`DatabaseMigrator.cs:110-123,211-239,268-274`).
- Mỗi migration mới: ReadCommitted transaction, chạy mọi batch, INSERT history với checksum, commit chung; lỗi rollback rồi throw; MigrateAsync trả false/LastError cho lỗi không cancellation (`DatabaseMigrator.cs:53-62,188-208`). Các migration trước đã commit vẫn giữ nguyên khi migration sau lỗi.
- Đã applied + checksum có giá trị: hash lại script normalized, mismatch → fail rõ ràng; không silently reapply. Checksum là của chuỗi sau normalization, không phải hash raw file (`DatabaseMigrator.cs:147-166,211-216`).
- Đã applied + checksum NULL: chấp nhận; không chứng minh script hiện tại từng chạy. Initial skip chỉ kiểm tra Accounts có tồn tại, không validate toàn schema. CHECK Tickets ở migration 005 có thể bị **bỏ qua** nếu có status ngoài miền. Đây là khác biệt legacy phải giữ trong inventory (`DatabaseMigrator.cs:159,177-185`; `Migration_005_Settlement.sql:44-54`).
- Không có `Down()` hay API rollback tự động. Một số migration 008..015 có comment Reversal, đó là hướng dẫn DDL thủ công, không chương trình rollback. Việc backfill dữ liệu/đổi status/seed không được phục hồi tự động. Migration 006 đã copy dữ liệu, drop index/FK/trigger; không thể coi rollback chỉ là đổi một cờ.
- Script DevSeedTrips **không có row lịch sử migration**, không checksum-check trong history. EnsureUpcomingTrips thực thi không truyền transaction; có thể thêm lịch ngoài 18 bước (`DatabaseMigrator.cs:72-108`). Phần vận hành startup thuộc báo cáo riêng; ở đây chỉ ghi side effect dữ liệu.

## 5. Ràng buộc ghế authoritative và giới hạn

### 5.1 Indexed view đang bảo vệ gì

`dbo.UX_ActiveTicketSeats` có đúng hai cột `TripId`, `SeatId` lấy từ Tickets JOIN TicketSeats, lọc Tickets.Status IN(Đã đặt, Đã thanh toán, Đã sử dụng). Unique clustered index `UX_ActiveTicketSeats_Trip_Seat(TripId,SeatId)` làm mỗi cặp chỉ xuất hiện một lần. Định nghĩa: `BusTicketSaleSystem/Data/Migration_008_SeatUniqueness.sql:42-57`.

- Đây là uniqueness DB, không phụ thuộc caller chạy UI/service hay Serializable. Vé Used vẫn chiếm ghế. Vé Cancelled không nằm view, nên ghế được bán lại mà giữ lịch sử.
- View **không lọc PaymentExpiresAt** hay PaymentStatus. Vé Đã đặt đã hết hạn nhưng chưa đổi Status vẫn bị index coi là chiếm ghế. Availability query lại bỏ hold hết hạn. Booking gọi sweep trước transaction để thu hẹp lệch này (`DatabaseHelper.cs:724-739`).
- View duy trì khi base table cập nhật nên đổi trip/status/seat gây trùng cặp vẫn bị uniqueness từ chối. Nhưng uniqueness không bảo đảm ghế thuộc bus, SeatCount chính xác, hay trip chưa xuất bến.
- Duplicate cũ làm migration fail với 51020, không auto-cancel bất kỳ booking nào (`Migration_008_SeatUniqueness.sql:29-38`).

### 5.2 Trigger cuối cùng

Nguồn định nghĩa cuối: `BusTicketSaleSystem/Data/Migration_009_TriggerRollbackFix.sql:27-72`.

1. `TR_TicketSeats_AntiDoubleSeat`, **AFTER INSERT**: với inserted booking active, tìm ticket khác cùng trip/seat active và (`PaymentStatus=Đã thanh toán` OR hạn giữ chỗ > UTC now), THROW 51002. Đây là defense-in-depth; index có predicate rộng hơn về hold hết hạn.
2. `TR_TicketSeats_ValidateSeatBus`, **AFTER INSERT**: join inserted→Tickets→Trips và Seats; nếu Trips.BusId≠Seats.BusId, THROW 51001.
3. Hai trigger không tự ROLLBACK trong phiên bản cuối. Booking bắt lỗi rồi rollback; `RollbackQuietlyAsync` bỏ qua InvalidOperationException nếu server đã kết thúc transaction (`DatabaseHelper.cs:836-871`).
4. **Không có AFTER UPDATE** ở hai trigger cuối. Thay TicketSeats.SeatId/TicketId, Trips.BusId hoặc Tickets.TripId qua direct SQL không được seat/bus trigger này kiểm tra lại. FK chỉ chứng minh ID tồn tại; indexed view chỉ chống trùng. Admin UpdateTrip cấm đổi bus khi bất kỳ vé đã tồn tại, nhưng đây là service guard, không constraint DB (`AdminDatabaseService.cs:1122-1124`).
5. Tickets.SeatId legacy không có FK/trigger còn hiệu lực; không dùng nó làm source of truth cho website hay đối soát ghế. Source hiện tại là TicketSeats (`Migration_006_MultiSeatTicket.sql:92-133`; `DatabaseHelper.cs:1142-1144`).

## 6. Taxonomy trạng thái, thời gian, giá và ownership

### 6.1 Các miền giá trị persist

| Trường | Miền DB | Ngữ nghĩa thực tế |
|---|---|---|
| Accounts.Role | Customer, Admin | Vai trò ứng dụng; không phải SQL database role |
| Trips.Status | Scheduled, Departed, Cancelled | Chuyến còn lên lịch / đã xuất bến / bị hủy |
| Tickets.Status | Đã đặt, Đã thanh toán, Đã hủy, Đã sử dụng | Giữ chỗ / đã thu tiền / giải phóng ghế / đã dùng |
| Tickets.PaymentStatus | Chưa thanh toán, Đã thanh toán, Hoàn tiền | Trạng thái tiền riêng, không đồng nhất Status vé |
| Payments.Status | Pending, Success, Failed, Refunded | Trạng thái ledger một lần thanh toán |
| Payments.PaymentMethod | VNPay, MoMo, BankTransfer, Cash | Tập giá trị DB cho phép, không đảm bảo flow đều enabled |
| Payments.Provider | Không CHECK | Giá trị provider do service ghi, DEFAULT VNPay |
| BusType/OperationType/Action/TargetType | Không CHECK | Text tự do ở DB, dù UI có danh sách lựa chọn |

Nguồn: `Schema1.sql:16,67-68`; `Schema_Payments.sql:24-26`; `Migration_015_StationOperationsAndCommission.sql:27-30`; constants C# `Models/DomainModels.cs:8-26`. Không có C# enum persist trong ba file Models: các trạng thái chủ yếu string literal/constants.

### 6.2 Tách trạng thái vé khỏi trạng thái tiền

- Booking mới: Tickets.Status=Đã đặt, PaymentStatus=Chưa thanh toán, PaymentExpiresAt=UTC now+15 phút, Price sao chép Trips.Price, SeatCount=số ghế; chưa tự tạo Payments trong DatabaseHelper (`DatabaseHelper.cs:757-774`).
- Hủy vé chưa thanh toán: Status=Đã hủy, CancelledAt=UTC now, xóa hạn hold; Payments.Pending→Failed; không sửa PaymentStatus sang Hoàn tiền (`DatabaseHelper.cs:888-926`).
- Hủy vé đã thanh toán được điều phối sang RequestRefundAsync. Request refund trước giờ chạy, owner đúng, Status/PaymentStatus đều Đã thanh toán; chuyển Status=Đã hủy, giữ PaymentStatus=Đã thanh toán, đóng hold, đặt RefundRequestedAt trên payment Success. Nghĩa là **ghế đã giải phóng nhưng tiền chưa hoàn** (`DatabaseHelper.cs:883-886,979-1049`).
- RefundRequestedAt khác RefundedAt: request không phải approval. Approval PaymentService ghi Payments.Ref​unded, RefundedAt, RefundApprovedByAccountId và Tickets.PaymentStatus=Hoàn tiền; flow cụ thể ở báo cáo payment (`Services/PaymentService.cs:547-589`).
- DB CHECK chỉ cấm từ ngoài miền, không cấm tổ hợp vô lý như Status=Used mà PaymentStatus=Unpaid. Tính nhất quán liên trạng thái hiện đến từ SQL service transaction, không từ CHECK đa cột.

### 6.3 Giá, tổng tiền và lịch sử

- `Trips.Price`: giá hiện tại chuyến. `Tickets.Price`: đơn giá snapshot khi đặt. `TicketDetails.TotalPrice = Price * SeatCount`; không phải Tickets.Price đã là tổng. Một booking nhiều ghế vẫn chỉ một passenger record và một code (`DatabaseHelper.cs:757-791`; `Models/DomainModels.cs:106-140`).
- StagePrice được lưu ở Trips/Buses nhưng insert booking đọc `tr.Price`, không đọc StagePrice và không lưu origin/destination chặng hành khách trong Tickets (`DatabaseHelper.cs:759-761`; `Migration_014_StagePrice.sql:16-21`). Không suy diễn đã có bán vé theo từng đoạn đường từ việc có cột StagePrice.
- Tên tuyến, giờ chạy, bus type, biển số trong ticket projection lấy JOIN dữ liệu hiện tại của Trips/Routes/Buses; không snapshot trong Tickets. Thay thông tin chuyến/tuyến/xe có thể đổi nội dung vé hiển thị sau này; Price/PassengerName/PassengerPhone là snapshot riêng (`DatabaseHelper.cs:1135-1162`).
- SeatNumbers dùng STRING_AGG theo SeatId, fallback dấu gạch nếu không có junction; SeatCount lấy column, không COUNT junction. Model không tự sửa mismatch (`DatabaseHelper.cs:1142-1158,1186`).
- `DATETIME2` không chứa timezone. C# ReadTicket/SearchTrips gán DateTimeKind.Utc rồi model đổi local để hiển thị. Admin Add/UpdateTrip chuyển input không phải UTC qua ToUniversalTime. SQL defaults dùng SYSUTCDATETIME; DevSeedTrips chuyển giờ Việt Nam bằng trừ 420 phút (`DatabaseHelper.cs:407-408,1168-1185`; `AdminDatabaseService.cs:1045-1046,1104-1105`; `DevSeedTrips.sql:36-45`). Filter ngày dùng CAST(DepartureTime AS date)=@Date trực tiếp trên UTC, không có chuyển timezone trong query (`DatabaseHelper.cs:369,399,610,640`).

### 6.4 Actor và ranh giới quyền dữ liệu

- UserSession chứa AccountId, Username, FullName, Email, Phone, Role; không có access token/session DB/expiry. `CurrentUser.Account` là mutable static; IsAdmin kiểm Role không phân biệt hoa thường (`Models/UserSession.cs:3-15`).
- AdminDatabaseService RequireAdmin chỉ nhìn CurrentUser.IsAdmin, không query lại Accounts.Role mỗi thao tác; đây là authorization ở process ứng dụng, không SQL RLS/GRANT riêng (`AdminDatabaseService.cs:10-14`).
- Tác vụ khách trong DatabaseHelper thường nhận accountId/account object do caller truyền. GetTickets/Dashboard/History có WHERE AccountId; Cancel/Refund có owner condition trong câu SELECT lock. BookTickets chỉ dùng accountId cho FK, không đối chiếu CurrentUser. UpdateProfile dùng account.AccountId; ChangePassword kiểm mật khẩu hiện tại nhưng không đối chiếu session static (`DatabaseHelper.cs:177-217,709-791,894-896,987-989,1052-1069`).
- GetTicketByCode chỉ lọc Code đã trim, **không owner filter/RequireAdmin trong helper** (`DatabaseHelper.cs:1112-1130`). Có thể được UI giới hạn elsewhere; không được coi tầng data này tự bảo vệ lookup bằng role.
- Station scope không có cột StationId/permission table trong DB. Admin helper dùng LocationService.CurrentLocation + match endpoint; Add/Update/DeleteTrip có predicate scope; CancelTrip đọc theo TripId và RequireAdmin nhưng không cùng predicate scope (`AdminDatabaseService.cs:22-61,1053-1059,1118-1121,1171-1175,1198-1208`). Đây là hành vi hiện tại, không đề xuất policy mới.

## 7. Hành vi DatabaseHelper: đọc, ghi và lỗi

### 7.1 Đăng ký, đăng nhập, hồ sơ, mật khẩu

**Đăng ký** (`DatabaseHelper.cs:157-175,1278-1304`):
1. Username/full name phải không trắng và dài tối đa 50/100. Không trim trong helper.
2. Password 8–128 ký tự, có uppercase, lowercase, digit theo `char.IsUpper/IsLower/IsDigit`; không bắt buộc ký tự đặc biệt, không bắt buộc ASCII.
3. Email nếu không trắng: regex `^[^@\s]+@[^@\s]+\.[^@\s]+$`; phone nếu không trắng: `^\+?[0-9\s-]{8,20}$`. Helper không explicit kiểm email.Length<=100 ngoài type parameter/schema. Không phone uniqueness.
4. INSERT không truyền Role → DEFAULT Customer. SQL 2601/2627 → thông báo username/email trùng; lỗi SQL khác → log và lỗi chung.

**Định dạng mật khẩu** (`DatabaseHelper.cs:13-21,1189-1257`):
- Hiện tại PBKDF2-SHA256, 310.000 iterations, salt ngẫu nhiên 16 bytes, digest 32 bytes; chuỗi `PBKDF2-SHA256$iterations$base64(salt)$base64(hash)`.
- Nhận prefix legacy `PBKDF2$...` nhưng vẫn derive SHA-256. So sánh `CryptographicOperations.FixedTimeEquals`.
- Từ chối plaintext và hash lỗi format; không tự chuyển plaintext thành password hợp lệ. Parser yêu cầu đúng 4 phần, iterations integer >0, base64 hợp lệ, salt/hash không rỗng; không giới hạn trên iterations hay buộc salt/hash đúng kích cỡ mới. Đây là giới hạn parser thực tế, không phải kiểm tra an toàn đầy đủ mọi stored input.
- Sau login thành công, prefix cũ hoặc iterations thấp hơn hiện tại được rehash; UPDATE có compare-and-swap `WHERE AccountId AND PasswordHash=@OldHash`, không ghi đè hash đã đổi song song. Nếu upgrade lỗi SQL vẫn giữ login thành công, chỉ log warning (`DatabaseHelper.cs:108-115,1226-1275`).

**Đăng nhập** (`DatabaseHelper.cs:59-155`):
1. TOP(1) WHERE Username=@Login OR Email=@Login, parameter NVARCHAR(100), không ORDER BY. Unique riêng từng cột không cấm username của account A trùng email account B; lookup có thể mơ hồ ở trường hợp đó. Không có constraint cross-field trong schema.
2. Không tìm thấy account: vẫn VerifyPassword với dummy hash để trả chi phí derivation rồi trả lỗi credentials chung. Không ghi lockout cho định danh không tồn tại.
3. LockoutEndsAt>app UTC now: từ chối ngay, kể cả password đúng; log account ID và hạn, không phải typed identifier.
4. Sai password: UPDATE atomic `FailedLoginCount=FailedLoginCount+1`; nếu new count>=5 đặt LockoutEndsAt=DB UTC now+15 phút; trả count để chọn thông báo locked/incorrect.
5. Hết 15 phút **không tự reset count**. Nếu lần tiếp theo sai, count vẫn >=5 và khóa lại; chỉ thành công mới clear count=0/LockoutEndsAt=NULL. Đọc account và verify không nằm trong một transaction với cập nhật failure/success: không tuyên bố login concurrent serializable.
6. Đúng password: clear failed state nếu cần, upgrade hash nếu cần, tạo UserSession. Lỗi SqlException trả database-unavailable, không propagate raw SQL.

**Hồ sơ và đổi mật khẩu** (`DatabaseHelper.cs:177-217`):
- UpdateProfile kiểm full name/contact, UPDATE theo AccountId; trả `account with { ... }`. Không kiểm affected-row==1, không sửa username/role/password.
- ChangePassword áp policy mới, SELECT hash hiện tại và VerifyPassword; UPDATE hash mới theo AccountId. SELECT/UPDATE không nằm chung transaction/CAS; không clear lockout.
- Admin reset dùng cùng ValidatePassword/HashPassword nhưng UPDATE chỉ PasswordHash, **không reset FailedLoginCount/LockoutEndsAt**, không bắt đổi ở lần đăng nhập sau. Role change tự hạ quyền admin đang đăng nhập bị service từ chối; không có DB guard “ít nhất một admin” (`AdminDatabaseService.cs:1308-1363`).

### 7.2 Tra cứu và các giá trị fallback

| API | Query / công thức / lọc thực tế | Evidence |
|---|---|---|
| GetDistinctOrigins | DISTINCT Origin ORDER BY Origin | `DatabaseHelper.cs:310-314` |
| GetDestinationsByOrigin | Có origin thì equality theo trim; không có thì mọi Destination DISTINCT | `DatabaseHelper.cs:316-338` |
| SearchTrips | Origin/Destination LIKE contains đã escape [,%,_; Scheduled; không ngày thì departure>=UTC now, có ngày chỉ CAST UTC date; sort departure | `DatabaseHelper.cs:360-423` |
| GetTripsByCompany | CompanyName exact OR contains escaped; Scheduled; cùng date policy; sort departure | `DatabaseHelper.cs:608-666` |
| AvailableSeats | Buses.SeatCount − COUNT DISTINCT junction SeatId của ticket active và (paid hoặc expiry>now) | `DatabaseHelper.cs:370-390,611-631` |
| GetSeats | Toàn bộ Seats thuộc bus trip; IsBooked bằng cùng active+paid/expiry; không lọc trip Scheduled/future | `DatabaseHelper.cs:669-695` |
| GetPopularRoutes | Top theo COUNT Trips, không count bookings; MIN giá mọi chuyến, không chỉ future/Scheduled; COALESCE giá120000; max tên hãng/branding độc lập; duration distance/50km/h; tag theo vị trí thứ tự | `DatabaseHelper.cs:253-307` |
| GetBookingCompanies | Group TRIM tên; left join chuyến tương lai và ngày nếu có; origin/dest match route OR OperatingArea; không filter Trips.Status; active count=COUNT DISTINCT trip; giá MIN hoặc120000; sort tripcount, rating, name | `DatabaseHelper.cs:471-535` |
| GetBusCompanies | Sort Rating DESC, ReviewCount DESC; MinPrice/ActiveTripsCount dùng default DTO nếu không truyền | `DatabaseHelper.cs:538-570`; `Models/DomainModels.cs:30-48` |
| GetBusCompanyByName | TOP1 exact OR LIKE contains, không ORDER BY ưu tiên exact | `DatabaseHelper.cs:573-605` |

Nhiều fallback **là dữ liệu demo/hiển thị**, không phải thông tin đã kiểm chứng: tên công ty mặc định, rating4.8, hotline, tên tài xế, màu/logo, giá120000. Dictionary không được biến fallback DTO thành DEFAULT SQL. Các read helper thường catch SqlException, log rồi trả list rỗng/null/zero; một số list đã đọc có thể là partial nếu lỗi giữa vòng đọc (`DatabaseHelper.cs:303-307,419-423,531-535,662-666`).

**Side effect khi đọc danh mục**: GetBusCompanies/GetBookingCompanies gọi EnsureBusCompaniesSyncedAsync. `Interlocked.Exchange` cho chạy tối đa một lần mỗi process kể cả lần đầu lỗi. INSERT công ty thiếu từ TRIM Buses.CompanyName, tạo rating4.8/review150/logo2ký tự/màu/default amenities, hotline từ MAX DriverPhone, vùng từ MAX route endpoints. Không transaction riêng/hard retry; catch mọi Exception và log (`DatabaseHelper.cs:437-469`). Vì vậy API có vẻ read có thể ghi DB trong lần đầu. Không nhầm với seed cố định SQL.

### 7.3 Đặt nhiều ghế và concurrency

Nguồn đầy đủ: `DatabaseHelper.cs:709-872`.

- Input seatIds phải ít nhất 1, không lặp; helper chưa kiểm passenger name/contact theo regex registration, chưa kiểm số lượng ghế maximum.
- Trước transaction gọi PaymentService.ExpirePendingPaymentsAsync; lỗi SQL trả lỗi payment-data thay vì đặt tiếp.
- Một unit of work mở connection và Serializable transaction; sinh mã `BT-{UTC yyyyMMddHHmmss}-{random 1000..9998}`; INSERT một Tickets bằng SELECT TripId có tồn tại; không tìm trip → rollback và lỗi not found.
- **Không có predicate tr.Status=Scheduled hoặc DepartureTime>now trong INSERT booking**. Search/model có giới hạn nhưng helper/DB không đảm bảo tương đương. Không suy diễn “không đặt được chuyến đã hủy/đã chạy ở mọi tầng”.
- Mỗi seat INSERT SELECT chỉ nếu thuộc bus trip và chưa có holder active/paid-or-unexpired. Một seat thất bại → rollback toàn bộ booking; không giữ một phần. Đọc projection trong cùng transaction, không load được → rollback; đủ thì commit.
- SQL 2601/2627/51001/51002 → lỗi kiểm soát ghế vừa bị lấy/không hợp lệ. Nhóm transient rollback rồi throw ra SqlResilience để retry. Mã random trùng TicketCode cũng có thể vào chung mapping 2601/2627.
- SqlResilience thử tối đa **3 lần tổng cộng**, delay sau lần 1/2 =120ms×attempt + jitter0..79ms; transient numbers 1205, -2, 4060, 40197,40501,40613,49918,49919,49920. Không CancellationToken trong helper retry. Các lỗi khác không retry (`Data/SqlResilience.cs:20-54`).
- Sau hết retry, BookTicketsAsync trả system busy. Không áp SqlResilience mặc định cho mọi DB call; callsite đọc được ở đây là booking (`DatabaseHelper.cs:732-745`).

### 7.4 Hủy/hoàn và danh sách vé

- CancelTicket gọi IsSettledTicket trước. Nếu read này lỗi SQL helper trả false; nhánh unpaid sau đó chỉ đọc Status/Departure, không đọc lại PaymentStatus. Không coi pre-check đó là cùng atomic transaction với cancellation (`DatabaseHelper.cs:883-926,951-971`).
- Nhánh hủy khóa Tickets bằng UPDLOCK,HOLDLOCK; owner phải khớp; chỉ Status Đã đặt/Đã thanh toán và Departure>UTC now. Notfound/wrong status/departed trả code phân biệt; toàn cập nhật vé + pending payments nằm transaction XACT_ABORT.
- RequestRefund cũng lock ticket trong transaction; phải owner đúng, chưa cancelled/used, đồng thời Status/PaymentStatus paid, chưa khởi hành. Payment Success chưa có request mới được stamp. Nếu không có Success ledger nhưng ticket flags paid, câu UPDATE payment có thể affect0 mà method vẫn trả success; DB không ràng buộc tồn tại Success row (`DatabaseHelper.cs:979-1026`).
- GetTickets(history=false): Status reserved/paid và departure>=now. History=true: used/cancelled hoặc reserved/paid đã qua giờ. Không dựa ArrivalTime, không trực tiếp dựa Trips.Status. List DESC departure (`DatabaseHelper.cs:1052-1078`).
- GetHistoryStats: đếm booking used hoặc reserved/paid qua giờ, cộng DistanceKm mỗi booking một lần; không nhân số ghế, loại cancelled (`DatabaseHelper.cs:1082-1109`).
- Customer dashboard: TotalTickets=COUNT mọi booking; UpcomingTrips=count reserved/paid có departure>=now; TotalSpent=SUM ticket.Price×SeatCount khi ticket.PaymentStatus=paid, không trực tiếp SUM Payments. Vé cancelled đang chờ refund vẫn có thể được tính tiền đã chi (`DatabaseHelper.cs:225-249`).

## 8. Ma trận DB enforcement so với service/UI

| Invariant | DB thật sự enforce | Service/model hiện tại | Lưu ý khi mô tả hiện trạng |
|---|---|---|---|
| Không bán cùng seat/trip cho hai booking active | Indexed view unique, cả Used | Serializable + seat checks + sweep + retry | Không chỉ bảo vệ UI; expired-but-not-cancelled vẫn chiếm theo index |
| Ghế thuộc bus trip | AFTER INSERT TicketSeats trigger | Booking EXISTS; UpdateTrip cấm đổi bus khi có vé | Không tổng quát cho UPDATE/direct SQL |
| Một live payment/booking | Filtered unique Pending/Success | Payment transaction | Không chỉ 1 payment suốt lịch sử |
| Xe không chồng lịch | Chỉ UNIQUE bus+departure | AddTrip range lock trong txn; UpdateTrip có UPDLOCK,HOLDLOCK nhưng batch đọc/cập nhật không có explicit transaction bao quanh | Không tuyên bố DB cấm mọi overlap; xem `AdminDatabaseService.cs:1048-1060,1107-1137` |
| Chuyến đến sau đi, giá không âm | CHECK | Admin validation | StagePrice chưa có CHECK tương ứng |
| Chuyến phải Scheduled/tương lai mới book | Không CHECK | Search/model lọc; BookSeatsCore không kiểm | UI khả dụng không tương đương enforcement |
| SeatCount ticket bằng số TicketSeats | Không | Booking gán một lần, rollback nếu thiếu seat | Direct SQL có thể lệch |
| Account/password hợp lệ | UNIQUE/domain/type, không format password | ValidatePassword/VerifyPassword | PasswordHash DB vẫn là text bất kỳ |
| Lockout 5 lần/15 phút | Chỉ cột/default | AuthenticateAsync/atomic UPDATE count | Không trigger, không IP/device throttle ở schema |
| Actor là admin khi confirm/refund | FK chỉ account tồn tại | RequireAdmin/settlement guard | DB FK không kiểm role |
| Tuyến thuộc station | Không station table/RLS | Scope từ process và predicate một số service | Không phải policy global của SQL Server |
| Audit không sửa/xóa | Không | Append-only convention | Không immutable audit storage |
| Amount = fare×seats | Không | Payment path kiểm/ghi theo ticket | Không có computed persisted total/constraint |
| Refund nhất quán timestamps và trạng thái | Không cross-table CHECK | SQL settlement/request transaction | Status flags có thể lệch với direct writer |

Các mệnh đề “không có” trong bảng được giới hạn ở **DDL/method implementation đã đọc**, không khẳng định cấu hình thủ công ngoài repository của một database đang chạy.

## 9. Xóa dữ liệu, giữ lịch sử và orphan logic

### 9.1 Chính sách FK

- Accounts có Tickets hoặc có Payments.ConfirmedBy/RefundApprovedBy → DELETE bị NO ACTION FK chặn.
- Routes có Trips; Buses có Trips/Seats; Trips có Tickets; Seats có TicketSeats; Tickets có TicketSeats/Payments → không cascade, không xóa cha khi con còn tham chiếu.
- Tickets.SeatId legacy không còn FK; Buses.RouteId không FK; Buses.CompanyName không FK; AuditLog.Actor/Target không FK. Vì vậy DB cho phép text reference/orphan logic ở những trường này.
- Hủy vé/chuyến/payment failure là UPDATE trạng thái, không xóa booking/junction, đảm bảo lịch sử còn tồn tại và index view giải phóng ghế bằng predicate Status.

### 9.2 Guard của admin ngoài FK

- DeleteRoute chỉ xóa khi không có Trips (`AdminDatabaseService.cs:398-414`, predicate ở `:401`). Không FK Buses.RouteId nên riêng reference đó không được FK bảo vệ.
- DeleteBus: từ chối bus không tồn tại, có Trips hoặc bất kỳ TicketSeats qua Seats; XACT_ABORT + transaction xóa Seats rồi Buses. Nếu xóa bus lỗi, seat map được rollback (`AdminDatabaseService.cs:969-1008`).
- DeleteTrip: phải không có bất kỳ Tickets và route trong station scope; không chỉ active tickets (`AdminDatabaseService.cs:1167-1191`).
- DeleteAccount: không xóa self; chặn ticket noncancelled, rồi mọi ticket history kể cả cancelled, rồi account có xác nhận/duyệt hoàn payment. Sau DELETE ghi audit AccountDeleted (`AdminDatabaseService.cs:1370-1416`). Không soft-delete flag trên Accounts.
- AuditLog không FK cho phép giữ ActorAccountId/TargetId sau khi account bị xóa. Tuy nhiên DB không bảo đảm actor tồn tại tại thời điểm log.

## 10. Audit: dữ liệu ghi và atomicity thật sự

Nguồn helper: `BusTicketSaleSystem/Data/AuditLog.cs:21-68`.

- Helper lấy Actor từ CurrentUser.Account ở lúc write, không từ tham số adminAccountId của operation; nếu không có session thì DBNull. Action/TargetType/TargetId parameter hóa. Detail cắt 400 ký tự, không cắt action/type/id thủ công.
- Overload tự mở connection catch **SqlException** và swallow sau khi file-log; overload nhận connection/transaction không catch, caller sở hữu failure.
- Action đã thấy callsite: AccountRoleChanged, AccountPasswordReset, AccountDeleted, TripCancelled; PaymentConfirmed, PaymentRejected, RefundApproved. Không coi mọi CRUD/login/check-in đều đã có audit chỉ vì có bảng AuditLog (`AdminDatabaseService.cs:1263,1325,1355,1400`; `PaymentService.cs:510,544,589`).
- **Payment settlement audit atomic**: ExecuteSettlementAsync mở Serializable transaction; outcome refused rollback và không audit; thành công ghi audit cùng connection/transaction rồi commit (`PaymentService.cs:595-635`).
- **Admin account/trip audit không chung atomic transaction**: role/reset/delete chạy UPDATE/DELETE autocommit rồi gọi WriteAsync(connection,null); CancelTrip batch COMMIT trước rồi ghi audit. Nếu audit lỗi SQL, business operation có thể đã commit nhưng method trả lỗi. Không lặp lại comment migration “mọi log đều trong cùng transaction” như fact (`AdminDatabaseService.cs:1251-1264,1314-1332,1345-1362,1376-1415`).
- Audit chỉ chứa detail summary, không lưu toàn bộ before/after JSON, không có cryptographic chain, không partition/retention/purge DDL. Không sao chép secrets hoặc password hash vào báo cáo này.

## 11. Seed dữ liệu, giá trị mẫu và side effects

### 11.1 SeedData.sql — idempotent theo truy vấn tồn tại

Nguồn: `BusTicketSaleSystem/Data/SeedData.sql:5-41`.

| Tuyến seed | DistanceKm |
|---|---:|
| Hà Nội → Hải Phòng | 120 |
| Hà Nội → Nam Định | 90 |
| Hà Nội → Thái Nguyên | 80 |
| Hà Nội → Thanh Hóa | 150 |
| Hà Nội → Ninh Bình | 100 |
| Hải Phòng → Hà Nội | 120 |
| Nam Định → Hà Nội | 90 |

| Xe seed | SeatCount | BusType |
|---|---:|---|
| 29B-123.45 | 40 | Ghế ngồi |
| 29B-678.90 | 40 | Ghế ngồi cao cấp |
| 29B-999.88 | 34 | Limousine VIP |

- Chỉ insert tuyến nếu cặp endpoint chưa có, xe nếu biển số chưa có. Không sửa/xóa dữ liệu đang có.
- Seed Seats cho **tất cả Buses** bằng số 1..TOP40 và <=SeatCount, skip nhãn đã có. Với bus lớn hơn40, script này không tự tạo các ghế >40. Không có trigger tự sinh ghế.
- Không seed Accounts/password/admin/tickets/payments; không seed Trips ở file này. Tài khoản integration nằm ở fixture, không phải seed ứng dụng.

### 11.2 Migration 007: nhà xe và hồ sơ mẫu

Nguồn: `BusTicketSaleSystem/Data/Migration_007_BusCompanyAndRegistration.sql:87-286`.

| Company | Rating | ReviewCount | Vùng hoạt động | Logo / màu |
|---|---:|---:|---|---|
| Sao Việt | 4.8 | 1820 | Hà Nội - Thái Nguyên - Lào Cai - Sa Pa | SV / #0F4C81 |
| Hà Lan Buslines | 4.9 | 2450 | Hà Nội - Thái Nguyên - Tuyên Quang | HL / #16A34A |
| Hoàng Long | 4.7 | 3120 | Hà Nội - Hải Phòng - Đà Nẵng - TP.HCM | HL / #DC2626 |
| Phương Trang (FUTA) | 4.8 | 5600 | Toàn quốc: Bắc - Trung - Nam | FUTA / #EA580C |
| Hải Vân Express | 4.8 | 1430 | Hà Nội - Lào Cai - Sa Pa - Điện Biên | HV / #7C3AED |

Mỗi công ty có hotline, Description marketing và Amenities literal trong script, **không phải review thu thập từ người dùng**. Sao Việt có wifi/nước/khăn/USB/điều hòa/LCD/massage; Hà Lan wifi5G/nước/khăn/Type-C/ghếVIP/điều hòa; Hoàng Long wifi/chăn/nước/sạc/rèm/WC; FUTA wifi/nước/khăn/sạc/màn hình/điều hòa; Hải Vân cabin/massage/TV/tai nghe/bữa nhẹ. Script insert theo CompanyName nếu chưa tồn tại, không overwrite existing company (`:88-170`).

Backfill Routes khi IntermediateStops NULL:
- Hà Nội→Thái Nguyên: Nội Bài, Sóc Sơn, Yên Bình, bến Thái Nguyên; lịch 06:00/08:30/11:00/14:00/16:30/19:00 (`:174-177`).
- Thái Nguyên→Hà Nội: Yên Bình, Sóc Sơn, Nội Bài, Giáp Bát; lịch 06:30/09:00/11:30/14:30/17:00/19:30 (`:180-183`).
- Hà Nội→Hải Phòng: Vĩnh Bảo, Tiên Lãng, Quán Toan, Vĩnh Niệm; lịch 06:00/07:30/09:00/11:00/13:30/15:30/18:00 (`:186-189`).
- Hà Nội→Đà Nẵng: Phủ Lý, Ninh Bình, Thanh Hóa, Vinh, Hà Tĩnh, Đồng Hới, Huế, bến Đà Nẵng; lịch08:00/15:00/19:00/21:00 (`:192-195`).
- Destination chứa Nha Trang: Miền Đông, Dầu Giây, Phan Thiết, Phan Rang, Cam Ranh, bến phía nam Nha Trang; lịch07:00/10:00/13:00/18:00/20:30 (`:198-201`).
- Phần còn NULL: ghép Origin(bến xuất phát), trạm cao tốc, Destination(bến trả khách); lịch07:00/09:30/13:30/16:00/18:30 (`:204-208`).

Backfill Buses chỉ nơi CompanyName NULL: BusId%4=0 Sao Việt/Hyundai Universe Express2023; %=1 Hà Lan/Thaco Mobihome VIP2024; %=2 Hoàng Long/Samco Primas Limousine2022; phần còn lại FUTA/Thaco Bluesky120S2023. Mỗi nhánh đồng thời gán tên/phone tài xế mẫu, ghi chú lịch, màu, số giấy tờ, ngày đăng ký/hạn kiểm định/bảo hiểm, RegisteredEntity và OperationType='Tuyến cố định'. Đây là **UPDATE dữ liệu hiện có**, không chỉ thêm defaults; có thể thay các field khác ngay cả khi chúng đã có giá trị miễn CompanyName NULL. Các giá trị mẫu giấy tờ không unique và không được báo cáo như dữ liệu thực đã xác minh (`:212-285`).

### 11.3 DevSeedTrips.sql — top-up lịch, không migration

Nguồn: `BusTicketSaleSystem/Data/DevSeedTrips.sql:20-71`; quyết định chạy `DatabaseMigrator.cs:72-108`.

| Tuyến | Xe | Giờ Việt Nam | Thời lượng phút | Price |
|---|---|---|---:|---:|
| Hà Nội→Hải Phòng | 29B-123.45 | 07:30 | 120 | 140000 |
| Hà Nội→Nam Định | 29B-123.45 | 10:30 | 120 | 110000 |
| Hà Nội→Ninh Bình | 29B-123.45 | 15:30 | 120 | 120000 |
| Hà Nội→Thanh Hóa | 29B-678.90 | 08:00 | 210 | 180000 |
| Hà Nội→Hải Phòng | 29B-678.90 | 14:00 | 120 | 150000 |
| Hà Nội→Thái Nguyên | 29B-999.88 | 09:00 | 120 | 100000 |
| Hải Phòng→Hà Nội | 29B-999.88 | 17:00 | 120 | 140000 |
| Nam Định→Hà Nội | 29B-999.88 | 06:30 | 120 | 110000 |

- Horizon check là DATEDIFF(DAY,UTC now,MAX departure future), fallback−1; >=2 thì không chạy. Không đánh giá coverage từng tuyến/nhà xe.
- Vòng @Day=0..14 **inclusive** (15 ngày lịch mốc), bỏ departure đã qua. TargetDate lấy UTC date; giờ slot local được trừ7h.
- JOIN tuyến/xe không có thì skip; NOT EXISTS cùng RouteId+DepartureTime (không phân biệt bus); NOT EXISTS overlap cùng bus theo khoảng mở `existing.Departure<new.Arrival AND existing.Arrival>new.Departure`; không filter cancelled.
- Không update/xóa chuyến hiện có. Insert mới Status Scheduled, không truyền StagePrice. Không tạo tài khoản/mật khẩu. Actual số chuyến thêm phụ thuộc thời điểm và dữ liệu, không khẳng định luôn120.

## 12. Reporting: SQL inline và DTO không phải bảng

### 12.1 Query doanh thu đang có

- Admin dashboard: counts routes/buses/future trips; TicketsSold đếm **dòng Tickets** paid/used, không SUM SeatCount; revenue SUM(Payments Success Amount) − SUM(Refunded Amount); pending count/amount và refunded amount riêng (`AdminDatabaseService.cs:68-82`).
- RevenueReport: chỉ Payments.Status Success/Refunded, lọc **Payments.CreatedAt** >=From và <To (không PaidAt/RefundedAt), join về route/bus/company; group Origin/Destination; TicketsSold=SUM CASE Success THEN1; GrossRevenue=SUM current Success.Amount; RefundedAmount=SUM current Refunded.Amount; CommissionRate=AVG(company rate), fallback0.1 (`AdminDatabaseService.cs:270-300`).
- Model `NetRevenue=GrossRevenue−RefundedAmount`; nếu net>0 thì commission=net×rate và payout=net−commission, nếu net<=0 hai khoản này=0. Không rounding nghiệp vụ trước render; UI display N0 (`Models/AdminModels.cs:299-314`). `AdminPaymentStats` dùng cùng success minus refunded (`:277-296`).
- **Caveat có bằng chứng**: GrossRevenue không bao gồm rows đã đổi từ Success→Refunded, sau đó Net lại trừ RefundedAmount. Không tự diễn giải công thức thành “tất cả đã thu trừ hoàn” hoặc sửa công thức trong báo cáo. Commission AVG theo payment rows của group route, không settlement riêng theo company; cùng route nhiều company có thể lấy trung bình theo rows, không phải weighted theo tiền (`AdminDatabaseService.cs:274-288`).
- Không có bảng doanh thu snapshot, commission payable, payout ledger hoặc stored report procedure trong schema. Đây là projection thời điểm query và có thể đổi khi trạng thái/payment/company rate thay đổi.

### 12.2 Inventory Models và nghĩa derived fields

| Model | Dữ liệu / derived behavior | Evidence |
|---|---|---|
| TicketStatuses | 6 constants strings Vietnamese; Paid dùng cho cả hai cột | `Models/DomainModels.cs:8-26` |
| BusCompanyItem | 11 field company + MinPrice default120000/ActiveTripsCount0; rating/review/price display; HasActiveTrips dựa count; Amenities split comma trim/remove empty | `Models/DomainModels.cs:30-51` |
| TripSearchResult | trip/route/time/unit fare/available count + branding/stops/driver/plate; IsAvailable seats>0 và departure>UTC now; opacity1 hoặc0.45; StopsList origin+comma stops+destination, Distinct OrdinalIgnoreCase | `Models/DomainModels.cs:53-99` |
| SeatInfo | SeatId,BusId,SeatNumber,IsBooked; DisplayName thêm booked | `Models/DomainModels.cs:101-104` |
| TicketDetails | projection booking, init SeatCount/passenger/plate/payment/expiry/used/cancelled; TotalPrice; IsPaid theo payment flag; IsUsed/Cancelled theo status; IsAwaitingPayment cho phép expiry NULL; CanRequestRefund paid+paid+future | `Models/DomainModels.cs:106-170` |
| PopularRouteItem | route/display duration/price/tag/minprice/brand/stops; fallback mô tả chạy thẳng | `Models/DomainModels.cs:172-189` |
| RouteSummaryItem | id/endpoints/nullable distance | `Models/DomainModels.cs:191-195` |
| UserSession/CurrentUser | account DTO và global mutable session; không persisted session | `Models/UserSession.cs:3-15` |
| RouteAdminItem | id/endpoints/distance; display route/km hoặc Not set | `Models/AdminModels.cs:5-9` |
| BusAdminItem | id/plate/capacity/type/UsedSeats + hồ sơ, FullPrice/StagePrice; occupancy round rồi clamp0..100; FareDisplay stage>0 thì toàn tuyến/chặng, không có giá dương thì theo cự ly | `Models/AdminModels.cs:11-45` |
| BusRegistrationInput | company/driver/plate/schedule/route/stops/capacity/type/legal fields + FarePrice, hotline, area, description, amenities, StagePrice | `Models/AdminModels.cs:47-73` |
| BusDraftItem | Form mutable strings cho số ghế/năm/giá; defaults40/Limousine/Hyundai/Universe/2023; route Hà Nội→Thái Nguyên; giá150000/chặng70000; ngày đăng ký today, kiểm định+2y, bảo hiểm+1y; derived summaries | `Models/AdminModels.cs:75-134` |
| TripAdminItem | id route bus/time/price/status; IsCancelled/Departed exact; CanCancel Scheduled+future; status display unknown cũng fallback Đang chạy | `Models/AdminModels.cs:136-162` |
| RecentBookingAdminItem | booking code/route/passenger/seat text/price/status/time; chưa SeatCount riêng trong record | `Models/AdminModels.cs:164-179` |
| AdminDashboardStats | 9 aggregate counters/amounts, Empty zeros, formatted settlement caption | `Models/AdminModels.cs:186-205` |
| AccountAdminItem | account fields+CreatedAt; IsAdmin so sánh exact, khác CurrentUser.IsAdmin ignorecase | `Models/AdminModels.cs:207-220` |
| PaymentAdminItem | 20 field ledger+ticket+route+audit labels+expiry; CanConfirm/Reject chỉ Pending, CanApproveRefund Success+request, CanCheckIn Success+ticket paid | `Models/AdminModels.cs:225-275` |
| AdminPaymentStats | bốn nhóm count/amount và NetRevenue | `Models/AdminModels.cs:277-296` |
| RevenueReportItem | route/tickets/gross/refunded/rate và net/commission/payout | `Models/AdminModels.cs:299-314` |

Model property dùng để trình bày/enable UI **không thay thế guard DB/service**. Ví dụ TicketDetails.IsAwaitingPayment cho expiry NULL là true nhưng availability query chỉ tính unpaid hold nếu expiry>now; không nhập nhằng hai định nghĩa (`Models/DomainModels.cs:149-151`; `DatabaseHelper.cs:675-677`).

## 13. Inventory kiểm thử hiện có — chỉ đọc, không chạy

### 13.1 SQL fixture và ranh giới tác động

`BusTicketSaleSystem.Tests/SqlServerFixture.cs:27-85,173-227`:
- Catalog cố định `BusTicketSaleSystem_Tests`; server từ BUS_TICKET_TEST_SERVER hoặc localhost; set env DB app/master trước helper calls.
- Initialize **reset/drop-create test DB**, migrate, seed12 customer accounts và trip/seat data. Dispose không drop DB. Không chạy suite trong đợt phân tích này vì sẽ mutate test DB.
- SQL không reachable hoặc migration false → SkipReason; `[SkippableFact]` test dùng SkipIfUnavailable. Vì vậy chỉ có source test hoặc tổng báo cáo xanh chưa đủ để kết luận SQL tests thực chạy; cần phân biệt skipped ở lần xác minh riêng.
- Guard kiểm configured InitialCatalog đúng test DB trước helper destructive. Fixture không introspect Dev DB và không chứng minh Dev schema hiện tại.

### 13.2 Test contract đang được bảo vệ trong source

| File | Kịch bản/assertion quan trọng | Evidence |
|---|---|---|
| ConcurrencyBookingTests.cs | 12 callers cùng seat →1 success/1 active holder; hai request3ghế chồng1ghế →1 success và loser không để partial; seat đã lấy trả lỗi kiểm soát | `:18-84` |
| ConcurrencyBookingTests.cs | Bật READ_COMMITTED_SNAPSHOT, hai raw SQL transaction synchronized → chỉ1 commit; loser phải 2601/2627/1205/51002; finally tắt RCSI; kiểm unique indexed-view object tồn tại | `:92-189` |
| AdminDataIntegrityTests.cs | 4 concurrent create BankTransfer →1 success và1 live payment; thêm bảng FK thử nghiệm làm bus delete thất bại→bus và8ghế giữ nguyên;5 concurrent identical trip slots→1 row | `:22-131` |
| AdminDataIntegrityTests.cs | Current station Cà Mau từ chối route/trip Hà Nội; assertion không insert trip ngoài scope | `:137-185` |
| AuthenticationTests.cs | Sau5 sai account khóa, password đúng vẫn bị từ chối; successful login clear counter; chỉnh hạn quá khứ rồi password đúng đăng nhập được | `:17-78` |
| AuthenticationTests.cs | Unknown login derivation cost >=0.5 wrong known login; timing test heuristic, không proof tuyệt đối chống enumeration | `:86-105` |
| SecurityTests.cs | Salt khác nhau cùng password; prefix và >=310k; verify password đúng/sai; invalid stored/null/plaintext/malformed hash rejected; legacy PBKDF2 prefix accepted | `:7-76` |
| MigrationHistoryTests.cs | Fresh fixture không checksum NULL; StagePrice cols + logical017 có history; sửa checksum016 thành toàn0 làm migrate false, phục hồi rồi migrate true | `:16-68` |
| AuditTrailTests.cs | Role change thêm đúng actor audit; Used ticket không được approve refund; manual confirm ghi1 audit, confirm lại bị từ chối và không thêm audit | `:17-118` |

Các test dữ liệu vòng đời khác hiện diện và được các báo cáo chức năng/payment phân tích: `ExpirySweepTests`, `TicketReleaseTests`, `VnPaySettlementTests`, `TripCancellationTests`, `CheckInTests`, `CommissionReportTests`, `DomainModelTests`. Không dùng tên test đơn thuần để khẳng định mọi boundary đã được test.

**Coverage chưa được các case trên chứng minh**: legacy schema thiếu CHECK/FK, direct SQL UPDATE đổi bus/seat, booking helper gọi thẳng chuyến Cancelled/past, bất nhất SeatCount/junction, account username↔email cross-collision, audit write fail sau admin business commit, concurrent đổi password/login, overlap khi UpdateTrip đồng thời. Đây là phạm vi chứng cứ chưa có trong các case đã đọc, không phải kết quả test thất bại đã chạy.

## 14. Các điểm phải giữ nguyên nghĩa khi dùng làm đầu vào chuyển web

1. **Đơn vị booking** là một Tickets nhiều TicketSeats; không tách mỗi ghế thành một mã vé nếu chưa có quyết định nghiệp vụ. Giá lưu trên Ticket là đơn giá, tổng phải nhân SeatCount (`DatabaseHelper.cs:757-791`; `DomainModels.cs:121-125`).
2. **Nguồn chống double-sale** hiện tại là indexed view unique trên (TripId,SeatId), không phải index legacy Tickets.SeatId và không chỉ trigger/app lock (`Migration_008_SeatUniqueness.sql:42-57`).
3. **Hết hạn theo đồng hồ không tự giải phóng uniqueness**; cần transition Status thông qua sweep. Availability UI và DB uniqueness không dùng predicate hoàn toàn giống nhau (`DatabaseHelper.cs:675-677,724-739`; `Migration_008_SeatUniqueness.sql:44-48`).
4. **Hoàn tiền là hai bước**: release/request và approval; cancel+paid là trạng thái hợp lệ chờ hoàn, không tự sửa thành refunded (`DatabaseHelper.cs:1018-1022`; `PaymentService.cs:569-572`).
5. **Dữ liệu tuyến/xe/nhà xe không toàn bộ normalized**: company join tên, stops/amenities/schedule là text, Buses.RouteId không FK. Không diễn giải source hiện tại thành schema đã có Station/Driver/RouteStop/Review tables.
6. **Mật khẩu** không plaintext compatibility; rehash chỉ sau successful derived-hash authentication. Không ghi secrets, hash mẫu có thể dùng đăng nhập, connection string nhạy cảm vào artifact (`DatabaseHelper.cs:1189-1275`).
7. **Role, station, ownership** hiện được enforce không đồng đều ở data helper; session process-global không phải server multiuser authorization. Báo cáo ghi đúng boundary, không tự phát minh policy web hoặc claim RLS đã có.
8. **Audit/reporting** phải đọc implementation: không phải mọi admin action+audit atomic; revenue currently Success-only minus Refunded; filter theo CreatedAt; rate AVG route-level. Không dùng comment/label làm công thức thay thế (`AdminDatabaseService.cs:270-300,1251-1264,1314-1325`).
9. **Migration state** mới và legacy không chắc giống nhau. 18 history rows không đảm bảo toàn bộ CHECK/index definition chuẩn nếu script từng skip hoặc có NULL checksum; runtime catalog chưa được truy vấn.
10. **Trạng thái hoàn thành phân tích**: đã có tài liệu source-backed; không sửa code; chưa chạy/validate ứng dụng hoặc database; không chứng nhận dữ liệu thực tế, khả năng deploy hay tính đúng runtime vượt quá bằng chứng source.
