# 05 — Khởi động, cấu hình và vận hành hiện tại

## 1. Phạm vi bằng chứng

Đây là trích xuất tĩnh từ implementation, không phải xác nhận hệ thống đang chạy thành công trên máy hoặc production. Không chạy ứng dụng trong đợt khảo sát: khởi động ứng dụng có thể tạo DB, chạy migration, seed lịch và cập nhật vòng đời vé. Không kết nối DB, không gọi dịch vụ thanh toán/email, không chạy test tích hợp có thao tác reset DB. Không thay đổi mã nghiệp vụ.

Quy ước: đường dẫn `BusTicketSaleSystem/...` tính từ thư mục chứa solution. Thông tin trong README được đối chiếu với implementation; khi khác nhau, mô tả code đang thực thi.

## 2. Nền tảng và ranh giới tiến trình

| Thành phần | Hiện trạng | Bằng chứng |
|---|---|---|
| Giao diện | WPF desktop, executable `WinExe`, không phải web frontend | `BusTicketSaleSystem/BusTicketSaleSystem.csproj:3-9` |
| Runtime | `net10.0-windows`, nullable và implicit usings bật | cùng file:5-8 |
| SQL | Microsoft.Data.SqlClient 7.0.2; truy cập SQL Server qua helper/service, không có ORM trong dependencies | cùng file:11-19; `README.md:7-9` |
| DI | Microsoft.Extensions.DependencyInjection 10.0.0 | csproj:14 |
| Configuration | Microsoft.Extensions.Configuration.Json 10.0.0 | csproj:13 |
| QR | QRCoder 1.6.0 | csproj:15 |
| PDF | QuestPDF 2025.12.3 | csproj:16 |
| Log | Serilog 4.3.0 + Serilog.Sinks.File 7.0.0 | csproj:17-18 |
| Phiên bản build khai báo | 1.1.0 | `Directory.Build.props:22` |

Ứng dụng desktop vừa hiển thị UI, vừa trực tiếp gọi DB và dịch vụ ngoài, vừa chạy bảo trì nền. Không được hiểu việc có service class là đã có backend HTTP. Listener VNPay là listener callback chuyên biệt, không phải API server tổng quát.

## 3. Trình tự khởi động chính xác

`App.OnStartup`, `BusTicketSaleSystem/App.xaml.cs:16-54`:

1. Gọi startup của WPF.
2. Khởi tạo ngôn ngữ đã lưu.
3. Ghi log bắt đầu tiến trình.
4. Đăng ký handler lỗi UI dispatcher, AppDomain và task chưa observe.
5. Tạo DI container.
6. Subscribe `ISessionService.SignedOut` để mở login và đóng shell cũ.
7. Chờ `DatabaseMigrator.MigrateAsync()`.
8. Nếu migration trả `false`: log `LastError`, hiện dialog database error, `Shutdown(1)`, không vào login.
9. Chờ `EnsureUpcomingTripsAsync()` để đồng bộ nhà xe và có thể bổ sung lịch mẫu.
10. Khởi động housekeeping nền.
11. Gọi `VnPayCallbackListener.Start()`; khả năng thực sự lắng nghe còn phụ thuộc điều kiện bên trong listener/tính năng VNPay, không được suy chỉ từ call này.
12. Mở cửa sổ login.
13. Exception ngoài nhánh migration: log, hiện startup error, `Shutdown(1)`.

### Hệ quả vận hành

- Chưa login không có nghĩa là chưa truy cập hoặc thay đổi DB: migrations và housekeeping bắt đầu trước login.
- Quyền DB trên máy trạm ảnh hưởng trực tiếp khả năng mở ứng dụng.
- Mỗi tiến trình desktop có scheduler riêng. `Start()` chỉ chống khởi động lặp trong cùng tiến trình, không phải leader election giữa máy trạm.
- Việc đưa logic này lên web cần tách trách nhiệm backend/job runner; đây là yêu cầu suy ra từ kiến trúc, chưa phải thay đổi đã thực hiện.

## 4. DI và vòng đời màn hình

`BusTicketSaleSystem/Services/AppComposition.cs:23-64`:

- `NavigationService` và `INavigationService`: scoped.
- `ISessionService`: singleton.
- `IPaymentDialogService`: singleton.
- Các page ViewModel được đăng ký transient: mỗi lần resolve có instance mới thay vì giữ dữ liệu lần mở đầu.
- Customer shell và admin shell có scope điều hướng riêng.
- `CreateShellScope` tạo scope, lấy navigator, đặt factory resolve ViewModel từ chính scope đó.
- Các ViewModel cần booking payload không nằm toàn bộ trong danh sách transient; không suy ra mọi trang đều được dựng qua cùng factory.

Danh sách đăng ký: Home, Booking, BusCompanyDetail, MyTickets, History, Profile, Main; AdminMain, Dashboard, RouteManagement, BusManagement, TripManagement, Payments, CheckIn, Reports, Accounts.

## 5. Cấu hình và precedence

`BusTicketSaleSystem/Services/AppConfig.cs:8-53`:

### 5.1 Nguồn cấu hình thực sự được nạp

- Base path là `AppContext.BaseDirectory` — thư mục executable, không phải mặc định thư mục source.
- Chỉ gọi `AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)`.
- Các environment variable được đọc trực tiếp khi truy cập property.
- Không thấy nạp `appsettings.Development.json`, `secrets.json` hoặc .NET User Secrets trong configuration builder này.
- JSON không tự reload trong tiến trình hiện tại.

### 5.2 Bảng cấu hình

| Khóa/biến | Thứ tự hoặc mặc định | Tác động |
|---|---|---|
| `BUS_TICKET_CONNECTION_STRING` | Environment → `ConnectionStrings:DefaultConnection` → local SQL Server, catalog `BusTicketSaleSystem`, integrated authentication | Kết nối DB ứng dụng |
| `BUS_TICKET_MASTER_CONNECTION_STRING` | Environment → `ConnectionStrings:MasterConnection` → local SQL Server, catalog `master`, integrated authentication | Kết nối phục vụ tạo DB |
| `Logging:LogDirectory` | Giá trị JSON không rỗng kết hợp base directory; thiếu thì `Logs` bên executable | Nơi lưu log |
| `Logging:RetainDays` | Số nguyên dương; mặc định 30 | Thực tế truyền vào giới hạn số file log lưu, không phải tác vụ xóa theo tuổi ngày độc lập |
| `VNPAY_TMN_CODE` | Environment; thiếu → rỗng | Merchant identity, không đủ để tự bật tính năng |
| `VNPAY_HASH_SECRET` | Environment; thiếu → rỗng | Bí mật ký; tuyệt đối không đưa giá trị vào báo cáo |
| `VNPAY_CALLBACK_PORT` | Số nguyên 1–65535; ngoài khoảng/không parse được → 5058 | Port loopback |
| Callback prefix | `http://127.0.0.1:{port}/vnpay-return/` | Return listener trên máy desktop |
| `VNPAY_RETURN_URL` | Environment → callback prefix | URL trả về |
| `BREVO_API_KEY` | Environment; thiếu → rỗng | Email transactional |
| `EMAIL_SENDER_NAME` | Environment → `OmniBus Việt Nam` | Tên người gửi |
| `EMAIL_SENDER_ADDRESS` | Environment; thiếu → rỗng | Địa chỉ gửi |
| `STATION_LOCATION` | LocationService đọc riêng | Ghim phạm vi máy trạm/bến |
| `BUS_TICKET_TEST_SERVER` | Fixture test đọc riêng; thiếu → `localhost` | Server test, không phải server ứng dụng |

Fallback connection strings của code đặt `Integrated Security=True`, `TrustServerCertificate=True`, `Encrypt=False`. Đây là hiện trạng cấu hình phát triển, không phải khuyến nghị bảo mật production. Không ghi nhận hoặc sao chép giá trị environment đang dùng trên máy.

### 5.3 Chênh lệch README cần lưu ý

1. `README.md:66-67` nhắc local override ở hai file development/secrets; builder hiện tại không nạp hai file đó (`AppConfig.cs:8-11`). Không hướng dẫn người triển khai dựa vào chúng như tính năng đã hoạt động.
2. `README.md:56` mô tả thiếu master environment sẽ thất bại; code có fallback JSON và localhost (`AppConfig.cs:18-21`). Việc thành công phụ thuộc server/quyền thực tế, không chỉ biến đó có tồn tại hay không.
3. CI khởi động LocalDB nhưng workflow hiện không đặt `BUS_TICKET_TEST_SERVER`; fixture mặc định localhost. Do đó không được khẳng định CI integration tests thực sự dùng LocalDB chỉ vì bước startup LocalDB có mặt. Bằng chứng: `.github/workflows/build-test.yml:31-38,50-51`; `BusTicketSaleSystem.Tests/SqlServerFixture.cs:33-42`.

## 6. Database bootstrap và migration

`BusTicketSaleSystem/Data/DatabaseMigrator.cs:22-63,110-123,169-217`:

- Semaphore nội bộ serialize các lần gọi trong cùng process.
- Lấy catalog từ connection string ứng dụng; không có tên catalog → lỗi.
- Kết nối master và chạy `IF DB_ID(...) IS NULL CREATE DATABASE ...`.
- Kiểm tra/tạo migration history, sau đó chạy 18 migration keys theo thứ tự cố định.
- Migration có transaction riêng; script và dòng history commit cùng nhau.
- Migration đã chạy được verify checksum; script đã bị sửa → startup fail.
- File script phải có ở `Data` bên executable; thiếu file → `FileNotFoundException`.
- Schema ban đầu có nhánh ghi nhận đã áp dụng nếu bảng `Accounts` đã tồn tại; ghi warning và dựa vào migration hardening bổ sung invariants.
- Migration là forward-only. Không có cơ chế tự chạy Down khi lỗi hoặc rollback version. Hoàn tác nghiệp vụ/schema cần quy trình riêng/backup; không chạy reset Dev.
- Chi tiết lịch sử, bảng, keys và invariants xem tài liệu 04.

## 7. Tự bổ sung lịch chạy mẫu

### Điều kiện gọi

`DatabaseMigrator.EnsureUpcomingTripsAsync`, `DatabaseMigrator.cs:72-108`:

1. Mở DB.
2. Gọi `EnsureBusCompaniesSyncedAsync()`.
3. Tính số ngày từ UTC hiện tại đến `MAX(DepartureTime)` của các chuyến tương lai; không có → -1.
4. Nếu còn ít nhất 2 ngày → không seed.
5. Nếu ít hơn → chạy `DevSeedTrips.sql`, đếm chênh lệch số chuyến tương lai, log.
6. Lỗi không phải cancellation → log, trả 0; không chặn login bằng exception từ nhánh này.

Lưu ý: phép đo là horizon chung của toàn bộ bảng Trips; không bảo đảm mỗi tuyến hoặc mỗi nhà xe đều có lịch đủ hai ngày. Predicate horizon không lọc trạng thái chuyến.

### Nội dung slot seed

`BusTicketSaleSystem/Data/DevSeedTrips.sql:20-29`:

| Tuyến | Biển xe mẫu | Giờ Việt Nam | Phút đi | Giá VND |
|---|---|---:|---:|---:|
| Hà Nội → Hải Phòng | 29B-123.45 | 07:30 | 120 | 140.000 |
| Hà Nội → Nam Định | 29B-123.45 | 10:30 | 120 | 110.000 |
| Hà Nội → Ninh Bình | 29B-123.45 | 15:30 | 120 | 120.000 |
| Hà Nội → Thanh Hóa | 29B-678.90 | 08:00 | 210 | 180.000 |
| Hà Nội → Hải Phòng | 29B-678.90 | 14:00 | 120 | 150.000 |
| Hà Nội → Thái Nguyên | 29B-999.88 | 09:00 | 120 | 100.000 |
| Hải Phòng → Hà Nội | 29B-999.88 | 17:00 | 120 | 140.000 |
| Nam Định → Hà Nội | 29B-999.88 | 06:30 | 120 | 110.000 |

Quy tắc SQL (`DevSeedTrips.sql:31-71`):

- Lặp day = 0..14, hai đầu bao gồm; không phải chỉ 14 lần lặp.
- Ngày target lấy từ ngày UTC; giờ slot quy ước giờ Việt Nam và trừ 420 phút trước lưu.
- Chỉ insert chuyến khởi hành sau UTC hiện tại.
- Phải tìm được tuyến đúng origin/destination và xe đúng biển số; thiếu thì tự bỏ slot.
- Không insert nếu đã có cùng tuyến và thời điểm khởi hành.
- Không insert nếu xe đã có khoảng chuyến giao nhau theo `existing.departure < new.arrival` và `existing.arrival > new.departure`.
- Chuyến tạo có trạng thái `Scheduled`.
- Script không update/delete dòng lịch cũ.

Đây là cơ chế dữ liệu phát triển đang được gọi từ startup, không phải module sinh lịch production đã được người dùng cấu hình. Khi chuyển web phải quyết định rõ cơ chế này; không mặc nhiên giữ việc seed lịch mẫu mỗi lần backend khởi động.

## 8. Housekeeping và vòng đời process

`BusTicketSaleSystem/Services/BackgroundJobScheduler.cs:15-85`:

- `Start` không làm gì nếu scheduler trong process đã có cancellation source.
- Chạy thread pool bằng `Task.Run`, không chạy DB work trên UI dispatcher.
- Chạy một pass ngay, rồi mỗi 60 giây bằng `PeriodicTimer`.
- Trong một loop không có hai pass overlap.
- Thứ tự mỗi pass: expire pending payments/seat holds → mark departed tickets used.
- Nếu thao tác đầu throw, thao tác thứ hai của pass đó chưa chạy; catch log lỗi và vòng tiếp theo tiếp tục.
- Ghi log số vé/hold thay đổi nếu lớn hơn 0.
- Khi stop: cancel timer, chờ tối đa 2 giây, catch AggregateException/log, dispose nguồn cancellation.
- Hàm DB ở pass không nhận cancellation token từ scheduler; không khẳng định Stop đã hủy được một SQL command đang chạy.
- Không có bảo đảm job vẫn chạy khi tất cả desktop đã đóng.

Khi thoát (`App.xaml.cs:118-125`): dừng callback listener → dừng scheduler → log tắt ứng dụng → flush Serilog → WPF exit.

## 9. Bến xe và vị trí máy trạm

`BusTicketSaleSystem/Services/LocationService.cs:21-233`:

### Dữ liệu và ưu tiên

- Singleton; mặc định `MachineLocation` và `_currentLocation` là `Thái Nguyên`.
- Danh mục địa điểm sử dụng `VietnamProvinces.List`. Code/comment dùng danh mục 63 tỉnh thành; không tự hiểu đây là danh mục hành chính mới nhất ngoài đời.
- Nếu `STATION_LOCATION` không rỗng: normalize, đặt cả máy và vị trí hiện tại, `IsStationPinned=true`.
- Constructor vẫn khởi chạy dò IP nền.

### Hành vi theo vai trò

- `CurrentLocation` của Admin luôn trả `MachineLocation`.
- `SetUserLocation` từ chối input rỗng và từ chối Admin đổi bến; log warning cho trường hợp Admin.
- User được đổi địa điểm; normalize, nếu khác thì đổi, raise `LocationChanged`, log; nếu giống thì trả false.
- `SetFixedMachineLocation` là API nội bộ công khai phục vụ config/test, cập nhật cả hai và raise event. Không coi đó là tính năng UI cho Admin.
- Enforcement phạm vi mutation nằm thêm ở AdminDatabaseService, xem tài liệu 02; khóa combobox hoặc getter không tự đủ làm authorization.

### Geolocation

- GET `https://ip-api.com/json/?fields=status,country,regionName,city`.
- HttpClient timeout 3 giây; User-Agent `OmniBus-Transit-VN/1.0`.
- Chỉ xử lý khi JSON `status=success`; ưu tiên city, fallback regionName.
- `country` được yêu cầu nhưng implementation này không kiểm tra giá trị country.
- Nếu chưa pinned thì được cập nhật `MachineLocation`; code này tự nó không chứng minh quyền admin dùng geolocation là an toàn. Phải xét guard `IsStationPinned` ở service quản trị.
- Với user, chỉ đổi `_currentLocation` theo IP khi nó vẫn bằng default station.
- Lỗi mạng/JSON → log thông tin, giữ fallback; không chặn startup.

### Chuẩn hóa

- Trim; bỏ tiền tố `Bến xe`, `Tỉnh`, `Thành phố`, `TP.`, `TP `.
- So khớp không phân biệt hoa thường với danh mục.
- Tiếp đến contains hai chiều.
- Tiếp đến alias không dấu/tiếng Anh hardcode cho một số tỉnh/thành, ví dụ Hanoi, Saigon, Danang.
- Không match → trả chuỗi đã làm sạch, không reject toàn bộ giá trị lạ. Đây không phải validator bắt buộc thuộc danh mục.

## 10. Ngôn ngữ và dữ liệu cục bộ

`BusTicketSaleSystem/Services/LocalizationService.cs:16-174`:

- Hỗ trợ `vi-VN` và `en-US`; mặc định tiếng Việt.
- Đọc `language.json` bên executable, property `Language`.
- Đọc lỗi → tiếng Việt. Chuỗi đã lưu không rỗng được nhận trực tiếp; apply culture có catch/log lỗi.
- `SetLanguage`: input rỗng bỏ qua; bắt đầu bằng `vi` → tiếng Việt; còn lại → tiếng Anh.
- Không đổi nếu culture giống hiện tại; đổi thì apply, save và raise `LanguageChanged`.
- Apply đặt culture/UI culture thread hiện tại và default thread culture.
- Thay ResourceDictionary strings đang có trong merged dictionaries; thiếu thì thêm.
- Persist bằng `File.WriteAllText`; lỗi ghi được log, không throw cho người dùng.
- `GetString` fallback theo tham số hoặc chính key; `Format` dùng culture hiện tại và nếu format lỗi thì trả template.
- Đây là preference cục bộ thư mục executable, không chứng minh đã lưu theo account hoặc đồng bộ qua thiết bị.

## 11. Thông báo và lỗi

### Toast

`BusTicketSaleSystem/Services/NotificationService.cs:6-78`:

- Bốn loại: Info, Success, Warning, Error.
- Một message hiện tại, không có persistent inbox hoặc queue trong service này.
- Mặc định 6 giây; Error 10 giây; caller có thể truyền lifetime riêng.
- Message mới trim và thay message cũ; timer restart để message mới có đủ thời gian.
- Message rỗng không làm gì.
- Dismiss dừng timer, xóa message nếu đang hiện và raise event.
- Owner ở shell nên toast không biến mất chỉ vì ViewModel trang cũ bị thay khi điều hướng.

### Exception toàn ứng dụng

`App.xaml.cs:75-115`:

- Tạo mã tham chiếu 8 ký tự từ GUID cho lỗi UI; ghi log cùng exception.
- SQL exception, IOException, TimeoutException, OperationCanceledException, ExternalServiceException: hiện dialog và đánh dấu handled, cho ứng dụng sống tiếp.
- Các loại khác: hiện fatal dialog, flush log, không set handled; không cố tiếp tục sau lỗi không biết trạng thái.
- AppDomain unhandled: log fatal.
- Unobserved task exception: log rồi `SetObserved()`.

### Log file

`Services/LoggerService.cs:8-28`:

- Tạo thư mục log ngay trong static constructor.
- MinimumLevel Debug.
- File `app-.log` với rolling ngày.
- Retained file count lấy từ cấu hình `RetainDays` mặc định 30.
- Shared file bật.
- Mỗi record có timestamp tới mili giây và offset, level, message, exception.
- Không thấy pipeline log tập trung/metrics/health endpoint từ cấu hình này. Không suy ra có chức năng giám sát production.

## 12. Build, publish, CI và kiểm thử hiện có

### Artifact khi publish

`BusTicketSaleSystem.csproj:20-27`:

- Copy `Data/*.sql` khi mới hơn.
- Copy `appsettings.json` khi mới hơn.
- Liên kết `QR.jpg` từ root thành `Assets/BankTransferQr.jpg`, copy cả build và publish.
- File ảnh QR này là tài nguyên đi kèm, không phải bằng chứng giao dịch ngân hàng đã thành công.

### CI khai báo

`.github/workflows/build-test.yml:1-75`:

- Trigger push/PR main/master, manual, nightly cron `17 2 * * *`.
- Windows latest, setup .NET 10.0.x, restore.
- Thử tạo/start LocalDB; bước này `continue-on-error: true`.
- Verify format hai project.
- Build Release `-warnaserror`.
- Test Release với TRX và XPlat Code Coverage.
- Audit cả dependencies transitive theo báo cáo vulnerable của dotnet.
- Publish `win-x64`, `--self-contained false`: máy chạy vẫn cần Windows Desktop runtime tương ứng.
- Upload test/coverage ngay cả khi lỗi; upload build chỉ khi success.
- Không có bước deploy web server trong workflow này.

### Test framework và rủi ro thao tác

`BusTicketSaleSystem.Tests.csproj:4-15`: net10.0-windows, xUnit 2.9.3, test SDK 17.14.1, SkippableFact, coverlet.

`SqlServerFixture.cs:56-85,173-201`:

- Test fixture thay environment app/master sang DB test trước dùng helper.
- Reset bằng SINGLE_USER + DROP DATABASE + CREATE DATABASE đúng catalog `BusTicketSaleSystem_Tests`.
- Kiểm tra catalog trước helper destructive.
- Khi SQL không reachable hoặc migration không dùng được: ghi skip reason; các test phụ thuộc gọi `SkipIfUnavailable`.
- Vì vậy kết quả test không fail không đồng nghĩa integration đã chạy: phải đọc số skip và reason.
- Đợt trích xuất này không chạy fixture, không reset DB test.

### Điều chưa được xác nhận runtime

- SQL Server và quyền DB trên máy hiện tại.
- Các migrations đã áp dụng vào DB thực tế hay chưa.
- Merchant/email credentials có được cấu hình hoặc có hiệu lực hay không.
- Listener callback có thể bind port hiện tại không.
- Máy có đủ quyền ghi log/language/export.
- Toàn bộ UI render và hành vi tương tác thực tế.
- Kết quả pipeline CI gần nhất.

## 13. Ranh giới cần giữ khi chuyển web

Các điểm dưới đây là yêu cầu phân tích chuyển đổi, không phải tính năng web đã được triển khai:

1. Session singleton/current-user desktop phải chuyển thành ngữ cảnh từng request/session người dùng; không dùng một trạng thái global cho nhiều khách.
2. Bến máy trạm/environment không tự ánh xạ được sang nhiều nhân viên dùng chung một backend; cần định nghĩa bến được phép theo identity/assignment và vẫn enforce server-side.
3. Housekeeping phải tiếp tục hoạt động dù người dùng đóng browser.
4. Callback loopback desktop không phải callback URL public của web.
5. Lưu `language.json` ở executable không thích hợp làm preference chung cho mọi người dùng web.
6. In/PDF/PNG, mở maps và gửi email cần giữ ý nghĩa nghiệp vụ nhưng đổi cơ chế phân phối qua browser/backend.
7. Migration/seed lịch mẫu không được vô tình trở thành hành vi mỗi replica backend tự làm không kiểm soát.
8. Atomicity ghế, settlement idempotency và kiểm tra quyền phải giữ ở server/DB, không dịch nguyên UI guard sang JavaScript rồi bỏ SQL guard.
9. Bảng báo cáo và UI hiện tại là nguồn yêu cầu; không tự thêm đa vai trò, ví điện tử, cổng nhà xe, SMS hoặc reconciliation ngân hàng như thể đã có.
