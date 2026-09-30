# 02. Hiện trạng quản trị và điều hành

## 1. Phạm vi, cách đọc và mức xác minh

Tài liệu mô tả **hiện trạng implementation WPF**, không phải thiết kế web đề xuất và không thay đổi nghiệp vụ. Đã đọc XAML, code-behind, toàn bộ các ViewModel admin, `AdminDatabaseService`, `AdminModels`, các đoạn service thanh toán/check-in được gọi và các test liên quan. **Không chạy ứng dụng, SQL, build, formatter, linter hoặc test**; mọi luồng dưới đây là xác minh tĩnh từ source, **chưa xác minh runtime**. Test được liệt kê là test có trong repository, không có nghĩa đã chạy hoặc đang pass.

Chứng cứ ghi đường dẫn relative từ root repository. Để bảng không quá dài, các ký hiệu sau là đường dẫn chính xác, áp dụng cho mọi dẫn chứng dạng `ADB:321-350`:

| Ký hiệu | Đường dẫn relative root |
|---|---|
| ADB | `BusTicketSaleSystem/Data/AdminDatabaseService.cs` |
| AM | `BusTicketSaleSystem/Models/AdminModels.cs` |
| MAIN | `BusTicketSaleSystem/ViewModels/Admin/AdminMainViewModel.cs` |
| DASH | `BusTicketSaleSystem/ViewModels/Admin/AdminDashboardViewModel.cs` |
| ACC | `BusTicketSaleSystem/ViewModels/Admin/AdminAccountsViewModel.cs` |
| ROUTE | `BusTicketSaleSystem/ViewModels/Admin/AdminRouteManagementViewModel.cs` |
| BUS | `BusTicketSaleSystem/ViewModels/Admin/AdminBusManagementViewModel.cs` |
| TRIP | `BusTicketSaleSystem/ViewModels/Admin/AdminTripManagementViewModel.cs` |
| PAY | `BusTicketSaleSystem/ViewModels/Admin/AdminPaymentsViewModel.cs` |
| CHECK | `BusTicketSaleSystem/ViewModels/Admin/AdminCheckInViewModel.cs` |
| REPORT | `BusTicketSaleSystem/ViewModels/Admin/AdminReportsViewModel.cs` |
| PS | `BusTicketSaleSystem/Services/PaymentService.cs` |
| TLS | `BusTicketSaleSystem/Services/TicketLifecycleService.cs` |
| DBH | `BusTicketSaleSystem/Data/DatabaseHelper.cs` |
| LOC | `BusTicketSaleSystem/Services/LocationService.cs` |

Các trạng thái “implemented” nghĩa có code thực hiện; “không có trên màn hình” nghĩa không có control/command trong XAML và ViewModel đã đọc, không khẳng định mọi phần khác của ứng dụng đều không có. Không trích giá trị cấu hình bí mật; tên field cấu hình chỉ dùng giải thích cơ chế.

## 2. Inventory màn hình, actor và điều hướng

### 2.1. Các màn hình thực sự có

| Màn hình | View / ViewModel | Chức năng chính thực hiện |
|---|---|---|
| Vỏ admin | `Views/AdminWindow.xaml`, `MAIN` | Sidebar tám module; thông tin phiên; ngôn ngữ; phạm vi bến; đăng xuất; host thông báo |
| Dashboard | `Views/AdminDashboardView.xaml`, `DASH` | KPI toàn hệ thống; chuyến sắp chạy; vé gần nhất; occupancy đội xe; shortcut; môi trường DB |
| Tài khoản | `Views/AdminAccountsView.xaml`, `ACC` | Tìm tài khoản; nâng/hạ quyền; reset password; xóa có xác nhận |
| Tuyến | `Views/AdminRouteManagementView.xaml`, `ROUTE` | Tạo/sửa/xóa tuyến, khoảng cách, lọc bến |
| Đội xe | `Views/AdminBusManagementView.xaml`, `BUS` | Hồ sơ nhà xe/xe/tài xế/pháp lý/tuyến/giá/giờ; đăng ký đơn hoặc nhiều bản nháp; tự tạo ghế và lịch |
| Chuyến | `Views/AdminTripManagementView.xaml`, `TRIP` | Gán tuyến/xe, lịch đi/đến, giá; sửa/xóa; hủy chuyến có vé |
| Thanh toán | `Views/AdminPaymentsView.xaml`, `PAY` | Sổ thanh toán; lọc/tìm; xác nhận thu tiền; từ chối; duyệt hoàn; check-in |
| Soát vé | `Views/AdminCheckInView.xaml`, `CHECK` | Nhập/quét chuỗi mã vé; tra cứu; xác nhận lên xe; lịch sử phiên |
| Báo cáo | `Views/AdminReportsView.xaml`, `REPORT` | Khoảng ngày UTC; doanh thu theo tuyến; hoa hồng và tiền trả nhà xe |

Chứng cứ điều hướng đầy đủ: `MAIN:22-31,34-77`; `BusTicketSaleSystem/Views/AdminWindow.xaml:51-228`. Không có tab riêng cho nhà xe, nhân viên quầy, tài xế, phân quyền chi tiết, nhật ký audit, xuất file hay cấu hình tỷ lệ hoa hồng; nhà xe được xử lý trong màn đội xe.

### 2.2. Actor và kiểm tra quyền

- Actor thực sự được kiểm tra là **Admin**, không có role riêng “nhân viên soát vé”, “kế toán”, “điều hành”, “nhà xe”. Dù mô tả check-in nói “station staff”, service vẫn dùng cùng quyền admin. `CurrentUser.IsAdmin` so sánh Role với `Admin` không phân biệt hoa/thường (`BusTicketSaleSystem/Models/UserSession.cs:11-15`; `TLS:29-32`).
- Các public thao tác đọc/ghi trong ADB gọi `RequireAdmin`, hoặc overload xe chuyển sang method có guard; thiếu quyền ném `UnauthorizedAccessException`. Đây là enforcement C# trước SQL, không phải row-level security của SQL Server (`ADB:10-14,602-625,940-963`).
- Xác nhận/từ chối/hoàn tiền và check-in còn yêu cầu `adminAccountId > 0`, phiên tồn tại và AccountId truyền vào đúng với `CurrentUser.Account.AccountId` (`PS:461-467`). PAY kiểm tra có ID phiên; CHECK dùng fallback ID 1 khi phiên null, nhưng fallback **không bỏ qua** guard service (`PAY:173-183`; `CHECK:187-191`).
- Guard đọc quyền từ session in-memory, không re-query role DB ở mỗi thao tác. Đổi role tài khoản trong DB không cập nhật session của người đang đăng nhập bằng tài khoản ấy trong đoạn code này (`ADB:1308-1333`; `BusTicketSaleSystem/Models/UserSession.cs:11-15`). Không nên coi đây là thu hồi phiên tức thời.
- Tra cứu mã vé `DBH.GetTicketByCodeAsync` **không có guard admin/owner/station trong chính method**; màn soát vé nằm trong shell admin nhưng enforcement lookup không tương đương enforcement thao tác check-in (`DBH:1112-1130`).

### 2.3. Navigation, lifecycle và hành vi chung

- Mở `AdminWindow` tạo DI scope riêng, resolve `AdminMainViewModel`; constructor MAIN điều hướng Dashboard ngay. Navigator scoped; tất cả page admin transient. Mỗi lần điều hướng tạo page mới rồi gọi `IRefreshable.RefreshAsync` fire-and-forget có bắt/log lỗi; không phải giữ tất cả form như tab lâu dài (`BusTicketSaleSystem/Views/AdminWindow.xaml.cs:13-25`; `MAIN:76-77`; `BusTicketSaleSystem/Services/AppComposition.cs:27-50,59-64`; `BusTicketSaleSystem/Services/NavigationService.cs:52-97,120-129`).
- `ActiveTab` cập nhật từ type ViewModel; breadcrumb hiện chuỗi `Dashboard/Routes/Buses/Trips/Payments/CheckIn/Reports/Accounts`. Khi CurrentViewModel đổi, ScrollViewer cuộn về đầu (`MAIN:37-54`; `BusTicketSaleSystem/Views/AdminWindow.xaml:283-288`; `BusTicketSaleSystem/Views/AdminWindow.xaml.cs:28-33`).
- Đăng xuất phát RequestLogout; code-behind gọi `ISessionService.SignOut`; service xóa `CurrentUser.Account`, log và phát SignedOut (`MAIN:63`; `BusTicketSaleSystem/Views/AdminWindow.xaml.cs:19`; `BusTicketSaleSystem/Services/SessionService.cs:47-52`). Màn admin không thêm xác nhận “có thay đổi chưa lưu”.
- Chuyển ngôn ngữ gọi singleton LocalizationService, cập nhật mã VI/EN. Nhiều label sidebar dùng DynamicResource; đa số nội dung màn con là tiếng Anh/Việt hard-code, vì vậy không thể khẳng định toàn bộ admin được dịch khi toggle (`MAIN:17-18,64-69`; `BusTicketSaleSystem/Views/AdminWindow.xaml:69,91,293-307`; các XAML màn con).
- Header hiển thị tên đầy đủ, fallback username rồi “Quản trị viên”; role fallback “Admin”; badge bến lấy MachineLocation, không phải trường bến riêng của tài khoản (`MAIN:15-20`).
- `AdminFailure.Describe` log ngoại lệ; unauthorized thành thông báo hết quyền cần đăng nhập; SqlException thành lỗi DB từ chối; ngoại lệ khác nối `exception.Message`. Các tuple lỗi đã được ADB bắt không đi qua phân loại ngoại lệ này (`BusTicketSaleSystem/ViewModels/Admin/AdminFailure.cs:13-21`).
- Ngoại trừ CheckIn Loaded focus input, các code-behind màn con chỉ InitializeComponent; nghiệp vụ không nằm ẩn trong click handler (`BusTicketSaleSystem/Views/AdminCheckInView.xaml.cs:8-12`; các `Views/AdminAccountsView.xaml.cs:5-10`, `AdminPaymentsView.xaml.cs:5-10`, `AdminReportsView.xaml.cs:5-10`, `AdminDashboardView.xaml.cs:4`, `AdminRouteManagementView.xaml.cs:4`, `AdminTripManagementView.xaml.cs:4`, `AdminBusManagementView.xaml.cs:4`).

## 3. Phạm vi bến: UI và enforcement thực tế khác nhau

### 3.1. Nguồn phạm vi

- Mặc định “Thái Nguyên”; nếu có biến cấu hình `STATION_LOCATION`, normalize rồi pin station. Admin `CurrentLocation` luôn trả MachineLocation; `SetUserLocation` từ chối thay đổi khi admin. Có method cấu hình/test `SetFixedMachineLocation` (`LOC:26-29,56-59,73-86,93-124`).
- **Caveat implementation:** comment nói IP geolocation không được quyết định station của admin, nhưng code vẫn gán `MachineLocation = normalized` nếu `!IsStationPinned`, bất kể đang là admin. Setter MachineLocation cập nhật và phát event khi admin. Vì vậy chỉ cấu hình đã pin mới được bảo vệ khỏi thay đổi do geolocation; không suy từ comment rằng mọi máy đều cố định tuyệt đối (`LOC:37-47,134-165`).
- ADB scope C# dùng Contains không phân biệt hoa/thường; chấp nhận nếu một đầu tuyến chứa tên station; scope rỗng cho qua. SQL dùng `Origin LIKE @Scope OR Destination LIKE @Scope`, pattern `%station%` đã escape `[`, `%`, `_`; so sánh SQL còn phụ thuộc collation DB. Không phải liên kết khóa ngoại StationId (`ADB:22-61,1425-1428`).

### 3.2. Ma trận thực tế

| Nghiệp vụ | UI/list | Service/SQL |
|---|---|---|
| Xem tuyến | ROUTE lọc một đầu Contains station | GetRoutesAsync trả **mọi tuyến**, chỉ require admin |
| Thêm tuyến | Kiểm tra một đầu trong bến | AddRouteAsync kiểm tra input trong bến trước INSERT |
| Sửa tuyến | Chỉ chọn hàng đã lọc; kiểm tra đầu mới | Chỉ kiểm tra **giá trị đầu tuyến mới**; không xác minh tuyến cũ theo ID đang trong bến |
| Xóa tuyến | Hàng từ list đã lọc | DeleteRouteAsync **không có scope predicate** |
| Xem chuyến | TRIP lọc chuỗi Route Contains station; route dropdown scoped | GetTripsAsync trả mọi chuyến; GetBusesAsync mọi xe |
| Thêm chuyến bằng form chuyến | Route dropdown scoped, kiểm tra nếu tìm thấy route | INSERT có RouteInStationScope trong cùng statement |
| Sửa chuyến | Hàng đã lọc và route dropdown scoped | SQL kiểm tra cả tuyến cũ lẫn tuyến đích cùng scope |
| Xóa chuyến | Hàng đã lọc | DELETE có scope EXISTS |
| Hủy chuyến | Chỉ hàng trong list đã lọc | CancelTripAsync **không kiểm tra station** |
| Xe/nhà xe/tự sinh lịch | Không lọc bến; AvailableRoutes là mọi tuyến | Register/Update/DeleteBus và internal schedule **không có scope guard** |
| Dashboard, Accounts, Payments, Reports | Không lọc bến | Truy vấn toàn hệ thống |
| Tra cứu/check-in | Nhập mã bất kỳ; không chọn chuyến/bến | Lookup/check-in không station predicate |

Chứng cứ: `ROUTE:96-109,144-180`; `TRIP:116-142,155-198`; `ADB:307-414,522-600,627-764,855-934,969-1010,1016-1192,1198-1279`; `BUS:427-455`; `ADB:68-301,1285-1305`; `DBH:1112-1130`; `TLS:29-67`.

**Kết luận hiện trạng:** badge “cố định” và banner “chỉ điều hành trong bến” không chứng minh toàn bộ admin đã station-isolated. Khi chuyển web phải mang theo sự phân biệt này như hiện trạng/gap, không âm thầm mô tả thành policy đã được enforce ở mọi API.

## 4. Dashboard / tổng quan vận hành

### 4.1. Giao diện và command

- Header “Operations Dashboard”; LastRefreshed; Refresh; New Trip (chỉ điều hướng màn chuyến, không mở dialog hay tạo sẵn bản ghi).
- Tám card: Total revenue, Awaiting settlement, Refunded, Tickets sold, Schedules, Bus fleet, Active routes, Customers.
- Bảng upcoming: tuyến + ngày, biển số, khung giờ và departure đầy đủ, giá. Bảng recent bookings: mã vé/thời gian đặt, hành khách và SĐT, tuyến, ghế, amount, trạng thái.
- Quick actions tới tuyến, đội xe, chuyến, payment, báo cáo; Fleet Occupancy có biển số, loại xe, số lượng/tỷ lệ và progressbar; System Environment hiển thị SQL Server instance, catalog, LastRefreshed, tên admin. Không có biểu đồ doanh thu chuỗi thời gian, filter ngày hoặc filter station ở Dashboard.
- Banner lỗi có nút đóng; progress loading; empty state riêng cho các list. Chứng cứ: `BusTicketSaleSystem/Views/AdminDashboardView.xaml:19-249,265-471,479-675`; `DASH:69-86`.

### 4.2. Tải và công thức KPI

Refresh chạy bốn read độc lập trên bốn connection đồng thời: stats, 6 booking mới, 5 chuyến, toàn bộ xe rồi Take(5). Chỉ sau Task.WhenAll thành công mới thay collections; thành công hiển thị `Dữ liệu lúc HH:mm:ss` theo giờ local, xóa message; lỗi đổi timestamp thành “Không tải được dữ liệu”, dùng AdminFailure, hạ IsLoading. Đây không phải snapshot transaction nhất quán bốn truy vấn, cũng không có polling “real-time” riêng trong ViewModel (`DASH:89-132`).

| KPI | Công thức SQL chính xác |
|---|---|
| RouteCount | COUNT toàn bộ Routes; không cờ active |
| BusCount | COUNT toàn bộ Buses |
| TripCount | COUNT Trips có DepartureTime >= SYSUTCDATETIME; **không loại Cancelled** |
| TicketsSold | COUNT Tickets có Status `Đã thanh toán` hoặc `Đã sử dụng`; không SUM SeatCount |
| TotalRevenue | SUM Payments.Amount hiện Status Success trừ SUM Amount hiện Status Refunded |
| CustomerCount | COUNT Accounts có Role Customer hoặc Role NULL |
| PendingSettlementCount / Amount | COUNT/SUM Payments hiện Pending; không tự loại deadline hết hạn |
| RefundedAmount | SUM Payments.Amount hiện Refunded |

Nguồn `ADB:68-98`; formatting VND N0 và pending caption số ít/số nhiều ở `AM:186-205`.

### 4.3. Ý nghĩa dữ liệu và caveat

- RecentBookings sắp BookingTime DESC, TOP được clamp 1..100; không lọc trạng thái, nên “recent purchases” gồm cả giữ chỗ/hủy. Amount là `tk.Price * tk.SeatCount`; ghế STRING_AGG theo SeatId; không có ghế dùng dấu gạch. PassengerDisplay ghép SĐT nếu có (`ADB:101-130`; `AM:164-179`).
- “UpcomingTrips” không WHERE tương lai: ưu tiên nhóm tương lai trước, sau đó các chuyến quá khứ, tất cả theo DepartureTime ASC; nếu không đủ chuyến tương lai sẽ lấp bằng chuyến quá khứ sớm nhất. Không chọn Status, do đó constructor TripAdminItem dùng default Scheduled; không loại chuyến Cancelled. Bản ghi đọc DateTime chưa SpecifyKind UTC, khác GetTripsAsync (`ADB:133-159`; `AM:136-145`; `ADB:1029-1035`).
- Occupancy không phải ghế bán của một chuyến: UsedSeats SQL là COUNT DISTINCT **TicketId** của vé Đã đặt/Đã thanh toán/Đã sử dụng tham chiếu ghế xe qua mọi thời gian, không SUM số ghế và không filter chuyến hiện tại. Model lấy `round(UsedSeats/SeatCount*100)` rồi clamp 0..100; SeatCount <= 0 trả 0. Vé nhiều ghế vẫn tính một TicketId, vé lịch sử vẫn tham gia. Tên UI “seat utilization” rộng hơn ngữ nghĩa query (`ADB:421-450`; `AM:37-39`).
- DB server/catalog được parse từ connection string qua SqlConnectionStringBuilder, không hiển thị password trong code; parse lỗi dùng nhãn không hợp lệ. Tài liệu không ghi giá trị thực (`DASH:16,63-67,134-147`).

## 5. Tài khoản / Accounts

### 5.1. Fields và trạng thái màn hình

- Search TextBox cập nhật mỗi ký tự, lọc in-memory không phân biệt hoa/thường trên username/full name/email/phone; trim search, rỗng trả toàn bộ. Không filter role, không paging, không tạo account hoặc sửa tên/email/phone trong module này (`ACC:40-47,123-156`).
- List đọc toàn bộ Accounts ORDER BY CreatedAt DESC; fields model: AccountId, Username, FullName, Email nullable, Phone nullable, Role, CreatedAt. XAML hiện ID, username, tên đầy đủ, email, phone, role, actions; CreatedAt không có cột dù model có display property (`ADB:1285-1305`; `AM:207-220`; `BusTicketSaleSystem/Views/AdminAccountsView.xaml:132-219`).
- Badge model `IsAdmin` dùng Role == `Admin` phân biệt hoa/thường, khác session IsAdmin; mọi role khác hiện Customer. Action đổi role tương ứng Promote to admin / Demote to customer (`AM:215-219`).
- Ba action mỗi hàng: toggle role, Reset password, Delete. Reset panel có prompt, NewPassword **TextBox thường, không PasswordBox**, Apply, Cancel. Delete có prompt tên username, Confirm delete/Keep account. Không yêu cầu nhập lại password admin, OTP hoặc xác nhận riêng khi tăng quyền (`BusTicketSaleSystem/Views/AdminAccountsView.xaml:67-124,192-213`; `ACC:94-121`).

### 5.2. Luồng đổi quyền

1. Từ row model chọn target `Customer` nếu IsAdmin, ngược lại `Admin`.
2. Gọi UpdateAccountRoleAsync. Guard service: admin session; newRole chỉ Customer/Admin; không được tự hạ mình thành Customer.
3. SQL UPDATE Role theo AccountId; không thấy account báo không còn tồn tại.
4. Thành công ghi AuditLog action AccountRoleChanged target Account, detail role mới và file log; VM message rồi refresh.
5. SQL lỗi trả lỗi chung; ngoại lệ khác qua AdminFailure. Không cập nhật role trong CurrentUser hoặc thông báo thu hồi phiên cho session khác.

Chứng cứ `ACC:158-171`; `ADB:1308-1333`.

### 5.3. Reset password

- Begin reset đóng pending delete, xóa NewPassword và message, giữ target. Cancel xóa target và NewPassword. Prompt nói minimum 8 nhưng policy service đầy đủ hơn.
- Apply gọi service; password không trắng; dài 8..128 ký tự và có ít nhất chữ hoa, chữ thường, chữ số theo `char.IsUpper/IsLower/IsDigit`. Không yêu cầu ký tự đặc biệt, không hỏi password hiện tại, không có confirm-password field (`ACC:64-66,98-111,174-191`; `ADB:1337-1343`; `DBH:1290-1294`).
- Hash PBKDF2 SHA256 với salt ngẫu nhiên, định dạng tự mô tả; chỉ UPDATE PasswordHash; không đổi CreatedAt/Role, không có cờ buộc đổi password lần đăng nhập sau trong luồng này. Success audit AccountPasswordReset và log; không tồn tại account lỗi riêng; SQL lỗi thông báo reset thất bại (`ADB:1345-1363`; `DBH:1189-1194`).
- Thành công panel đóng, secret trong VM được xóa; không refresh list vì fields list không đổi. Thất bại giữ panel để sửa. Khi BeginDelete đóng target reset, code không đồng thời clear NewPassword, khác Cancel reset (`ACC:112-118,174-191`).

### 5.4. Xóa account

- UI hai bước; target captured; xác nhận gọi service; dù result fail hay exception, PendingDelete bị xóa. Success refresh; fail giữ list và message (`ACC:112-120,194-209`).
- Service không cho xóa account đang đăng nhập.
- SQL kiểm tra theo thứ tự: không tồn tại; có Tickets Status khác Đã hủy; có bất kỳ lịch sử Tickets kể cả hủy; có Payments mà account từng ConfirmedBy/RefundApprovedBy; rồi mới DELETE Accounts. “Cancel vé trước khi xóa” trong thông báo không đồng nghĩa sẽ xóa được sau hủy, vì guard lịch sử kế tiếp vẫn chặn.
- Không soft-delete/disable/lock account trong module. Bản ghi không có các tham chiếu trên vẫn có thể bị FK khác từ DB từ chối, khi đó trả thông báo SQL chung. Thành công ghi audit AccountDeleted, file log (`ADB:1370-1416`).
- Account role/reset/delete audit được ghi **sau mutation, không nằm cùng transaction**; overload AuditLog(connection,null) không nuốt SQL exception. [INFERENCE] Có thể thay đổi dữ liệu đã commit nhưng UI nhận lỗi nếu bước ghi audit thất bại. Chứng cứ ranh giới transaction `ADB:1322-1332,1354-1362,1397-1415`; `BusTicketSaleSystem/Data/AuditLog.cs:42-47`.

## 6. Tuyến đường / Routes

### 6.1. Màn hình và input

Form luôn có Origin, Destination, Distance (km), nút Add route khi không editing hoặc Save changes/Cancel khi editing; Refresh; banner scope; list DisplayName `Origin → Destination`, RouteId, DistanceDisplay (`N1 km` hoặc `Not set`), Edit/Delete; confirmation panel xóa; loading và empty. Không field IntermediateStops trên form tuyến dù dữ liệu Routes có thể có stops ở luồng khác. Không search, pagination hay sort tương tác (`BusTicketSaleSystem/Views/AdminRouteManagementView.xaml:17-197`; `AM:5-9`).

- Mới khởi tạo Origin/Destination/Distance rỗng; không tự điền station ngay constructor. Cancel edit/ResetForm mới đặt Origin=StationScope, Destination và Distance rỗng (`ROUTE:13-15,69-94,122-129`).
- Chọn Edit đóng pending delete, lưu route ID, copy hai đầu và distance F1; set IsEditing/message. Add/update thành công xóa fields thành rỗng rồi refresh; update thoát editing. Fail giữ form (`ROUTE:75-85,144-197`).
- Distance rỗng => null; không parse được decimal => thông báo kilomet. Parse theo culture mặc định; kiểm tra âm ở service, 0 hợp lệ (`ROUTE:131-141`; `ADB:326-328,358-360`).

### 6.2. Quy tắc ghi dữ liệu

| Thao tác | Validation/guard | Mutation và lỗi |
|---|---|---|
| Add | Trim Origin/Destination; cả hai không trắng; khác nhau không phân biệt hoa/thường; distance không âm; input trong bến | INSERT SELECT nếu chưa có cùng Origin và Destination. Thành công đúng 1 row; 0 row báo tuyến đã có; SqlException log và lỗi chung |
| Update | Cùng validation; scope xét dữ liệu mới | SQL báo ID không còn; báo tuyến khác có cùng hai đầu; nếu hợp lệ update ba field. Không cấm sửa tuyến đang có Trips/Tickets |
| Delete | RequireAdmin; SQL không có scope | DELETE nếu không tồn tại **bất kỳ Trips** tham chiếu route, không chỉ scheduled dù thông báo nói scheduled. Không tồn tại ID cũng thành thông báo “còn chuyến”; SQL lỗi khác trả chung |

Nguồn `ADB:321-414`. Không có transaction/UPDLOCK chống race trùng tuyến trong Add/Update ở chính implementation này; không suy NOT EXISTS thành bảo đảm concurrency. DB constraints cuối cùng xem báo cáo schema.

### 6.3. Side effects và trạng thái

- Tạo/sửa chỉ Routes, không tự tạo xe/chuyến, không audit chuyên biệt. Sửa tên hai đầu thay đổi nội dung route hiển thị của mọi truy vấn JOIN về Routes, không cập nhật giá/lịch chuyến đã lưu (`ADB:331-373`; query JOIN ở `ADB:105-115,1020-1023`).
- Xóa là hard delete có xác nhận; fail/success đều đóng confirmation; chỉ success refresh (`ROUTE:199-214`). Không có kích hoạt/ngừng khai thác tuyến, versioning hoặc archive trong module.

## 7. Đăng ký và quản lý đội xe / Buses

### 7.1. Layout và inventory field

Màn hình ban đầu ẩn form; nút Đăng ký xe mới mở/reset form, Đóng form chỉ ẩn khi không editing; mở lại sẽ reset form. Nút Làm mới tải list. Form có phần nhà xe dùng chung và phần một hoặc nhiều xe bản nháp; footer đăng ký toàn đội hoặc lưu thay đổi xe (`BUS:143-154,305-323,468-513`; `BusTicketSaleSystem/Views/AdminBusManagementView.xaml:12-47,460-502`).

| Nhóm | Controls và binding trên XAML | Ghi chú lưu trữ |
|---|---|---|
| Nhà xe | CompanyName ComboBox editable; RegisteredEntity; CompanyHotline; CompanyOperatingArea; CompanyAmenities; CompanyDescription | CompanyName/RegisteredEntity ghi Buses; profile ghi/upsert BusCompanies; Description chỉ được truyền ở nhánh batch draft, không trong BuildInput đơn/sửa |
| Nhận dạng xe | Plate, SeatCount, BusType ComboBox, OperationType ComboBox, Make, Model, Color | BusType: Limousine/Ghế ngồi/Ghế nằm/Cabin; OperationType: Tuyến cố định/Hợp đồng/Du lịch |
| Tài xế và pháp lý hiện trên form | DriverName, DriverPhone, InspectionNumber, InspectionExpiryDate DatePicker, InsuranceExpiryDate DatePicker | Không phải bảng tài xế riêng; mỗi xe lưu tên/SĐT |
| Tuyến | SelectedOrigin editable từ danh mục tỉnh; SelectedDestination editable; IntermediateStops editable + presets; nút Nối trạm | Match route bằng equality hai đầu, không bằng station ID |
| Giá | FarePrice toàn tuyến, StagePrice mỗi chặng | Parse decimal; chỉ >0 mới truyền giá, lỗi/rỗng/âm/0 => null, không báo validation giá |
| Biểu đồ giờ | DepartureTimeNote text; preset sáng/trưa/chiều/tối, mỗi 1h, mỗi 2h, xóa giờ | Là chuỗi rồi service parse TimeSpan |
| Draft | Danh sách xe chuẩn bị, Sửa/Xóa từng draft, Thêm xe mới, Lưu xe này vào danh sách, Đóng mục nhập xe | In-memory, chưa ghi DB tới lúc đăng ký toàn bộ |

Chứng cứ controls: `BusTicketSaleSystem/Views/AdminBusManagementView.xaml:49-122,124-336,338-454`; property/options: `BUS:18-90,128-243`; input construction: `BUS:791-817,844-875`.

**Field có trong model/VM/service nhưng không có ô nhập riêng ở XAML hiện tại:** ManufactureYear, RegistrationNumber, RegistrationDate vẫn được default/copy/lưu; form không cho sửa trực tiếp ba field này. Model còn FullPrice/StagePrice/FareDisplay nhưng fleet row hiện tại không bind FareDisplay. SQL danh sách lấy FullPrice từ chuyến có TripId lớn nhất, không từ field giá toàn tuyến của Buses (`BUS:37-43,235-241,391-397`; `AM:11-45`; `ADB:444-445`; `BusTicketSaleSystem/Views/AdminBusManagementView.xaml:594-623`).

### 7.2. Defaults và auto-fill

- Form mặc định 40 ghế, Limousine, Hyundai Universe, năm 2023, màu Trắng - Xanh; ngày đăng ký Today, đăng kiểm Today+2 năm, bảo hiểm Today+1 năm; tuyến cố định; fare 150000, stage 70000; khung giờ 06:30/09:00/14:30. Các default về tài xế/đơn vị là dữ liệu mẫu hard-code, không phải “đã xác minh hồ sơ” (`BUS:18-45,468-502`).
- Đổi CompanyName khi không editing khởi chạy auto-fill không await: lấy RegisteredEntity và DriverPhone từ xe đầu tiên cùng company; sau đó query BusCompanyByName và fill hotline/area/description/amenities nếu có. Lỗi auto-fill bị catch trống. Không có cancellation hoặc so sánh tên đang chọn sau await trong method (`BUS:128-137,515-545`).
- SelectForEdit gán CompanyName **trước khi** set IsEditing=true, nên có thể kích hoạt auto-fill nền; không copy `b.FullPrice`/`b.StagePrice` vào field giá và không reset tất cả profile fields trước khi edit. [INFERENCE] Giá và profile lưu sau sửa có thể đến từ form/default/auto-fill, không phải chính giá cũ của xe. Chứng cứ `BUS:376-415`.
- Danh sách origin dùng VietnamProvinces.List; destinations bỏ đúng origin bằng equality không phân biệt hoa/thường. Control vẫn editable nên không giới hạn user vào danh mục. AvailableRoutes lấy mọi tuyến; match exact Origin/Destination không phân biệt hoa/thường; nếu thiếu một đầu SelectedRoute=null (`BUS:181-209,427-454`).
- Khi match route và FarePrice rỗng hoặc bằng chuỗi `150000`: `estPrice = max(100000, round(distance*1200/10000)*10000)` với distance null =>100 km; stage estimate `max(50000, round(estPrice*0.45/10000)*10000)`. Không tự thay giá đã nhập khác 150000 (`BUS:211-227`).

### 7.3. Preset điểm dừng và giờ

- Preset stops có nhóm hai chiều Hà Nội–Thái Nguyên, Thái Nguyên–Tuyên Quang, Hà Nội–Tuyên Quang, Hà Nội–Hải Phòng, Hà Nội–Lào Cai hoặc có Sa Pa; còn lại gợi ý chung origin/bến xuất phát, trạm cao tốc, destination/bến trả. Không tìm tuyến/map API. Nếu IntermediateStops đang rỗng tự chọn preset đầu. Ví dụ nhóm Thái Nguyên–Tuyên Quang gồm Bờ Đậu, Đại Từ, Sơn Dương, Cầu Chả, bến Tuyên Quang (`BUS:596-666`).
- SelectedStopOption setter **thay toàn bộ IntermediateStops** bằng giá trị vừa chọn. Nối trạm lấy parameter hoặc SelectedStopOption, split existing bằng comma/semicolon, trim, không thêm nếu đã có case-insensitive; ghép lại comma. Không lưu điểm dừng thành entity hoặc thứ tự có tọa độ (`BUS:78-88,366-370,668-681`).
- Preset sáng thêm 06:00,08:30; trưa 11:00,13:30; chiều 15:30,17:30; tối 19:30,21:00. Append split `,;|`, bỏ trùng chuỗi case-insensitive, sort chuỗi, ghép comma. Preset 1h **ghi đè** 06:00..20:00 cách 1h; 2h ghi đè 06:00..20:00 cách 2h; Xóa giờ đặt rỗng (`BUS:324-336,547-561`).

### 7.4. Fleet list và bộ lọc

Refresh chạy song song xe/companies/routes; list xe không scoped; companies UNION CompanyName của BusCompanies với distinct Buses. Nếu query companies lỗi hoặc trả rỗng, service log rồi trả năm tên mẫu để dropdown không rỗng — không được coi đó là dữ liệu DB đã tồn tại (`BUS:427-465`; `ADB:493-519`).

Filter nhà xe “Tất cả nhà xe” hoặc equality case-insensitive; search mỗi ký tự trên Plate/RouteName/DriverName/BusType, trim và Contains. Summary khi chọn company = số xe đã lọc và số route distinct đã lọc; khi all companies = số xe đã lọc nhưng số companies distinct của **toàn bộ Buses**, không phải subset tìm kiếm. Empty flag HasNoBuses dùng Buses gốc, không FilteredBuses nên tìm không có kết quả có thể chỉ list trắng, không hiện empty-state riêng (`BUS:98-120,256-257,563-595`).

Row hiện: biển số, operation type; nhà xe/tài xế/SĐT; tuyến/stops/giờ; specs/SeatCount/RegistrationNumber/InspectionNumber; Sửa hồ sơ/Xóa. Không có cảnh báo màu quá hạn đăng kiểm/bảo hiểm trên list (`BusTicketSaleSystem/Views/AdminBusManagementView.xaml:537-657`).

### 7.5. Draft workflow nhiều xe

1. Nhập company và cấu hình xe; SaveVehicleDraft chỉ validate biển số không trắng, seat integer 1..200. Không validate duplicate plate giữa drafts, tài xế/SĐT, giá, ngày pháp lý, thời gian, route scope.
2. Copy field từng xe sang BusDraftItem; origin rỗng fallback Hà Nội, destination rỗng fallback Thái Nguyên; giữ routeId nếu matched. Thêm draft, reset **chỉ** plate/registration/inspection và tài xế, rồi ẩn panel xe. Company, tuyến, cấu hình xe, giá và khung giờ được giữ để nhập tiếp (`BUS:683-727,504-513`).
3. Thêm xe mới chạy cùng reset hạn chế; mở panel. Đóng mục nhập chỉ ẩn khi đã có ít nhất một draft; không discard dữ liệu panel. Xóa draft khỏi collection chưa tác động DB (`BUS:339-364`).
4. Edit draft copy về form rồi **remove khỏi DraftBuses ngay**, mở panel; chỉ khi SaveVehicleDraft mới thêm lại. Không có snapshot để khôi phục draft nếu bỏ nhập sau edit (`BUS:729-756`).
5. SaveAllBuses bắt buộc CompanyName không trắng. Nếu không draft, gọi AddAsync đơn. Nếu có draft, lặp tuần tự `DraftBuses.ToList()`; mỗi xe có transaction riêng service, **không transaction toàn đội** (`BUS:759-842`).
6. Draft chưa có routeId và đủ hai đầu: thử AddRouteAsync rồi GetRoutesAsync, match lại dù AddRoute trả lỗi. Không chặn nếu vẫn routeId null. Sau đó build input, truyền profile company hiện tại chung cho tất cả draft; Description có được truyền ở nhánh này. Số ghế không hợp lệ khi build fallback40; năm <=1900 fallback2023; giá <=0/parse fail =>null; defaults pháp lý tương tự đơn (`BUS:773-817`).
7. Với mỗi RegisterBus result, chỉ tăng successCount nếu success; **không hiển thị/giữ result.Error của xe fail**. Nếu không exception, refresh rồi clear toàn bộ drafts, ResetForm (xóa CompanyName), đóng form, hiện “thành công toàn bộ {successCount}” với CompanyName đã reset. [INFERENCE] Batch có thể ghi thành công một phần nhưng giao diện vẫn dùng lời “toàn bộ”, mất danh sách draft fail và mất tên nhà xe trong thông báo. Đây là hành vi code, không phải đảm bảo all-or-nothing (`BUS:819-836`).
8. Nếu exception, hiện AdminFailure và hạ loading; các xe đã commit trước đó không rollback; drafts chưa được clear ở nhánh catch. [INFERENCE] Retry nguyên danh sách có thể gặp plate duplicate cho xe đã ghi trước (`BUS:769-837`; `ADB:588-593`).

### 7.6. Add/update một xe và validation

- AddAsync/UpdateAsync bắt buộc plate, seat integer 1..200. Khi route chưa matched nhưng có hai đầu, thử tạo route distance null; **chỉ reload/match khi AddRoute.Success**, lỗi tạo route không được show hay dừng thao tác xe. Vì vậy vẫn có thể register/update xe RouteId null khi route bị scope/duplicate/validation từ chối (`BUS:878-905,936-964`).
- BuildInput trim chuỗi; thiếu company fallback Sao Việt (nhánh SaveAll đã check company), tài xế “Chưa phân công”, phone dấu gạch, giờ trống “Theo biểu đồ bến”; make/model fallback Hyundai/Universe, màu Trắng; registration number fallback plate, inspection number KD-001; ngày null fallback Today/+2y/+1y; entity fallback đơn vị vận tải; operation fallback tuyến cố định; fare/stage positive hoặc null. CompanyDescription **bị bỏ khỏi BuildInput**, mặc dù có ô nhập (`BUS:844-875`).
- Service Register/Update kiểm tra plate không trắng và SeatCount1..200 lần nữa; bus type rỗng dùng Make+Model hoặc Standard; năm <=1900 dùng2023. Không có format biển số, regex phone, ràng buộc tuổi xe, expiry phải tương lai, arrival/schedule của xe, stage<=full, positive fare trực tiếp ở hai service này; các cột/constraint SQL khác là lớp cuối (`ADB:522-568,627-653,698-722`).
- Add success refresh, rồi reset/đóng hoặc giữ form cho xe tiếp nếu `keepFormOpenForNextBus`. Có AddCommand/AddAndContinueCommand trong VM nhưng **XAML hiện tại không bind hai nút này trực tiếp**; luồng thêm từ footer SaveAll, không draft => AddAsync(false). Update success ResetForm và đóng; message success vừa set bị ResetForm xóa. Fail giữ form (`BUS:319-321,909-928,963-976`; `BusTicketSaleSystem/Views/AdminBusManagementView.xaml:474-498`).

### 7.7. Transaction đăng ký, đổi sức chứa và hồ sơ nhà xe

**RegisterBusAsync:** mở connection/transaction; INSERT Buses OUTPUT BusId; tạo Seats 1..SeatCount còn thiếu bằng recursive CTE; upsert company nếu name không trắng; nếu RouteId>0 tự sinh chuyến; commit. SqlException duplicate2601/2627 rollback và nói biển số đã có; các lỗi SQL khác rollback lỗi đăng ký chung. Không ghi audit chuyên biệt cho xe (`ADB:530-599,772-782`).

**UpdateBusDetailsAsync:** trong transaction đọc SeatCount cũ, số plate duplicate ở xe khác, số tham chiếu ghế >capacity mới với vé chưa hủy, rồi mọi vé lịch sử; không tồn tại bus => rollback. Duplicate => rollback. Giảm ghế: có vé hoạt động vượt capacity từ chối; chỉ còn vé hủy vượt capacity vẫn từ chối để giữ lịch sử. Ghế có SeatNumber không convert được int không lọt điều kiện `TRY_CONVERT(INT)>@Count`. Update mọi field Buses; giảm thì DELETE Seats số lớn; tăng hoặc bằng thì top-up các số còn thiếu. Upsert company và tự sinh lịch lại nếu có route; commit (`ADB:635-750`).

**Company upsert:** nếu name mới, tạo BusCompanies với hotline fallback, rating4.8/reviewCount120, area từ input hoặc route hoặc Toàn quốc, description/amenities mẫu nếu thiếu, logo viết tắt và màu palette từ hash tên. Existing company chỉ thay hotline/area/description/amenities khi input không rỗng; không sửa CommissionRate/rating/reviewCount/brand. Không có form tạo company riêng ngoài side effect này (`ADB:784-853`). Các rating/reviewCount mẫu không phải kết quả tổng hợp review thực tế trong thao tác đăng ký.

### 7.8. Tự sinh lịch: quy tắc khác form chuyến thủ công

`ScheduleTripsForBusInternalAsync` chạy trong transaction của register/update xe (`ADB:855-934`):

1. Distance default100km; đọc route DistanceKm nếu decimal>0. Duration phút = `max(60, round(distance/50*60))` — mô hình tốc độ50km/h, tối thiểu1h.
2. Fare: customPrice>0 ưu tiên; nếu không, lấy Price của chuyến gần nhất theo TripId trên cùng route nếu >0; nếu không `max(100000, round(distance*1200/10000)*10000)`. StagePrice truyền nguyên nullable input.
3. Parse DepartureTimeNote split comma/semicolon/pipe, TimeSpan.TryParse; token lỗi bị bỏ. Nếu không có token hợp lệ, thêm07:30 và14:00. Không validate hh:mm trong ngày, không chuẩn hóa hoặc deduplicate danh sách tại đây.
4. Lặp day0..14 **bao gồm cả hôm nay và ngày thứ14**; mỗi time ghép DateTime.Today local; bỏ departure<=DateTime.Now. Arrival=departure+duration.
5. Chỉ bỏ insert nếu đã có cùng BusId, RouteId, DepartureTime. Không kiểm tra khoảng chồng lấn thời gian, không gọi AddTripAsync, không route scope và không normalize UTC; insert không chỉ rõ Status, dựa vào default SQL Scheduled. Update xe chỉ thêm slot thiếu, không xóa/sửa chuyến cũ khi thay giờ/route/giá.

Caveat quan trọng: form ghi “14 ngày tới”, nhưng loop15 ngày lịch gồm phần còn lại hôm nay; đường auto schedule dùng local DateTime đưa thẳng SQL, còn form chuyến thủ công chuyển UTC. [INFERENCE] Có thể lệch giờ hiển thị/điều kiện departure khi máy không UTC và có thể tạo lịch overlap mà màn chuyến thủ công từ chối. Unique index DB vẫn có thể từ chối slot trùng theo guard khác; không coi internal NOT EXISTS là toàn bộ constraint.

### 7.9. Xóa xe

UI hai bước với cảnh báo xóa xe và seat map; cancel không mutation; confirm gọi DeleteBusAsync, đóng pending confirmation dù fail, success refresh (`BUS:417-424,979-994`; `BusTicketSaleSystem/Views/AdminBusManagementView.xaml:504-529`).

SQL `SET XACT_ABORT ON`; kiểm tra xe tồn tại, mọi Trips tham chiếu xe, mọi TicketSeats tham chiếu seat của xe; bị chặn có thông báo riêng. Nếu qua: BEGIN TRANSACTION; DELETE Seats rồi Buses; COMMIT. Không xóa trips/tickets/company cascade bằng code. Guard có UPDLOCK/HOLDLOCK nhưng BEGIN TRANSACTION nằm **sau** các IF preflight; không nên tuyên bố toàn bộ preflight được giữ khóa trong một transaction. SQL lỗi log + trả false; XACT_ABORT giúp không để seat map bị mất khi DELETE Buses fail (`ADB:969-1009`).

## 8. Lập lịch, sửa, xóa và hủy chuyến / Trips

### 8.1. Fields, list, defaults

- Route dropdown DisplayName/RouteId, Bus dropdown LicensePlate/BusId; Departure/Arrival TextBox format `yyyy-MM-dd HH:mm` cập nhật LostFocus; Price VND cập nhật từng ký tự. Không DateTimePicker riêng và không validator định dạng ngày custom trong VM (`BusTicketSaleSystem/Views/AdminTripManagementView.xaml:56-126`).
- Default: ngày mai07:00 đi,10:00 đến, price150000; sau tải nếu selected IDs=0 chọn route/xe đầu tiên. Routes/trips lọc station, buses không lọc. Query ORDER BY departure DESC, không riêng “sắp chạy” dù header danh sách đã lên lịch (`TRIP:18-22,116-142`; `ADB:1016-1036`).
- Hàng: tuyến/badge trạng thái, biển số, schedule, giá, Hủy chuyến nếu CanCancel, Edit, Delete. Không filter ngày/status/text, không paging, không xuất danh sách hành khách, không command “xuất bến” (`BusTicketSaleSystem/Views/AdminTripManagementView.xaml:165-248`).
- StatusDisplay: Cancelled=>Đã hủy, Departed=>Đã xuất bến, mọi giá trị khác=>Đang chạy (bao gồm Scheduled). CanCancel chỉ Scheduled và DepartureTime>UtcNow. Date/time display model gọi ToLocalTime. GetTrips chỉ rõ KindUtc khi đọc SQL (`AM:136-162`; `ADB:1029-1035`).

### 8.2. Thêm thủ công

1. VM scope check nếu route được tìm thấy trong collection; parse decimal Price hoặc báo Fare must be a number.
2. Service RequireAdmin; arrival phải >departure; fare không âm (0 hợp lệ); convert non-UTC DateTime.ToUniversalTime.
3. SQL `SET XACT_ABORT ON; BEGIN TRANSACTION` rồi INSERT SELECT Scheduled khi route/bus tồn tại, route trong station, và không có Trips cùng bus thỏa `DepartureTime < newArrival AND ArrivalTime > newDeparture` với UPDLOCK/HOLDLOCK; COMMIT.
4. Khoảng kề nhau arrival cũ==departure mới không overlap. Không loại Cancelled/Departed trong overlap; không yêu cầu departure ở tương lai; không giới hạn duration hoặc bus phải thuộc route đã gán trong Buses.
5. Price SqlDecimal Precision10/Scale0. Nếu đúng1row thành công; 0row query route riêng phân biệt out-of-scope với overlap/route-bus invalid. Unique duplicate2601/2627 trả lỗi overlap chung; SqlException khác lỗi tạo chuyến chung.
6. VM success refresh giữ fields, không reset form.

Chứng cứ `TRIP:155-181`; `ADB:1039-1091`.

### 8.3. Sửa chuyến

- Edit copy RouteId/BusId, Departure/Arrival **nguyên DateTime UTC từ list** vào form; Price F0; đóng pending delete. XAML form format DateTime trực tiếp, khác property Schedule chuyển local. [INFERENCE] Cần lưu ý khác biệt giữa giờ trên form sửa và giờ hàng danh sách; chưa kiểm chứng conversion binding runtime (`TRIP:93-105`; `BusTicketSaleSystem/Views/AdminTripManagementView.xaml:79-89`; `AM:147-152`).
- VM parse fare và scope như Add; service kiểm tra arrival>departure, price>=0, convert UTC.
- SQL theo thứ tự: chuyến tồn tại; route tồn tại; bus tồn tại; tuyến đích và tuyến hiện tại đều scope; nếu đổi BusId và có **bất kỳ Tickets** (kể cả hủy) từ chối; nếu overlap xe khác TripId từ chối; rồi UPDATE RouteId/BusId/times/Price. Không sửa Status, StagePrice hoặc ticket price đã ghi; không cấm đổi route/time/price khi vé đã phát hành nếu vẫn cùng bus; không cấm sửa chuyến Cancelled/Departed (`ADB:1098-1164`).
- Update overlap có UPDLOCK/HOLDLOCK nhưng không explicit transaction quanh chuỗi IF+UPDATE như Add. Không suy test concurrency Add đã chứng minh Update. Thành công thoát editing và refresh; Cancel chỉ clear editing ID/message, không reset fields (`TRIP:87-92,183-214`).

### 8.4. Xóa chuyến

Hai bước RequestDelete/ConfirmDelete; prompt tuyến/ngày không hoàn tác. SQL DELETE chỉ nếu không có bất kỳ Tickets và thuộc scope; không có guard trạng thái/thời gian. 0row gom chung lý do có vé/out-of-scope, kể cả ID không còn. SQL lỗi trả chung. VM đóng confirmation dù fail, success refresh (`TRIP:106-113,216-231`; `ADB:1167-1191`).

### 8.5. Hủy chuyến và dây chuyền trạng thái

Đây là **khác xóa**: giữ lịch sử, cập nhật chuyến/vé/payment. Nút hủy gọi ngay service với reason cố định “Chuyến xe bị hủy bởi ban điều hành bến.”; không dialog xác nhận, không ô nhập reason trong XAML (`TRIP:234-246`; `BusTicketSaleSystem/Views/AdminTripManagementView.xaml:208-231`).

SQL CancelTripAsync:

1. RequireAdmin; XACT_ABORT; BEGIN TRANSACTION; khóa Trips theo ID UPDLOCK/HOLDLOCK.
2. Không thấy =>rollback/code1; đã Cancelled=>rollback/code2; Departed hoặc Departure<=SYSUTCDATETIME=>rollback/code3. Không có check station; không explicit yêu cầu Scheduled ngoài các trạng thái bị loại.
3. UPDATE Trips.Status=Cancelled.
4. Tickets trên chuyến Status Đã đặt hoặc Đã thanh toán => Đã hủy, CancelledAt UTC, PaymentExpiresAt NULL. Không đổi PaymentStatus, không hủy vé Đã sử dụng.
5. Mọi Payments Success của vé chuyến => set RefundRequestedAt nếu chưa có, Notes nếu null thành ghi chú hủy; status vẫn Success, **chưa thực hiện hoàn tiền**.
6. Mọi Payments Pending => Failed, Notes nếu null thành ghi chú hủy. Không set VerifiedAt/ConfirmedBy tại nhánh này.
7. COMMIT; sau đó ghi AuditLog TripCancelled với reason và file log. Không xóa TicketSeats hoặc trip/ticket records.

Nguồn `ADB:1198-1278`. [INFERENCE] Payment Success của vé đã sử dụng vẫn có thể được đánh dấu requested vì câu UPDATE payment không filter Ticket.Status; ApproveRefund sau đó sẽ từ chối used. Audit sau commit có thể fail riêng, giống account. UI message success nói vé/thanh toán đã được cập nhật, không chứng minh người dùng được email/SMS; không có call thông báo bên ngoài trong CancelTripAsync.

Trạng thái Departed được migration backfill cho chuyến Scheduled quá khứ; không thấy command điều hành “xuất bến” ở module hiện tại. Không nhầm tự chuyển ticket Used với tự chuyển trip Departed (`BusTicketSaleSystem/Data/Migration_015_StationOperationsAndCommission.sql:19-37`; `TLS:15-25`).

## 9. Payments & Settlement / đối soát

### 9.1. Giao diện, fields và filter

- Năm KPI: pending count/amount, success count/amount, refunded count/amount, failed count/amount, net. Đây là global stats, **không áp dụng filter/search của list**.
- Status dropdown All/Pending/Success/Failed/Refunded; đổi status tự RefreshAsync. Search TextBox cập nhật query nhưng chỉ search khi Enter/nút Search hoặc Refresh; không tự query từng ký tự. Query tìm TicketCode, TransactionNo, ProviderTransactionNo, PassengerName, PassengerPhone bằng LIKE có escape wildcard; không tìm route/note/company. TOP mặc định200, method clamp1..1000; không pagination hoặc nút lấy trang tiếp. Pending trước, sau đó CreatedAt DESC (`PAY:23-49,99-103,142-169`; `ADB:170-205,234-264,1419-1428`).
- Bảng: mã vé, created time, reference (provider ưu tiên, fallback local transaction, không có=>dấu gạch); passenger, confirmed by; route/departure; channel PaymentMethod hoặc PaymentMethod·Provider nếu khác; PaidAt; amount; payment status + ticket status; Manage.
- Manage mở action panel, headline paymentID/ticket/amount/status và dòng refund request nếu có; fields ProviderReference và ActionNote; Confirm settlement, Approve refund, Check in for boarding, Reject payment; Close. Chọn row reset reject state/reason/note/message, copy reference cũ. Close xóa selected và các fields (`PAY:104-119`; `AM:225-275`; `BusTicketSaleSystem/Views/AdminPaymentsView.xaml:86-170,197-285,334-370`).
- Note cũ và PaymentExpiresAt có trong record/derived `NotesDisplay`, `ExpiryDisplay` nhưng XAML bảng/panel không hiển thị chúng; RefundRequestedAt hiện có điều kiện, RefundedAt không có cột. Không có upload chứng từ, ảnh biên lai, đối soát tự động bank feed, chỉnh Amount hay add payment từ màn admin này.

### 9.2. Enabled command trên UI không phải validation cuối

| Action | Predicate model/UI | Enforcement thêm ở service |
|---|---|---|
| Confirm | Payment.Status Pending | Admin/ID phiên; payment ID; payment tồn tại và Pending; chỉ BankTransfer/Cash; ticket Status Đã đặt |
| Reject | Pending; confirm reject panel mở | Admin/ID phiên; payment tồn tại và Pending; reason bắt buộc chỉ ở VM |
| Approve refund | Success và RefundRequestedAt có giá trị | Admin/ID; Success; request có; ticket không Used |
| Check-in từ payments | Success và TicketStatus Đã thanh toán | Admin/ID; ticket còn tồn tại; status Paid; các guard ticket Used/Cancelled/Unpaid |

Nguồn `AM:265-274`; `PAY:120-139,185-287`; `PS:469-589`; `TLS:29-85`. Ví dụ Pending VNPay vẫn có nút Confirm enabled theo model nhưng service sẽ từ chối xác nhận thủ công. Predicate refund không loại ticket used trước nên service có thể từ chối sau click.

### 9.3. Xác nhận thu tiền thủ công

1. PAY kiểm tra selected.CanConfirm và session ID; trim optional ProviderReference/ActionNote thành null nếu trắng.
2. Service ValidateAdminSession; paymentId>0; transaction Serializable trong helper, khóa payment/ticket UPDLOCK/HOLDLOCK.
3. Không thấy payment lỗi; non-Pending báo đã settled; method không BankTransfer/Cash lỗi; ticket không Đã đặt (không còn, hủy, paid, used...) lỗi eligible.
4. Thành công Payments => Success, PaidAt/VerifiedAt UTC, ProviderTransactionNo/reference, ConfirmedByAccountId, Notes; Tickets=>Status Paid, PaymentStatus Paid, PaymentExpiresAt NULL.
5. Audit PaymentConfirmed **trong cùng transaction**, commit; file log. VM clear reference/note rồi refresh, giữ selected bằng PaymentId nếu vẫn ở filtered list.

Nguồn `PAY:185-209,147-159`; `PS:469-510,595-635`.

**Caveat:** SQL xác nhận không so trực tiếp PaymentExpiresAt hay departure với now; thông báo có chữ “expired” chỉ vì ticket đã chuyển trạng thái khỏi Đã đặt. Nếu worker expiry chưa đổi trạng thái thì riêng method này không chặn bằng deadline. Không gọi gateway/bank để chứng minh đã nhận tiền; đây là operator assertion, không callback verification (`PS:474-495`).

### 9.4. Từ chối payment

- UI hai bước: RequestReject mở panel cảnh báo hủy vé/giải phóng ghế; nhập RejectReason bắt buộc nonwhitespace ở VM; Keep payment đóng panel/xóa reason; confirm gửi reason trim (`PAY:123-133,212-240`; `BusTicketSaleSystem/Views/AdminPaymentsView.xaml:260-285`).
- Service không kiểm tra reason bắt buộc, chỉ TextOrNull. Payment bất kỳ method nhưng phải Pending; UPDATE Failed, VerifiedAt UTC, ConfirmedByAccountId, Notes; UPDATE Tickets => Cancelled/CancelledAt UTC/PaymentExpiresAt NULL **chỉ nếu ticket hiện Đã đặt**. Không đổi PaymentStatus hay xóa seat rows ở method này; thao tác payment vẫn thành công nếu ticket không còn Đã đặt nhưng payment Pending (`PS:513-544`).
- Audit PaymentRejected trong settlement transaction. Fail payment missing/not Pending, lỗi SQL/invalid operation rollback trả message. Success đóng confirm reject, clear reason, refresh (`PS:595-635`; `PAY:228-240`).

### 9.5. Duyệt hoàn tiền

- UI chỉ optional ActionNote; không nhập số tiền, tỷ lệ hoặc ngày hoàn; không có riêng nút reject refund/withdraw request. Success message dùng toàn `payment.Amount` (`PAY:243-267`).
- SQL yêu cầu payment Success, RefundRequestedAt không null; khóa ticket; nếu ticket Used =>từ chối. Không yêu cầu ticket Cancelled trong method, không tính cutoff từ departure, không gọi nhà cung cấp thanh toán.
- Thành công payment Status Refunded, RefundedAt UTC, RefundApprovedByAccountId, Notes=@Note (có thể xóa note cũ nếu null); ticket chỉ đổi PaymentStatus=Hoàn tiền, không đổi Status, không đổi Amount, không ghi một refund ledger row riêng; ghi Audit RefundApproved cùng transaction (`PS:547-589,595-618`).
- Vì không có AmountRefunded/partial amount trong flow này, báo cáo coi toàn payment.Amount là hoàn. “Paid back” là trạng thái sổ nội bộ, không có chứng cứ hoàn tiền external bank/gateway đã xảy ra từ method này. Quy tắc khách gửi request/cutoff thuộc báo cáo lifecycle; không tự gán các điều kiện đó thành enforcement ApproveRefund.

### 9.6. Failure và concurrency UI

- ExecuteSettlementAsync rollback khi outcome bị từ chối; SqlException rollback quiet + thông báo ledger unavailable; InvalidOperationException rollback + operation could not be completed; finally dispose transaction. Audit SQL failure ở helper làm rollback cả trạng thái payment/ticket (`PS:595-635`).
- Confirm/Reject/ApproveRefund/CheckIn trong PAY là `RelayCommand` với async lambda, khác Refresh/Search là AsyncRelayCommand; PAY không có busy riêng cho mutation, predicate chỉ trạng thái snapshot. [INFERENCE] Có thể click lại khi await; SQL transaction/state guard là lớp từ chối lần sau, không nên mô tả UI đã chặn mọi double-submit (`PAY:99-139`).
- Khi refresh, nếu selected PaymentId không còn trong list do filter/trạng thái/top200, selection mất và reject panel đóng. Fields reference/note có thể vẫn giữ vì refresh chỉ đổi SelectedPayment, không chạy ClearSelection (`PAY:142-160`).

## 10. Soát vé / Check-in chuyên dụng

### 10.1. Bề mặt thực tế

- Một TextBox `TxtScannerInput` bind TicketCodeInput; Enter=SearchCommand; Tra cứu vé; Làm mới thực tế gọi ClearCommand. Loaded focus TextBox (`BusTicketSaleSystem/Views/AdminCheckInView.xaml:33-74`; `BusTicketSaleSystem/Views/AdminCheckInView.xaml.cs:8-12`).
- Nói “barcode/QR” nhưng implementation chỉ nhận **chuỗi mã vé** từ bàn phím/scanner dạng keyboard wedge; không camera capture, decode ảnh QR/barcode hoặc device SDK ở view/VM này. Không tự check-in sau scan: lookup rồi click xác nhận riêng.
- Card tìm thấy hiển thị ticket code/badge, passenger/SĐT, số ghế và tổng chỗ, route/plate, departure, TotalPrice; nút xác nhận disabled nếu !CanCheckIn. Empty state sẵn sàng quét.
- Cột lịch sử phiên hiển thị passenger, route, seats, time checked-in. Model còn TicketCode nhưng row XAML không hiện. Banner luôn màu nền đỏ nhạt hard-code, không bind MessageKind; MessageKind có Info/Warning/Danger/Success ở VM nhưng không đổi style banner (`BusTicketSaleSystem/Views/AdminCheckInView.xaml:76-208`; `CHECK:11-16,29-30,59-96`).

### 10.2. Lookup và lỗi

1. Trim code; rỗng báo nhập/quét và return. Không clear FoundTicket khi code rỗng.
2. IsSearching true, clear message, gọi DBH.GetTicketByCodeAsync. SQL theo equality TicketCode, parameter nvarchar30, không owner/admin/station guard; SQL lỗi log rồi trả null, nên VM có thể hiển thị “Không tìm thấy” cho cả trường hợp DB lookup gặp SqlException (`DBH:1112-1130`; `CHECK:125-146`).
3. Nếu không có: FoundTicket=null, danger. Nếu Used: warning có UsedAt.ToLocalTime; nếu Cancelled: danger; nếu PaymentStatus khác Paid: warning cần thanh toán; còn lại success “sẵn sàng” (`CHECK:148-168`).
4. CanCheckIn chặt hơn message: cần FoundTicket.Status==Paid **và** PaymentStatus==Paid và !IsCheckingIn. Vì vậy một trạng thái không Used/Cancelled có PaymentStatus Paid có thể nhận success lookup nhưng nút vẫn không enabled nếu Status khác Paid (`CHECK:72-85,149-168`).
5. Ngoại lệ khác log rồi danger; không xóa FoundTicket trong catch. IsSearching false finally. Chưa có cancellation request hoặc stale-response protection trong lookup (`CHECK:170-179`).

### 10.3. Xác nhận và transition

- VM lấy ticket snapshot; nếu null/!CanCheckIn return; lấy adminID/fallback1; IsCheckingIn=true; gọi TLS.CheckInTicketAsync (`CHECK:182-191`).
- Service kiểm tra admin+matching ID, ticketID>0; transaction Serializable; lock ticket UPDLOCK/HOLDLOCK; missing/Used/Cancelled/non-Paid từ chối với thông báo riêng, rollback; Paid=>Used, UsedAt SYSUTCDATETIME, commit, file log operator ID (`TLS:29-67`).
- **Service không đối chiếu PaymentStatus hoặc ledger Success**, không kiểm tra thời gian check-in trước chuyến bao lâu, không station scope, không so chuyến đang Cancelled/Departed, không đòi ghế/route hợp lệ thêm. UI chuyên dụng chặt hơn ở PaymentStatus; payment console chặt hơn ở ledger nhưng service chỉ ticket status.
- Success VM thêm record đầu RecentCheckIns với DateTime.Now HH:mm:ss; success message, re-fetch ticket để badge mới, clear TicketCodeInput. Re-fetch sau mutation có thể lỗi/null; không rollback check-in đã commit. Fail hiển thị error, giữ found ticket; exception log/danger; finally IsCheckingIn=false (`CHECK:192-224`).
- Không audit DB CheckIn trong service, chỉ file log. Không lưu riêng CheckedInByAccountId vào Tickets bởi code này. UsedAt cũng do auto worker chuyển vé đã khởi hành sang Used, nên UsedAt không nhất thiết chứng minh quét vé thủ công (`TLS:15-25,65-67`).
- RecentCheckIns không load từ DB, không giới hạn số dòng trong phiên. Clear/Refresh chỉ clear input/found/message/kind, **không clear RecentCheckIns**; rời trang trở lại tạo transient mới nên lịch sử phiên cũ không được khôi phục (`CHECK:32,111-123,195-200`; `BusTicketSaleSystem/Services/AppComposition.cs:48`; `BusTicketSaleSystem/Services/NavigationService.cs:52-71`).

## 11. Báo cáo doanh thu, hoa hồng và tiền trả nhà xe

### 11.1. Controls, range và failure

- FromDate/ToDate DatePicker; Run report, Last7days, Last30days; range caption “UTC, inclusive”; message banner có clear; loading; bốn tổng card Net/Gross/Refunded/TicketsSettled; bảng tuyến có thêm commission/payout; footer totals chỉ tickets/gross/refund/net, **không có tổng commission/payout trên XAML** (`BusTicketSaleSystem/Views/AdminReportsView.xaml:19-65,67-122,124-214`).
- Default từ UtcNow.Date-29 đến UtcNow.Date (30 ngày kể cả hôm nay). Preset n ngày đặt To=todayUTC, From=To-(n-1), chạy report. DatePicker đổi ngày không tự query. To<From báo lỗi, không chạy SQL, không clear rows cũ (`REPORT:19-28,43-76`).
- Gọi service với `[FromDate.Date, ToDate.Date+1day)`, SQL dùng >=From và <To; không maximum range. Success replace rows/totals/caption, clear message; fail AdminFailure, giữ dữ liệu đã có; hạ loading và notify empty flags (`REPORT:65-104`).

### 11.2. Ngữ nghĩa SQL theo tuyến

Nguồn `ADB:270-300`:

- Join Payments→Tickets→Trips→Routes→Buses và LEFT JOIN BusCompanies **bằng CompanyName**, không BusCompanyId.
- Chỉ Payments Status hiện tại Success hoặc Refunded; lọc **Payments.CreatedAt**, không PaidAt, RefundedAt, BookingTime hay DepartureTime.
- GROUP BY **Origin,Destination**, không RouteId, không company, không ngày. Một dòng là một cặp tên hai đầu trong toàn cửa sổ, không một dòng tuyến/ngày dù comment XAML nói “per route per day”.
- TicketsSold = SUM(case Status Success then1 else0): đếm payment rows Success, không số ghế, không số vé distinct và không cộng refunded.
- GrossRevenue = SUM Amount của rows hiện Success; RefundedAmount = SUM Amount của rows hiện Refunded. ORDER BY gross DESC rồi route summary ASC.
- CommissionRate = COALESCE(AVG(BusCompanies.CommissionRate),0.1000), tính trên joined rows được chọn. Đây không phải weighted average theo Amount; rows company null bị AVG bỏ qua nếu có company khác, fallback10% chỉ khi AVG null. Không chia nhóm từng nhà xe.

### 11.3. Công thức model và totals

Với mỗi route:

- `NetRevenue = GrossRevenue - RefundedAmount`.
- `CommissionAmount = NetRevenue > 0 ? NetRevenue * CommissionRate : 0`.
- `CompanyPayout = NetRevenue > 0 ? NetRevenue - CommissionAmount : 0`.
- Decimal tính không round ở property; chỉ display N0 VND; rate display `%` một chữ số thập phân tối đa. Không có guard clamp commission rate 0..1 trong model (`AM:299-314`).

Totals trong REPORT: cộng tickets/gross/refund; totalNet=gross-refund; totalCommission=sum mỗi row.CommissionAmount; avgRate=totalCommission/totalNet nếu net>0, không thì0.10. Tạo RevenueReportItem “All routes” dùng rate này. [INFERENCE] Totals.CompanyPayout tính từ aggregate net và commission không nhất thiết bằng tổng row.CompanyPayout khi có tuyến âm; XAML không hiển thị hai tổng này nên không mô tả như đã có payout tổng (`REPORT:79-90`; `AM:306-308`).

### 11.4. Caveat kế toán cần bảo toàn khi mô tả hiện trạng

1. Payment đổi Success→Refunded không còn nằm GrossSuccess và đồng thời bị trừ ở Refunded. Ví dụ một payment100 đã success rồi refunded, không có payment khác: query gross0/refund100/net-100, **không phải0**. Đây là hệ quả đúng theo code current-status query; không sửa thành công thức gross lịch sử đã thu. Cùng công thức được dùng Dashboard và PaymentStats (`ADB:77-82,237-263,275-287`; `AM:290,306`).
2. Refund xảy ra hôm nay của payment CreatedAt tháng trước sẽ nằm cửa sổ tháng trước khi rerun, không ngày hoàn; báo cáo không phải cash-flow theo thời điểm actual settlement (`ADB:285-287`).
3. Commission join dùng công ty hiện tại của bus và tỷ lệ hiện tại, không snapshot lúc thanh toán; sửa company của bus có thể đổi kết quả lịch sử khi report lại. Không có màn admin chỉnh CommissionRate; migration default10% (`ADB:283-284`; `BusTicketSaleSystem/Data/Migration_015_StationOperationsAndCommission.sql:40-45`; `BUS:68-75`; form XAML).
4. “Trả nhà xe” chỉ giá trị tính toán; không tạo payable, không chuyển tiền, không đánh dấu đã chi trả, không có bank payout API ở report (`REPORT:65-104`; `ADB:270-300`).
5. Không có export CSV/Excel/PDF, print report, chart, lưu report snapshot, lọc nhà xe/station/route trên màn này. Report commands chỉ run/presets/clear; toàn bộ XAML không bind export (`REPORT:45-56`; `BusTicketSaleSystem/Views/AdminReportsView.xaml:1-214`).

## 12. Inventory DTO/model và ý nghĩa field dẫn xuất

| Model | Dữ liệu và computed contract |
|---|---|
| RouteAdminItem | RouteId, Origin, Destination, decimal? DistanceKm; DisplayName/DistanceDisplay |
| BusAdminItem | BusId/plate/capacity/type/UsedSeats; company/driver/phone/schedule/route/stops; make/model/year/color; registration/inspection numbers và ba ngày nullable; entity/operation; FullPrice,StagePrice nullable; Availability, OccupancyPercentage/Text, SpecsDisplay,LegalStatusDisplay,FareDisplay |
| BusRegistrationInput | Toàn bộ field hồ sơ xe; RouteId nullable; FarePrice/Hotline/OperatingArea/Description/Amenities/StagePrice optional; không CommissionRate |
| BusDraftItem | ViewModelBase mutable giữ snapshot string input mỗi xe, routeId nullable, ngày nullable; derived RouteDisplay,DriverDisplay,VehicleDisplay,PriceSummary,ScheduleSummary; không company profile riêng mỗi draft |
| TripAdminItem | IDs route/bus/trip, route string, plate, departure/arrival, price, status default Scheduled; local schedule/time/date/price display; IsCancelled/IsDeparted/CanCancel/StatusDisplay |
| RecentBookingAdminItem | TicketId/code, route, passenger/phone, seat string, Price (query đã nhân SeatCount), status, booking time; display amount/time/seat/passenger |
| AdminDashboardStats | Route/Bus/Trip/Ticket/Customer counts; TotalRevenue; pending count/amount; refunded amount; Empty zeros; các display VND/caption |
| AccountAdminItem | ID/username/name/contact/role/created time; IsAdmin exact casing, role badge, toggle caption, created display |
| PaymentAdminItem | PaymentId/TicketId/code; hai transaction refs; method/provider/amount/payment status/ticket status; passenger/route/departure; created/paid/refundrequested/refunded; confirmedBy name/notes/expiry; derived reference/channel/displays/action flags |
| AdminPaymentStats | Count/Amount cho Pending/Success/Failed/Refunded; Empty zeros; Net=SuccessAmount-RefundedAmount |
| RevenueReportItem | RouteSummary,TicketsSold,GrossRevenue,RefundedAmount,CommissionRate default0.10; Net/Commission/Payout và formatted displays |
| CheckedInTicketItem | TicketCode,PassengerName,Route,SeatNumbers,TimeCheckedIn (string local time) trong CHECK, không AM |

Nguồn: `AM:5-73,75-179,186-220,225-314`; `CHECK:11-16`. Các default mẫu trong BusAdminItem và COALESCE GetBuses có thể khiến dữ liệu thiếu hiển thị như thông tin thật; đặc biệt CompanyName/DriverName/Phone/Make/Model/Year/Color. FullPrice lấy chuyến cuối theo TripId, StagePrice ưu tiên bus rồi chuyến cuối (`ADB:425-445`; `AM:11-35`).

## 13. Tích hợp, mutation và audit tổng hợp

| Bề mặt | Tác động chính | Tích hợp / audit |
|---|---|---|
| Dashboard/Reports | SELECT, không ghi dữ liệu nghiệp vụ | SQL qua DatabaseHelper connection; chỉ error log |
| Accounts role/reset/delete | UPDATE Role/PasswordHash hoặc DELETE Accounts | Audit AccountRoleChanged/AccountPasswordReset/AccountDeleted sau mutation; session in-memory |
| Routes | INSERT/UPDATE/DELETE Routes | Không audit action riêng trong methods |
| Bus register/update | Buses + Seats + BusCompanies + tự tạo Trips cùng transaction từng xe | Không audit action riêng; auto-fill DatabaseHelper; preset nội bộ, không map API |
| Delete bus | DELETE Seats rồi Buses | XACT_ABORT transaction phần xóa; không xóa company/trips |
| Manual trip add/update/delete | Trips | Scope một số thao tác; add transaction guard overlap |
| Cancel trip | Trips.Cancelled, active Tickets.Cancelled, Success payment request refund, Pending payment Failed | TripCancelled audit sau commit; reason UI cố định |
| Confirm/reject/refund | Payments/Tickets đôi trạng thái | Serializable; audit payment trong transaction; không bank/gateway call tại thao tác manual |
| Check-in | Tickets.Status/UsedAt | Serializable; file log operator, không audit DB action riêng |
| Language/location | Singleton dịch/vị trí; header/presets scope | Location IP API khi singleton khởi tạo, pin config; không admin station picker |

Chứng cứ implementation nằm ở các section trên; cơ chế AuditLog lưu ActorAccountId từ CurrentUser, Action nvarchar60, TargetType40, TargetId60, Detail truncate400 (`BusTicketSaleSystem/Data/AuditLog.cs:21-23,50-68`). Không ghi secret password vào audit/reset log (`ADB:1355-1356`).

## 14. Tests liên quan đã đọc — coverage tồn tại, chưa chạy

| File:line | Điều được test source thực sự assert |
|---|---|
| `BusTicketSaleSystem.Tests/AdminDataIntegrityTests.cs:23-43` | 4 CreatePayment đồng thời chỉ một success và một live payment Pending/Success |
| `BusTicketSaleSystem.Tests/AdminDataIntegrityTests.cs:51-92` | Cố ý FK phụ làm DELETE Buses fail; DeleteBus false và vẫn đủ8seats +1bus |
| `BusTicketSaleSystem.Tests/AdminDataIntegrityTests.cs:99-131` | 5 AddTrip cùng slot đúng1success/1row, pin station về route |
| `BusTicketSaleSystem.Tests/AdminDataIntegrityTests.cs:138-185` | Station Cà Mau từ chối AddTrip và AddRoute tuyến ngoài bến; AddTrip không insert |
| `BusTicketSaleSystem.Tests/TripCancellationTests.cs:21-77` | Tạo vé paid rồi hủy chuyến: trip Cancelled, ticket Cancelled, refund request tồn tại,1audit, chuyến bị loại khỏi customer search. **Không test pending-payment branch** dù mô tả đầu file nhắc pending |
| `BusTicketSaleSystem.Tests/CheckInTests.cs:18-81` | Book→bank confirm→lookup→VM check-in→ticket Used, recent list, lần check-in thứ hai bị từ chối |
| `BusTicketSaleSystem.Tests/CheckInTests.cs:84-114` | Vé chưa thanh toán lookup được nhưng CanCheckIn false/badge unpaid |
| `BusTicketSaleSystem.Tests/AuditTrailTests.cs:18-40` | Role change thêm audit và Actor đúng admin |
| `BusTicketSaleSystem.Tests/AuditTrailTests.cs:47-82` | Refund request với ticket Used bị từ chối, payment vẫn Success |
| `BusTicketSaleSystem.Tests/AuditTrailTests.cs:85-118` | Confirm Cash có1audit; confirm lại bị từ chối không thêm audit |
| `BusTicketSaleSystem.Tests/CommissionReportTests.cs:12-44` | Model gross2m-refund0.2m=>net1.8m,commission180k,payout1.62m; net0=>commission/payout0. Không chạy query reporting thực tế |
| `BusTicketSaleSystem.Tests/DomainModelTests.cs:196-205` | Occupancy model 0/50/100%, display20/40; không xác minh query đếm TicketId thay vì seats |
| `BusTicketSaleSystem.Tests/NavigationAndViewModelTests.cs:118-166` | Form xe hidden/toggle, bus/operation options, match route theo hai đầu |
| `BusTicketSaleSystem.Tests/NavigationAndViewModelTests.cs:307-355,400-421` | Khởi tạo một số WPF admin view không resource error; preset giờ. Không phải test tương tác/render đầy đủ; theory này không gồm AdminPaymentsView |
| `BusTicketSaleSystem.Tests/NavigationAndViewModelTests.cs:358-398` | Filter company/search plate và gợi ý fare>=100000 |
| `BusTicketSaleSystem.Tests/NavigationAndViewModelTests.cs:425-513` | Nhiều draft giữ tuyến/tài xế/giá khác nhau, stop presets và append |

Test SQL dùng SkippableFact và `_fixture.SkipIfUnavailable()`, nên chỉ nhìn tên test không chứng minh môi trường đã chạy DB thành công. Các đường đáng chú ý chưa có chứng cứ coverage từ các test admin đã đọc: update-trip concurrency, batch bus partial failure, lệch UTC auto schedule, scope cancel/delete route/bus/payments/check-in, lịch pháp lý, báo cáo CreatedAt + status mutation, phí hoàn partial. Đây là **khoảng trống xác minh**, không phải khẳng định lỗi runtime đã tái hiện.

## 15. Những điều không được diễn giải thành tính năng đã hoàn tất

1. “Phạm vi bến cố định” không đồng nghĩa row-level isolation toàn bộ admin; xem ma trận mục3.
2. “Tự sinh lịch 14 ngày” không đồng nghĩa kiểm tra conflict giống form chuyến; đường internal khác guard/time basis.
3. “Thành công toàn bộ đội xe” không đồng nghĩa transaction cả batch; lỗi tuple từng xe bị bỏ qua.
4. “Soát QR” là nhập mã chuỗi vào TextBox; chưa thấy decode camera/ảnh.
5. “Thanh toán/hoàn tiền thành công” trên màn manual là cập nhật ledger/operator action, không bằng chứng external money movement.
6. “Doanh thu thuần” là công thức current Success minus current Refunded, có thể âm và phản ánh ngày tạo payment, không phải kế toán phát sinh chuẩn khác.
7. “Hoa hồng nhà xe” tính trung bình rate theo joined payment rows ở mức route; không settlement report theo từng company và không payout thực.
8. “Đã xuất bến” có enum/SQL migration backfill; không nút xuất bến hoặc quy trình điều độ đầy đủ trong admin đã đọc.
9. Các ngày đăng kiểm/bảo hiểm được lưu nhưng không có enforcement hạn sử dụng/cảnh báo vận hành trong form/service này.
10. Không có export/print trên các màn admin đã đọc; dịch vụ in vé ở phần khác không tự biến thành export báo cáo admin.
11. Không có một nhóm command bị comment-out/disabled toàn module trong các XAML đã đọc; **disabled/ẩn có điều kiện** gồm Add/Save theo editing, Delete-confirm panel khi chưa có target, Hủy chuyến khi !CanCancel, settlement theo model flags, check-in khi !CanCheckIn. Command Add/AddAndContinue của xe tồn tại trong VM nhưng không có nút trực tiếp trên XAML hiện tại.
12. Tất cả kết luận vẫn là source-derived và **chưa xác minh runtime**; không mở app hay DB để tránh mutation ngoài phạm vi điều tra.
