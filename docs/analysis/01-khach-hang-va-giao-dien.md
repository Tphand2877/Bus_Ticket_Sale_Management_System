# 01 — Hiện trạng chức năng khách hàng và giao diện

## 1. Phạm vi, cách đọc và mức độ xác minh

- Đây là **trích xuất hiện trạng source**, không phải đặc tả tính năng web tương lai và không thay đổi nghiệp vụ. Đã đọc XAML, code-behind, ViewModel khách, navigation/session, domain model, các đoạn SQL được luồng khách gọi và các test liên quan. **Không chạy ứng dụng, kết nối DB, build, test, formatter hay linter** theo yêu cầu. Mọi hành vi dưới đây là implemented trong code, **chưa xác minh runtime**.
- Chứng cứ dùng đường dẫn tương đối từ root repository. `BusTicketSaleSystem/...` là project ứng dụng; `BusTicketSaleSystem.Tests/...` là source kiểm thử. Không ghi giá trị secret.
- `PaymentWindow` và implementation tích hợp thanh toán/in/email thuộc báo cáo chuyên đề khác. Tài liệu này ghi đầy đủ điểm gọi, điều kiện UI và tác động mà màn khách yêu cầu; không coi việc gọi service là bằng chứng gateway/máy in/mail đã hoạt động thực tế.
- Phân loại: **implemented/exposed** = có implementation và control gọi; **implemented/not exposed** = có API/property/command nhưng không nối tới control trong XAML hiện tại; **disabled/không khả dụng** = bị ẩn, vô hiệu hóa hoặc không nằm trong lựa chọn; **chưa xác minh runtime** áp dụng toàn bộ.
- Source implementation được ưu tiên hơn tên màn hình, comment và văn bản quảng bá. Ví dụ “Tìm chuyến” hiện chưa chuyển bộ lọc sang màn sau; “Book Again” không sao chép chuyến cũ; phần hình ảnh nhà xe và barcode có thành phần trang trí tĩnh. Các khác biệt này được ghi riêng bên dưới.

## 2. Inventory màn hình, actor và điều hướng

| Màn/host | Actor và đầu vào | Đường đi thực tế | Chứng cứ |
|---|---|---|---|
| `LoginWindow` / `LoginViewModel` | Chưa đăng nhập; username hoặc email, password | Admin → AdminWindow; mọi role khác → MainWindow | `BusTicketSaleSystem/Views/LoginWindow.xaml.cs:39-56`, `BusTicketSaleSystem/ViewModels/LoginViewModel.cs:60-95` |
| `SignUpWindow` / `SignUpViewModel` | Người mở dialog từ Login; thông tin tài khoản | Dialog modal thuộc Login; thành công đóng dialog, đưa notice về Login; không tự đăng nhập | `BusTicketSaleSystem/Views/SignUpWindow.xaml.cs:20-47`, `BusTicketSaleSystem/Views/LoginWindow.xaml.cs:52-56` |
| `MainWindow` / `MainViewModel` | Session không phải Admin theo luồng login chuẩn | Shell gồm Overview, Booking, MyTickets, History, Account; mở Home đầu tiên | `BusTicketSaleSystem/ViewModels/MainViewModel.cs:105-149` |
| `HomeView` / `HomeViewModel` | Session dùng cho thống kê; vị trí global cho origin | Search/QuickBook → Booking; ViewTicket → MyTickets; ViewCompany → chi tiết nhà xe | `BusTicketSaleSystem/ViewModels/HomeViewModel.cs:80-104` |
| `BookingView` / `BookingViewModel` | Danh sách nhà xe, không nhận bộ lọc từ Home | Chọn nhà xe → BusCompanyDetail; Back → Home | `BusTicketSaleSystem/ViewModels/BookingViewModel.cs:36-66` |
| `BusCompanyDetailView` / `BusCompanyDetailViewModel` | `BusCompanyItem`, ngày tùy chọn; constructor có origin/destination tùy chọn | BookTrip → SeatSelection với nguyên Trip; Back → Booking | `BusTicketSaleSystem/ViewModels/BusCompanyDetailViewModel.cs:69-115` |
| `SeatSelectionView` / `SeatSelectionViewModel` | Trip; tên/điện thoại mặc định từ session | Continue → Confirmation; Back → Booking (không quay về chi tiết nhà xe cũ) | `BusTicketSaleSystem/ViewModels/SeatSelectionViewModel.cs:19-20,59-98` |
| `BookingConfirmationView` / `BookingConfirmationViewModel` | Trip, danh sách ghế, tên, điện thoại | Book → giữ vé + tạo payment → dialog tùy phương thức → MyTickets; Back → SeatSelection mới | `BusTicketSaleSystem/ViewModels/BookingConfirmationViewModel.cs:68-97,99-167` |
| `MyTicketsView` / `MyTicketsViewModel` | AccountId hiện tại | Xem vé paid → TicketView; stage hủy/hoàn → confirm; BookNew → Booking | `BusTicketSaleSystem/ViewModels/MyTicketsViewModel.cs:91-154` |
| `TicketView` / `TicketViewModel` | `TicketDetails` snapshot từ MyTickets | In/export/email/map; về Home hoặc MyTickets | `BusTicketSaleSystem/ViewModels/TicketViewModel.cs:69-88` |
| `HistoryView` / `HistoryViewModel` | AccountId hiện tại | Refresh; BookAgain → Booking không mang dữ liệu dòng | `BusTicketSaleSystem/ViewModels/HistoryViewModel.cs:37-64` |
| `ProfileView` / `ProfileViewModel` | Session hiện tại | Lưu thông tin; đổi password; đổi ngôn ngữ | `BusTicketSaleSystem/ViewModels/ProfileViewModel.cs:50-81,84-159` |
| `NotificationHost` / `NotificationHostViewModel` | Host nổi thuộc shell | Nhận toast global, dismiss bằng nút; tồn tại qua navigation | `BusTicketSaleSystem/Views/MainWindow.xaml:395-407`, `BusTicketSaleSystem/Views/NotificationHost.xaml:10-59` |

### 2.1 Phân quyền thực tế, không đồng nhất với UI

1. Identity là static mutable `CurrentUser.Account`; `UserSession` gồm AccountId, Username, FullName, Email?, Phone?, Role. `IsAdmin` so role `Admin` không phân biệt hoa thường. Không có token, cookie, expiry timestamp hay role permission collection trong model này. Chứng cứ: `BusTicketSaleSystem/Models/UserSession.cs:3-15`.
2. Login gán trực tiếp `CurrentUser.Account`; Profile thay bằng bản session mới sau lưu thành công. `SessionService.SignIn` cũng gán identity nhưng Login không gọi nó. Vì vậy comment “service là writer duy nhất” không phản ánh implementation. Chứng cứ: `BusTicketSaleSystem/ViewModels/LoginViewModel.cs:82-85`, `BusTicketSaleSystem/ViewModels/ProfileViewModel.cs:93-108`, `BusTicketSaleSystem/Services/SessionService.cs:27-44`.
3. NavigationService không kiểm tra đăng nhập/role: nó resolve/activate ViewModel. MainWindow constructor cũng không có guard role; việc chọn shell nằm ở Login code-behind. Không được coi việc ẩn/chọn shell là authorization cho API web. Chứng cứ: `BusTicketSaleSystem/Services/NavigationService.cs:52-97`, `BusTicketSaleSystem/Views/MainWindow.xaml.cs:13-27`, `BusTicketSaleSystem/Views/LoginWindow.xaml.cs:43-45`.
4. Profile save/password, Confirmation book, MyTickets load/cancel/refund, History load kiểm tra session null và báo cần đăng nhập; Home stats chỉ return nếu null. Các thao tác duyệt nhà xe/chuyến/ghế không yêu cầu session tại ViewModel tương ứng. Chứng cứ: `BusTicketSaleSystem/ViewModels/ProfileViewModel.cs:86,124`, `BusTicketSaleSystem/ViewModels/BookingConfirmationViewModel.cs:101`, `BusTicketSaleSystem/ViewModels/MyTicketsViewModel.cs:160-164,198-202,234-238`, `BusTicketSaleSystem/ViewModels/HistoryViewModel.cs:48-52`, `BusTicketSaleSystem/ViewModels/HomeViewModel.cs:175`.
5. SQL danh sách vé lọc AccountId; SQL hủy/hoàn lọc đồng thời TicketId + AccountId và kiểm trạng thái/giờ khởi hành trong transaction. Tuy nhiên AccountId là tham số do caller cung cấp, không phải principal độc lập của SQL. `GetTicketByCodeAsync` chỉ lọc code, không ownership; không có ô tra code công khai ở UI khách đã đọc. Chứng cứ: `BusTicketSaleSystem/Data/DatabaseHelper.cs:883-946,979-1048,1052-1078,1112-1130`.
6. Khách có thể thay đổi vị trí; `LocationService.SetUserLocation` từ chối nếu `CurrentUser.IsAdmin`. Đây là enforcement service cho lựa chọn vị trí, không phải bộ lọc đang hoạt động của danh sách nhà xe. Chứng cứ: `BusTicketSaleSystem/Services/LocationService.cs:93-114`, `BusTicketSaleSystem/ViewModels/BookingViewModel.cs:60,81-85`.

### 2.2 Shell, navigation, trạng thái trang, logout

- MainWindow desktop WPF 1280×840, minimum 1080×700, sidebar cố định 260; không có layout web/responsive được implementation tại đây. Login 880×580 minimum 800×540; SignUp 520×720 có scroll, chỉ minimize. Chứng cứ: `BusTicketSaleSystem/Views/MainWindow.xaml:11-30`, `BusTicketSaleSystem/Views/LoginWindow.xaml:4-16`, `BusTicketSaleSystem/Views/SignUpWindow.xaml:5-13`.
- Sidebar commands đầy đủ: `NavigateHomeCommand`, `NavigateBookingCommand`, `NavigateMyTicketsCommand`, `NavigateHistoryCommand`, `NavigateProfileCommand`, `LogoutCommand`; header có QuickBook dùng lại NavigateBooking, selector vị trí TwoWay, ToggleLanguage. Avatar initials lấy chữ đầu từ phần đầu/cuối tên, tên 1 phần lấy tối đa 2 ký tự; fallback `GU`. Tên ưu tiên FullName rồi Username rồi Guest, role fallback Customer. Chứng cứ: `BusTicketSaleSystem/ViewModels/MainViewModel.cs:27-67,105-138`, `BusTicketSaleSystem/Views/MainWindow.xaml:78-288,324-389`.
- `ActiveTab`: Home=Overview; Booking/Seat/Confirmation=Booking; MyTickets/Ticket=MyTickets; History=History; Profile=Account. **BusCompanyDetail không có nhánh mapping**, nên giữ tab đang active trước đó (có thể Overview nếu vào từ Home). Breadcrumb hiển thị raw ActiveTab. Chứng cứ: `BusTicketSaleSystem/ViewModels/MainViewModel.cs:143-149`, `BusTicketSaleSystem/Views/MainWindow.xaml:317-321`.
- Mỗi shell tạo DI scope riêng chứa NavigationService; các page đăng ký transient. Navigation theo type cần factory hoặc singleton đăng ký; thiếu factory throw rõ ràng; cũng nhận concrete instance. Activate đặt CurrentViewModel, dispose trang cũ IDisposable nếu không cùng instance và không registered singleton, rồi gọi `IRefreshable.RefreshAsync` không await tại caller; lỗi refresh được log. Chứng cứ: `BusTicketSaleSystem/Services/AppComposition.cs:27-40,59-64`, `BusTicketSaleSystem/Services/NavigationService.cs:42-129`.
- DataTemplate map 9 page khách sang Views; các comment “placeholder” trong App.xaml không chứng minh page chưa implementation. ContentControl dùng chung ScrollViewer; navigation scroll về đầu. Chứng cứ: `BusTicketSaleSystem/App.xaml:19-57`, `BusTicketSaleSystem/Views/MainWindow.xaml:397-404`, `BusTicketSaleSystem/Views/MainWindow.xaml.cs:22-35`.
- Back không có history stack: các command tạo/resolve trang mới. Về SeatSelection từ Confirmation tạo VM mới nên không truyền lại ghế/name/phone vừa nhập; chúng được nạp lại mặc định từ account. Chứng cứ: `BusTicketSaleSystem/ViewModels/BookingConfirmationViewModel.cs:94-96`, `BusTicketSaleSystem/ViewModels/SeatSelectionViewModel.cs:19-20,26-29`.
- Logout không hỏi xác nhận: MainVM raise RequestLogout → MainWindow gọi SessionService.SignOut → clear CurrentUser, log nếu có account, raise SignedOut → App mở Login mới, đổi MainWindow và đóng shell cũ. Scope dispose khi window Closed. Không thấy SignOut hủy vé/payment/giữ chỗ, reset location, xóa language hay notification. **SignOut raise event mọi lần**, kể cả identity đã null; comment “exactly once” không có guard thực hiện. Chứng cứ: `BusTicketSaleSystem/ViewModels/MainViewModel.cs:135-138`, `BusTicketSaleSystem/Views/MainWindow.xaml.cs:18-27`, `BusTicketSaleSystem/Services/SessionService.cs:47-52`, `BusTicketSaleSystem/App.xaml.cs:25-27,60-66`.

## 3. Đăng nhập

### 3.1 Trường, nút, trạng thái

- `UsernameOrEmail`, `Password`: mặc định chuỗi rỗng; username TextBox update theo từng thay đổi; password đưa vào VM qua PasswordChanged (không bind trực tiếp). Loaded tự focus username. Chứng cứ: `BusTicketSaleSystem/ViewModels/LoginViewModel.cs:16-26`, `BusTicketSaleSystem/Views/LoginWindow.xaml:208-230`, `BusTicketSaleSystem/Views/LoginWindow.xaml.cs:24-36`.
- `SignInCommand`: nút default Enter, không thực thi trong lúc IsBusy; `GoToSignUpCommand` cũng có `!IsBusy`, xóa ErrorMessage rồi mở signup. `SetVietnameseCommand`, `SetEnglishCommand` gắn 2 nút; `ToggleLanguageCommand` tồn tại VM nhưng không gắn nút Login. Chứng cứ: `BusTicketSaleSystem/ViewModels/LoginViewModel.cs:31-58`, `BusTicketSaleSystem/Views/LoginWindow.xaml:133-163,232-261`.
- Banner đỏ ErrorMessage, banner xanh StatusMessage (notice đăng ký), progress indeterminate khi IsBusy. Không có remember-me, forgot-password, OTP hay đăng nhập khách trong XAML/commands hiện tại. Chứng cứ inventory: `BusTicketSaleSystem/Views/LoginWindow.xaml:178-261`, `BusTicketSaleSystem/ViewModels/LoginViewModel.cs:31-40`.

### 3.2 Luồng thành công/lỗi và enforcement

1. Bắt đầu xóa cả lỗi và status; trim username/email, không trim password; thiếu một trong hai → lỗi tại VM, không gọi DB (`LoginViewModel.cs:60-70`).
2. Authenticate SELECT TOP 1 theo `Username=@Login OR Email=@Login`; lấy hash, role, contact, FailedLoginCount, LockoutEndsAt. Username/email parameter NVARCHAR(100). Account không tồn tại vẫn chạy hash verification dummy rồi trả thông báo sai credentials (`BusTicketSaleSystem/Data/DatabaseHelper.cs:59-90`).
3. Account đang LockoutEndsAt > UTC now bị từ chối kể cả password đúng. Sai password increment trong UPDATE SQL; từ lần thứ 5 đặt lock 15 phút bằng SYSUTCDATETIME. Thành công reset count/lockout nếu cần; nhận hash legacy hoặc work factor cũ thì upgrade có điều kiện old hash. Chứng cứ: `BusTicketSaleSystem/Data/DatabaseHelper.cs:26-33,93-154,1260-1275`.
4. Hash hiện hành PBKDF2 SHA256 310.000 iterations, salt 16 bytes ngẫu nhiên, hash 32 bytes; verify constant-time; chỉ chấp nhận derived hash format hiện hành hoặc legacy, không plaintext. Chứng cứ: `BusTicketSaleSystem/Data/DatabaseHelper.cs:13-21,1189-1194,1209-1257`.
5. Thành công gán session, xóa VM password, log username/role, raise LoginSucceeded; view clear PasswordBox, mở AdminWindow nếu role Admin case-insensitive, còn lại MainWindow rồi đóng Login. Chứng cứ: `BusTicketSaleSystem/ViewModels/LoginViewModel.cs:75-85`, `BusTicketSaleSystem/Views/LoginWindow.xaml.cs:39-49`.
6. SQL lỗi được helper log và trả “Could not connect to the database”; tuple failure lên ErrorMessage; exception ngoài SQL được VM log và báo sign-in unavailable; finally IsBusy=false. Chứng cứ: `BusTicketSaleSystem/Data/DatabaseHelper.cs:117-121`, `BusTicketSaleSystem/ViewModels/LoginViewModel.cs:75-95`.
7. Account lookup TOP 1 không ORDER BY; username/email là hai namespace so sánh OR. Không suy ra cơ chế resolve nếu một login khớp nhiều tài khoản ở hai cột chỉ từ UI này (`DatabaseHelper.cs:61-63`).

## 4. Đăng ký

### 4.1 Inventory trường và interaction

| Trường | Mặc định/required/giới hạn UI | Validation VM |
|---|---|---|
| FullName | Rỗng, nhãn bắt buộc; TextBox không MaxLength | Trim, không whitespace, tối đa 100 |
| Username | Rỗng, bắt buộc; TextBox không MaxLength | Trim, không whitespace, tối đa 50, không chứa bất kỳ char whitespace |
| Email | Rỗng, optional | Trim; nếu có dùng EmailAddressAttribute |
| Phone | Rỗng, optional; MaxLength=15; PhoneNumber attached restriction | Trim; nếu có regex `^\+?[0-9]{8,15}$` |
| Password | Rỗng, PasswordBox | 8–128 ký tự, có uppercase/lowercase/digit; không yêu cầu ký tự đặc biệt |
| ConfirmPassword | Rỗng, PasswordBox | Phải bằng Password nguyên chuỗi |

Chứng cứ: `BusTicketSaleSystem/ViewModels/SignUpViewModel.cs:17-36,57-76,99-152`, `BusTicketSaleSystem/Views/SignUpWindow.xaml:57-138`.

- `CreateAccountCommand` (Register Account; Enter/default), `CancelCommand` (Back to Sign In; IsCancel/Escape), cả hai predicate `!IsBusy`. Có ErrorMessage và progress; không chọn role. PasswordBox route theo Tag; thành công xóa cả 2 controls, đóng cửa sổ, trả notice username cho Login. Không auto-login. Chứng cứ: `BusTicketSaleSystem/ViewModels/SignUpViewModel.cs:38-50,84-86`, `BusTicketSaleSystem/Views/SignUpWindow.xaml:140-158`, `BusTicketSaleSystem/Views/SignUpWindow.xaml.cs:32-47`.
- Mở form là `ShowDialog` với Owner Login; cancel chỉ Close, không insert. Màn signup dùng label tiếng Anh hard-coded, không chuyển toàn bộ thành tiếng Việt chỉ nhờ language switch tại Login. Chứng cứ: `BusTicketSaleSystem/Views/LoginWindow.xaml.cs:52-56`, `BusTicketSaleSystem/Views/SignUpWindow.xaml:30-38,57-152`.

### 4.2 SQL/service và khác biệt validation

- Helper tự validate username 1–50, full name 1–100, password policy như trên, contact; INSERT Accounts chỉ Username/PasswordHash/FullName/Phone/Email. Role không lấy từ người dùng; mặc định DB cần đối chiếu báo cáo schema. Email/phone trống tại VM được đổi null. Chứng cứ: `BusTicketSaleSystem/Data/DatabaseHelper.cs:157-167,1278-1303`, `BusTicketSaleSystem/ViewModels/SignUpViewModel.cs:71-76`.
- Helper **không** cấm whitespace bên trong username như VM. Email helper dùng regex yêu cầu dấu chấm sau @, khác EmailAddressAttribute. Phone helper chấp nhận optional `+` đầu rồi 8–20 ký tự trong tập số/space/hyphen, không phải 8–20 chữ số thuần. Không được nâng quy tắc UI thành quy tắc SQL. Chứng cứ: `BusTicketSaleSystem/Data/DatabaseHelper.cs:1280-1283,1297-1303`.
- Attached PhoneNumber restriction dùng `char.IsDigit` cộng space, `.`, `-`, `(`, `)`, `+`; chặn input/paste chứa ký tự khác. Nó rộng hơn regex đăng ký (dấu chấm/ngoặc/spaces được gõ nhưng submit bị reject); MaxLength 15 của textbox tính cả dấu `+`, trong khi regex cho phép `+` + 15 digits. Chứng cứ: `BusTicketSaleSystem/Common/TextInputRestrictions.cs:19-20,51-52,79-100`, `BusTicketSaleSystem/Views/SignUpWindow.xaml:108-109`, `BusTicketSaleSystem/ViewModels/SignUpViewModel.cs:141-152`.
- Trùng unique SQL 2601/2627 → lỗi username hoặc email đã đăng ký; SQL khác log và báo không tạo được; ngoại lệ ngoài helper VM báo registration unavailable; IsBusy được reset. Chứng cứ: `BusTicketSaleSystem/Data/DatabaseHelper.cs:169-174`, `BusTicketSaleSystem/ViewModels/SignUpViewModel.cs:78-96`.

## 5. Hồ sơ, mật khẩu và ngôn ngữ

### 5.1 Hồ sơ

- Vào trang/refresh đọc FullName, Email, Phone từ **session** (không SELECT lại Accounts), xóa 3 password strings và 2 inline status; Username/AccountRole chỉ đọc. Username/role hiển thị badge, không chỉnh sửa. Chứng cứ: `BusTicketSaleSystem/ViewModels/ProfileViewModel.cs:22-36,70-81`, `BusTicketSaleSystem/Views/ProfileView.xaml:23-37`.
- Form thông tin cá nhân: FullName, Email, Phone update theo từng thay đổi; Phone MaxLength=15 cùng attached restriction; không có MaxLength cho name/email tại XAML. SaveProfileCommand được chặn khi IsBusy. VM bắt full name và email hợp lệ nếu có, trim cả 3, contact trắng → null; helper enforcing name 1–100/contact như mục đăng ký. Chứng cứ: `BusTicketSaleSystem/Views/ProfileView.xaml:178-214`, `BusTicketSaleSystem/ViewModels/ProfileViewModel.cs:53,84-97`, `BusTicketSaleSystem/Data/DatabaseHelper.cs:177-188`.
- SQL update theo account.AccountId; thành công trả record copy, không reload role/username từ DB. Không kiểm affected rows trước trả success. Trùng email → lỗi riêng; helper/VM có lỗi khác. Thành công cập nhật ambient session và toast, không navigation; inline ProfileStatusMessage không bị clear ngay đầu SaveProfileAsync, nên lỗi cũ có thể còn nếu vừa sửa thành công. Shell chỉ notify UserName khi navigation đổi trang, không subscribe CurrentUser (static không event). Chứng cứ: `BusTicketSaleSystem/Data/DatabaseHelper.cs:184-195`, `BusTicketSaleSystem/ViewModels/ProfileViewModel.cs:84-118`, `BusTicketSaleSystem/ViewModels/MainViewModel.cs:89-101`.

### 5.2 Đổi mật khẩu

- 3 PasswordBox: CurrentPassword/NewPassword/ConfirmPassword; PasswordChanged chuyển đúng field theo Tag. ChangePasswordCommand bị chặn IsBusy; VM reset password error, yêu cầu đủ 3, new ít nhất 8, confirmation bằng nhau. Helper vẫn kiểm 8–128 + upper/lower/digit; đối chiếu mật khẩu hiện tại bằng hash rồi UPDATE hash mới. Không có rule new khác old, reset session hoặc tự logout ở luồng này. Chứng cứ: `BusTicketSaleSystem/Views/ProfileView.xaml:115-155`, `BusTicketSaleSystem/Views/ProfileView.xaml.cs:47-55`, `BusTicketSaleSystem/ViewModels/ProfileViewModel.cs:121-156`, `BusTicketSaleSystem/Data/DatabaseHelper.cs:199-215,1290-1294`.
- Success toast; clear VM strings + event PasswordFieldsCleared, view clear cả ba PasswordBox. View detach subscription khi DataContext đổi/unload; VM detach LanguageChanged khi dispose. Chứng cứ: `BusTicketSaleSystem/ViewModels/ProfileViewModel.cs:69,137-143`, `BusTicketSaleSystem/Views/ProfileView.xaml.cs:11-44`.

### 5.3 Ngôn ngữ và vị trí dùng chung

- Profile expose `SelectVietnameseCommand`, `SelectEnglishCommand`, `ToggleLanguageCommand`; XAML chỉ có 2 nút chọn VI/EN. Login 2 nút; shell toggle. Service codes vi-VN/en-US, mặc định vi-VN, nạp/lưu `language.json` tại AppContext.BaseDirectory, không theo account. SetLanguage normalize prefix `vi` → vi-VN, còn lại en-US; đổi Culture hiện tại + default thread culture + resource dictionary; lỗi lưu/áp dụng log. Chứng cứ: `BusTicketSaleSystem/ViewModels/ProfileViewModel.cs:38-58`, `BusTicketSaleSystem/Views/ProfileView.xaml:39-91`, `BusTicketSaleSystem/Services/LocalizationService.cs:16-59,90-173`.
- Nhiều nhãn/messaging vẫn hard-coded Việt hoặc Anh; ticket/payment status converters **luôn dịch sang Anh**, không dựa culture. Không được ghi “toàn bộ UI song ngữ đầy đủ”. Chứng cứ: `BusTicketSaleSystem/Common/Converters.cs:13-54`, `BusTicketSaleSystem/Views/HomeView.xaml:21-28`, `BusTicketSaleSystem/Views/BookingConfirmationView.xaml:16-20`.
- Selector location lấy danh mục VietnamProvinces; mặc định Thái Nguyên, có thể nhận STATION_LOCATION và normalize; constructor dò IP nền timeout 3 giây qua HTTPS. Không đọc giá trị cấu hình trong khảo sát. Khách đổi location cập nhật singleton/event, không lưu vào account; admin từ chối. Home dùng current location chọn origin có chứa tên; Booking chỉ cập nhật banner. Chứng cứ: `BusTicketSaleSystem/Services/LocationService.cs:26-29,56-86,93-114,131-177,183-232`, `BusTicketSaleSystem/ViewModels/HomeViewModel.cs:133-143,212-219`, `BusTicketSaleSystem/ViewModels/BookingViewModel.cs:81-85`.

## 6. Home / Overview — tìm kiếm hiển thị, dashboard và tuyến phổ biến

### 6.1 Trường và commands

| Thành phần | Giá trị/nội dung và xử lý |
|---|---|
| Origin | Bắt đầu từ CurrentLocation; ComboBox không editable, có text search; load DISTINCT Routes.Origin ORDER BY Origin |
| Destination | Ban đầu rỗng; ComboBox không editable; load DISTINCT destination của origin, hoặc toàn bộ nếu origin trắng |
| Ngày khởi hành | Mặc định DateTime.Today + 1 ngày; DatePicker DisplayDateStart = Today OneTime |
| Số hành khách | Mặc định `1 Passenger`; options 1,2,3,4 dạng chuỗi tiếng Anh |
| SwapLocationsCommand | Tuple swap Origin/Destination; setter Origin kích hoạt async reload destinations nếu origins đã load |
| SearchCommand | Nút Tìm chuyến default Enter và Xem tất cả lịch trình; chỉ navigate Booking |
| QuickBookCommand | Nút Đặt vé của route card, nhận parameter nhưng bỏ qua; chỉ navigate Booking |
| ViewTicketCommand | Xem vé của tôi → MyTickets |
| ViewCompanyCommand | Bấm logo/tên nhà xe trong popular route; lookup tên → concrete detail VM |
| RefreshCommand | Load origins, stats, popular routes tuần tự |

Chứng cứ: `BusTicketSaleSystem/ViewModels/HomeViewModel.cs:14-52,74-119`, `BusTicketSaleSystem/Views/HomeView.xaml:31-36,92-180,291-360`, `BusTicketSaleSystem/Data/DatabaseHelper.cs:310-336`.

- Load origins ưu tiên mục chứa CurrentLocation case-insensitive, rồi previous origin equal, rồi mục đầu, rồi rỗng. Destinations ưu tiên previous destination equal, rồi đầu/rỗng; request version chặn response cũ ghi đè request mới. Lỗi route/destination/stats/popular có StatusMessage tương ứng, nhưng lỗi SQL nhiều helper đã catch thành empty/default nên không luôn vào VM catch. Chứng cứ: `BusTicketSaleSystem/ViewModels/HomeViewModel.cs:123-170,173-209`, `BusTicketSaleSystem/Data/DatabaseHelper.cs:244-249,303-306,333-336`.
- **Gap quan trọng:** Origin/Destination/DepartureDate/SelectedPassengers không được truyền vào Booking. Không gọi SearchTripsAsync từ luồng Home. Chọn ngày hôm sau và 4 hành khách vẫn chỉ mở danh sách nhà xe mặc định; số hành khách không giới hạn số ghế. Đây là kết luận tĩnh từ body command, không phải mô tả kiểm thử runtime. Chứng cứ: `BusTicketSaleSystem/ViewModels/HomeViewModel.cs:86-89`, `BusTicketSaleSystem/ViewModels/BookingViewModel.cs:60`.

### 6.2 Thống kê và công thức

- TotalTickets = COUNT mọi Tickets của account (bao gồm cancelled/reserved, tính booking row không tính từng ghế).
- UpcomingTrips = số ticket row có status Reserved hoặc Paid và departure >= UTC now, không distinct TripId và không lọc PaymentExpiresAt.
- TotalSpent = SUM Price × SeatCount khi Tickets.PaymentStatus=Paid; không dựa Payments ledger và không lọc Tickets.Status. Vé đã yêu cầu hoàn nhưng chưa đổi PaymentStatus vẫn còn đóng góp theo công thức này.
- Tier suy từ TotalSpent: Gold >= 5.000.000; Silver >= 2.000.000; Bronze >= 500.000; còn lại Standard. Không có điểm thưởng, discount, redemption hay ghi tier vào DB ở đây.
- Mặc định khi chưa load: 0 vé, 0 upcoming, `0 VND`, Standard; lỗi SQL stats trả (0,0,0).

Chứng cứ: `BusTicketSaleSystem/ViewModels/HomeViewModel.cs:18-22,173-194`, `BusTicketSaleSystem/Data/DatabaseHelper.cs:225-249`.

### 6.3 Popular routes

- Lấy tối đa 4 routes, GROUP BY route và distance, xếp COUNT Trips giảm dần rồi RouteId. Không lọc trips tương lai/Scheduled; “popular” là nhiều trip row, không phải số vé bán/rating.
- Giá = MIN Trip.Price; thiếu trip → 120.000. Nhà xe/color/logo/intermediate stops dùng MAX/COALESCE độc lập; không đảm bảo công ty lấy MAX là công ty cung cấp min price. Distance null →100 km; duration giả định 50 km/h, giờ/phút lấy integer; tag theo thứ tự Most Popular/Express Daily/Direct Route/Featured (duration/tag không được render trong Home card hiện tại).
- Card hiển thị logo chữ, company name, from→to, stops summary (`Tuyến chạy thẳng cao tốc` nếu rỗng), giá từ và Đặt vé. Empty state nếu không loading và collection rỗng.
- ViewCompany lookup tên exact OR contains (TOP 1 không ORDER BY); nếu không tìm thấy/lỗi SQL thì dựng thông tin fallback in-memory: CompanyId=1, tên đã chọn, hotline/rating/review/description/amenities/logo/color mẫu. Không được ghi fallback là dữ liệu vận hành thật hay đánh giá của người dùng.

Chứng cứ: `BusTicketSaleSystem/Data/DatabaseHelper.cs:253-307,573-605`, `BusTicketSaleSystem/ViewModels/HomeViewModel.cs:91-104,197-209`, `BusTicketSaleSystem/Models/DomainModels.cs:172-188`, `BusTicketSaleSystem/Views/HomeView.xaml:302-384`.

## 7. Booking — danh mục nhà xe, không phải form lọc chuyến hoàn chỉnh

- Exposed commands: BackToHome, Refresh, SelectCompany; tên/logo và nút Chọn nhà xe cùng gọi SelectCompany với BusCompanyItem. Không có input route/date/passenger/search text trong Booking XAML. Chứng cứ: `BusTicketSaleSystem/ViewModels/BookingViewModel.cs:33-47`, `BusTicketSaleSystem/Views/BookingView.xaml:14-85,116-137,184-191`.
- Mỗi card: logo text, company, nhãn tĩnh “Chính hãng”, hotline, operating area, rating F1/5.0, review count N0, description, MinPriceDisplay, TripStatusDisplay. Không có thao tác viết review/gọi hotline; đây là text hiển thị. Chứng cứ: `BusTicketSaleSystem/Views/BookingView.xaml:114-181`, `BusTicketSaleSystem/Models/DomainModels.cs:30-50`.
- Refresh gọi `GetBookingCompaniesAsync(null,null)` không date; SQL trả mọi BusCompanies qua LEFT JOIN, group tên trim, count DISTINCT trips tương lai, min price trip tương lai (fallback 120.000); sort active trips desc, rating desc, tên. **SQL này không kiểm Trips.Status=Scheduled**, và vẫn trả company 0 chuyến. Thông báo “N nhà xe đang hoạt động” là wording VM, không bộ lọc chứng minh company active. Chứng cứ: `BusTicketSaleSystem/ViewModels/BookingViewModel.cs:55-77`, `BusTicketSaleSystem/Data/DatabaseHelper.cs:471-535`.
- Side effect đáng chú ý: đọc danh sách gọi EnsureBusCompaniesSyncedAsync. Lần đầu process dùng Interlocked gate, INSERT công ty còn thiếu từ Buses.CompanyName, tạo metadata fallback (rating/review/description/amenities); lỗi chỉ log. Vì vậy không được chạy luồng này trong cuộc khảo sát read-only bằng cách gọi method. Chứng cứ: `BusTicketSaleSystem/Data/DatabaseHelper.cs:437-473`.
- Loading progress, StatusMessage badge, empty state khi !IsBusy và Companies.Count=0; exception VM clear list, báo DB unavailable. SQL catch trả empty list, có thể hiển thị “không tìm thấy” thay vì lỗi. Chứng cứ: `BusTicketSaleSystem/ViewModels/BookingViewModel.cs:19-30,64-77`, `BusTicketSaleSystem/Data/DatabaseHelper.cs:531-535`.
- **Binding không có implementation:** empty-state “Xem tất cả nhà xe” bind `ShowAllTripsCommand`, nhưng BookingViewModel chỉ có 3 command kể trên. Chưa có action thực hiện cho nút này trong source hiện tại. Chứng cứ: `BusTicketSaleSystem/Views/BookingView.xaml:199-225`, `BusTicketSaleSystem/ViewModels/BookingViewModel.cs:33-48`.

## 8. Chi tiết nhà xe và lịch chuyến

### 8.1 Hiển thị và input

- Header company name, rating, review count, hotline, operating area, description, amenities split theo dấu phẩy trim bỏ rỗng. “Hình ảnh phương tiện” là gradient + bus icon + tên, không tải ảnh xe. Nhãn đối tác chính thức, đúng giờ 4.9, sạch 4.8, an toàn 4.9 là hard-coded, không dòng review từ DB. Chứng cứ: `BusTicketSaleSystem/Views/BusCompanyDetailView.xaml:27-122`, `BusTicketSaleSystem/Models/DomainModels.cs:49-50`.
- DatePicker SelectedDate TwoWay, mặc định Today (khác Home ngày mai); không DisplayDateStart nên có thể chọn ngày quá khứ tại UI. Setter đổi ngày gọi LoadTripsAsync. Back xuất hiện đầu trang và empty state. Chứng cứ: `BusTicketSaleSystem/Views/BusCompanyDetailView.xaml:14-22,145-151,273-277`, `BusTicketSaleSystem/ViewModels/BusCompanyDetailViewModel.cs:19,27-34,84`.
- Mỗi trip: giờ đi/đến, ngày, route, bus type, intermediate stops, biển số, giá, badge trạng thái, nút Chọn ghế & Đặt vé. Nút IsEnabled = IsAvailable, tooltip badge; card opacity=1 hoặc .45. Chứng cứ: `BusTicketSaleSystem/Views/BusCompanyDetailView.xaml:187-255`.

### 8.2 Query/filter/availability

1. Query GetTripsByCompany: tên exact OR escaped substring, Trips.Status=Scheduled; có date → CAST UTC DepartureTime AS date bằng @Date; date null → departure >= SYSUTCDATETIME; sort departure tăng. Reader gắn Kind.Utc. DatePicker ngày local không chuyển thành khoảng UTC theo múi giờ. Chứng cứ: `BusTicketSaleSystem/Data/DatabaseHelper.cs:608-659`.
2. AvailableSeats = Buses.SeatCount − COUNT DISTINCT TicketSeats.SeatId của trip mà ticket status Reserved/Paid/Used **và** PaymentStatus=Paid hoặc PaymentExpiresAt>UTC now. Không đếm bằng số actual Seats row; thông tin card có thể khác map nếu SeatCount metadata không khớp Seats. Chứng cứ: `BusTicketSaleSystem/Data/DatabaseHelper.cs:611-617,669-679`.
3. `TripSearchResult.IsAvailable`: available >0 và departure sau UTC now; EnsureUtc đổi Local→UTC, UTC giữ nguyên, Unspecified giữ nguyên. Badge ưu tiên đã khởi hành, rồi còn chỗ/hết chỗ. Không có live timer refresh company trip availability trong VM này. Chứng cứ: `BusTicketSaleSystem/Models/DomainModels.cs:74-89`, `BusTicketSaleSystem/ViewModels/BusCompanyDetailViewModel.cs:119-158`.
4. Constructor có origin/destination optional. VM lọc contains case-insensitive trên kết quả SQL nhưng nếu lọc ra zero trong khi raw trips >0 thì trả lại toàn bộ raw trips. Luồng Home/Booking không truyền hai filter này hoặc travelDate. Chứng cứ: `BusTicketSaleSystem/ViewModels/BusCompanyDetailViewModel.cs:69-84,126-143`, `BusTicketSaleSystem/ViewModels/HomeViewModel.cs:103`, `BusTicketSaleSystem/ViewModels/BookingViewModel.cs:45`.
5. Load gate SemaphoreSlim.WaitAsync(0): request đến khi đang load bị bỏ qua, không queue; đổi ngày nhanh có thể đổi SelectedDate nhưng request tương ứng không chạy. Không có request-version như destinations Home. Exception báo không tải chuyến; finally cập nhật HasTrips/HasNoTrips/ActiveTripsCount. SQL exception có thể bị helper nuốt thành empty list. Chứng cứ: `BusTicketSaleSystem/ViewModels/BusCompanyDetailViewModel.cs:119-156`, `BusTicketSaleSystem/Data/DatabaseHelper.cs:662-666`.

### 8.3 Commands exposed và phần chỉ có trong VM

- **Exposed:** BookTripCommand nhận Trip parameter hoặc fallback SelectedTrip; trip null return; unavailable báo không thể đặt do hết chỗ/đã khởi hành; available navigate SeatSelection(trip). BackCommand → Booking.
- **Implemented/not exposed trong XAML hiện tại:** SelectTripCommand; BookSelectedTripCommand (can execute khi selected available); RefreshCommand; SelectedTrip mặc định chuyến available đầu tiên, nếu không có thì dòng đầu.
- **Pickup/dropoff chỉ là state VM:** StopsList = origin + intermediate split comma + destination, distinct case-insensitive; cả pickup/dropoff collections chứa cùng list. Chọn trip đặt pickup đầu, dropoff cuối (fallback origin/destination). XAML không có control chọn pickup/dropoff; BookTrip/BookSelected chỉ truyền Trip, không truyền selected stops; không có công thức giá theo chặng trong luồng khách.

Chứng cứ: `BusTicketSaleSystem/ViewModels/BusCompanyDetailViewModel.cs:37-67,85-115,143,159-175`, `BusTicketSaleSystem/Models/DomainModels.cs:91-98`, `BusTicketSaleSystem/Views/BusCompanyDetailView.xaml:145-151,242-254`.

## 9. Chọn ghế và thông tin hành khách

### 9.1 Input, map, defaults

- Trip nguyên snapshot; PassengerName default account.FullName hoặc rỗng, PassengerPhone default account.Phone hoặc rỗng. Một tên + một phone dùng cho **cả booking nhiều ghế**, không danh sách hành khách mỗi ghế. Không có email/giấy tờ/tuổi/đối tượng giảm giá/pickup/dropoff tại màn này. Chứng cứ: `BusTicketSaleSystem/ViewModels/SeatSelectionViewModel.cs:19-32`, `BusTicketSaleSystem/Views/SeatSelectionView.xaml:286-305`.
- Map là ListBox SelectionMode Multiple + WrapPanel cell 80×70, không sơ đồ tọa độ lưu theo xe hoặc chia tầng. Mỗi item có SeatId, SeatNumber, IsBooked, IsSelected; ghế booked bị disable, màu xám, cursor No và nhãn booked; selected xanh. Legend Available/Selected/Already booked, Front/Rear/Door/Driver chỉ hình minh họa. Chứng cứ: `BusTicketSaleSystem/Views/SeatSelectionView.xaml:114-175,179-277`, `BusTicketSaleSystem/ViewModels/SeatSelectionViewModel.cs:188-203`.
- Query lấy Seats theo Trip.BusId ORDER BY SeatId; IsBooked EXISTS ticket seat cùng trip có active status và settled hoặc hold còn hạn. Không đọc global Seats.IsBooked. Chứng cứ: `BusTicketSaleSystem/Data/DatabaseHelper.cs:669-695`.
- Giá đơn vị lấy Trip.Price; total preview = Trip.Price × selected count, format N0 VND; chưa chọn hiển thị `None`, `0 VND`. Số ghế free/booked đếm collection, không trừ số đang chọn khỏi free count. Chứng cứ: `BusTicketSaleSystem/ViewModels/SeatSelectionViewModel.cs:45-53`.

### 9.2 Refresh và đồng thời

- IRefreshable load map, sau đó start DispatcherTimer mỗi 20 giây. Nút Refresh gọi cùng RefreshAsync. Khi rebuild map giữ selected theo SeatNumber case-insensitive nếu ghế vẫn free; nếu ghế vừa taken/mất khỏi map thì bỏ selected và báo rõ seat numbers. Không giữ chỗ tại DB khi chỉ click chọn. Chứng cứ: `BusTicketSaleSystem/ViewModels/SeatSelectionViewModel.cs:14,94-105,117-155`.
- Status bình thường là free/booked count; map rỗng báo chưa configured. Exception manual refresh báo lỗi; auto refresh exception chỉ log, không ghi StatusMessage lỗi mới. SQL helper catch trả empty list có thể khiến VM hiểu là chưa có map. Chứng cứ: `BusTicketSaleSystem/ViewModels/SeatSelectionViewModel.cs:151-174`, `BusTicketSaleSystem/Data/DatabaseHelper.cs:691-695`.
- Dispose dừng timer, tháo Tick và PropertyChanged; được gọi khi Back, navigation dispose, hoặc view Unloaded. LoadAsync có guard disposed trước/sau await nhưng không có load semaphore nên timer/manual có thể overlap. Chứng cứ: `BusTicketSaleSystem/ViewModels/SeatSelectionViewModel.cs:88-114,119-145`, `BusTicketSaleSystem/Views/SeatSelectionView.xaml.cs:18-20`.

### 9.3 Continue, back và validation

- Commands đủ: RefreshCommand; BackToBookingCommand (2 nút); GoToConfirmationCommand.
- Continue không có can-execute giới hạn ghế; chạy lần lượt: ít nhất 1 selected, không SeatInfo.IsBooked, PassengerName không whitespace; lỗi inline, ở lại trang. Thành công trim name/phone, tạo Confirmation với toàn bộ selected seats theo thứ tự collection. Không gọi DB kiểm lại availability tại thời điểm Continue; definitive booking xảy ra tại Confirmation.
- Phone textbox không MaxLength hoặc attached restriction tại màn này; VM không regex/required phone hoặc max name; không rule max 4 ghế, liền kề, giới tính, loại ghế hay giá khác nhau từng vị trí. SelectedPassengers của Home không được dùng.
- Back về Booking mới, không giữ company/day/selected state.

Chứng cứ: `BusTicketSaleSystem/ViewModels/SeatSelectionViewModel.cs:28-29,65-98`, `BusTicketSaleSystem/Views/SeatSelectionView.xaml:43-53,294-304,333-345`.

## 10. Xác nhận booking và điểm nối thanh toán

### 10.1 Nội dung review và lựa chọn

- Trip origin/destination, datetime departure/arrival, bus type, seat numbers; passenger name/phone readonly; seat count, fare total. Các nhãn Central bus station, Arrival station, Direct, WiFi/free water, insurance included, baggage 20kg included, VAT included là XAML text tĩnh, không dữ liệu tính phí riêng hay tùy chọn bảo hiểm/hành lý.
- Constructor từ 1 ghế được chuyển sang danh sách; danh sách rỗng throw ArgumentException. `Seat` convenience lấy Seats[0]; tổng = Trip.Price × SeatCount.
- PaymentMethods hiện chỉ **Cash** (“Cash on board”) và **BankTransfer** (“Bank transfer (VietQR)”), cả hai enabled; mặc định BankTransfer. VNPay không nằm trong list mặc dù có nhánh switch xử lý VNPay trong code. Không diễn giải comment subscription/config thành bằng chứng cấu hình runtime.
- ComboBox bind method; item disabled theo IsEnabled; warning có thể hiển thị lý do selected disabled hoặc lý do của option disabled đầu tiên. Hiện 2 option đều enabled nên warning thường rỗng.
- Commands: CompleteBookingCommand AsyncRelayCommand chống re-entry; BackToSeatsCommand predicate !IsBusy, có 2 nút cùng command. Progress khi busy; StatusMessage hiển thị cả banner đầu trang lẫn dưới nút.

Chứng cứ: `BusTicketSaleSystem/Views/BookingConfirmationView.xaml:60-151,164-248`, `BusTicketSaleSystem/ViewModels/BookingConfirmationViewModel.cs:12,31-61,63-96`.

### 10.2 Luồng success/error và persistence

1. Complete kiểm CurrentUser !=null, selected method enabled; không revalidate name/phone/Trip.IsAvailable ngay tại method. IsBusy=true.
2. BookTicketsAsync nhận account ID, trip ID, SeatIds, name, phone. Tạo **một Tickets row cho tất cả ghế**, một mã vé, nhiều TicketSeats. Fail tuple → status, không tạo payment.
3. CreatePaymentAsync nhận TicketId/TicketCode/method code; không dùng preview price từ UI làm input amount ở điểm gọi này. Tạo **một payment cho booking**.
4. Nếu tạo payment thất bại: gọi CancelTicketAsync để release; nếu cancel cũng fail thì log mã vé và nhắc hold còn đến expiry sweep. Status dùng payment.ErrorMessage; fallback nói ghế đã released dù nhánh compensation có thể thất bại — cần phân biệt thông điệp và persistence thực tế.
5. Cash: toast reserved, navigate MyTickets; không mở dialog; không phải bằng chứng đã thu tiền.
6. BankTransfer: dialog request chứa ticket code, Ticket.TotalPrice, transactionNo, payment URL, IsBankTransfer=true, hold display deadline tính ticket.BookingTime+15 phút; outcome TransferSubmitted → success toast, mọi outcome khác warning chưa submit. Đều navigate MyTickets sau đóng dialog; không gọi settle từ Confirmation.
7. Nhánh VNPay hiện không reachable từ list lựa chọn bình thường: dialog IsBankTransfer=false, GatewayConfirmed toast success, khác warning pending; sau dialog MyTickets.
8. Exception catch log, status “The booking could not be completed”; finally IsBusy=false. Không có transaction bao trùm cả booking và payment ở VM; rollback payment fail dùng compensation nêu trên.

Chứng cứ: `BusTicketSaleSystem/ViewModels/BookingConfirmationViewModel.cs:99-213`.

### 10.3 Quy tắc booking enforce trong helper/SQL đã đọc

- Reject danh sách rỗng, reject SeatId trùng (không âm thầm deduplicate cho success). Trước transaction gọi ExpirePendingPaymentsAsync; lỗi SQL expiry → không book. Book core bọc SqlResilience, transaction isolation Serializable. Chứng cứ: `BusTicketSaleSystem/Data/DatabaseHelper.cs:709-778`.
- Ticket INSERT SELECT từ Trips theo TripId: đơn giá **tr.Price hiện tại ở DB**, SeatCount=request count, status `Đã đặt`, payment `Chưa thanh toán`, PaymentExpiresAt=SYSUTCDATETIME+15 phút. Do đó preview Trip.Price snapshot có thể khác giá snapshot lúc thực book; UI dialog sau đó lấy Ticket.TotalPrice đã read-back. Chứng cứ: `BusTicketSaleSystem/Data/DatabaseHelper.cs:757-762`, `BusTicketSaleSystem/ViewModels/BookingConfirmationViewModel.cs:179-185`.
- TicketCode dạng `BT-` + UTC yyyyMMddHHmmss + random 4 chữ số từ GetInt32(1000,9999). Seat junction INSERT bảo đảm Seat thuộc bus của trip và chưa có active ticket giữ theo predicate paid/hold còn hạn. Một ghế insert 0 rows → rollback toàn bộ, báo ghế taken/invalid. Read-back thất bại → rollback; đủ tất cả → commit. Chứng cứ: `BusTicketSaleSystem/Data/DatabaseHelper.cs:764-834`.
- SQL error 2601/2627/51001/51002 được map về taken/invalid; lỗi khác báo DB/trip data. Transaction lỗi transient rollback rồi retry ở wrapper; không khẳng định hiện trạng index/trigger DB triển khai thực tế chỉ từ catch error. Schema/triggers thuộc báo cáo dữ liệu. Chứng cứ: `BusTicketSaleSystem/Data/DatabaseHelper.cs:836-853`.
- Helper body booking này không tự xác thực CurrentUser, không kiểm passenger nonempty/phone format, và INSERT ticket chỉ WHERE TripId (không thêm status Scheduled hoặc departure tương lai). VM company đã check availability, nhưng đó không phải cùng enforcement với SQL booking. Cần đọc ràng buộc/trigger riêng trước kết luận tổng thể DB cho phép mọi invalid path. Chứng cứ: `BusTicketSaleSystem/ViewModels/BusCompanyDetailViewModel.cs:93-100`, `BusTicketSaleSystem/Data/DatabaseHelper.cs:709-774`.

## 11. Vé của tôi — danh sách active, hủy giữ chỗ, yêu cầu hoàn

### 11.1 Collection và trường hiển thị

- Refresh/load phải có session; gọi GetTicketsAsync(accountId,false). SQL lọc status Reserved/Paid, DepartureTime>=UTC now, ORDER BY DepartureTime DESC. Không có lọc payment deadline trong danh sách; hold đã hết nhưng chưa được lifecycle đổi status vẫn có thể xuất hiện. Không paging, tìm theo code hoặc lọc trạng thái tại XAML.
- Mỗi card: code, ticket status converter, payment status converter, route, date dd/MM/yyyy, time HH:mm, bus type (fallback Standard coach), seat numbers, total N0 VND. Status/date là snapshot lúc load.
- Nút header Refresh, BookNewTicket; empty-state BookNewTicket. Progress IsLoading, StatusMessage, ErrorMessage và confirmation panel riêng.

Chứng cứ: `BusTicketSaleSystem/ViewModels/MyTicketsViewModel.cs:232-288`, `BusTicketSaleSystem/Data/DatabaseHelper.cs:1052-1078`, `BusTicketSaleSystem/Views/MyTicketsView.xaml:24-98,120-191,199-223`.

### 11.2 Visibility/permissions theo vé

| Action | Điều kiện source | Kết quả UI |
|---|---|---|
| ViewDetailsCommand | parameter TicketItemViewModel và Ticket.PaymentStatus=Paid | Nút Xem vé điện tử chỉ hiện khi paid; navigate TicketView(snapshot) |
| Pending display | !CanViewETicket | Nút Chờ thanh toán disabled, không có command retry/resume payment |
| CancelTicketCommand | Ticket.Status=Reserved và DepartureTime>UTC now | Nút Hủy giữ chỗ hiện, stage confirmation |
| RequestRefundCommand | Status=Paid AND PaymentStatus=Paid AND departure tương lai | Nút Yêu cầu hoàn vé hiện, stage confirmation |
| ConfirmPendingAction | PendingTicket !=null và pending action !=None | Async thực hiện action staged |
| DismissPendingAction | Không predicate | Xóa pending, không ghi DB; nút giữ vé IsCancel=True |

Chứng cứ: `BusTicketSaleSystem/ViewModels/MyTicketsViewModel.cs:95-119,263-288`, `BusTicketSaleSystem/Models/DomainModels.cs:142-165`, `BusTicketSaleSystem/Views/MyTicketsView.xaml:86-95,159-191`.

**Lưu ý enforcement:** RelayCommand.CanExecute dùng để WPF hỏi trạng thái, nhưng RelayCommand.Execute gọi thẳng delegate không tự guard CanExecute. StagePendingAction chỉ kiểm kiểu parameter; SQL vẫn phải chịu trách nhiệm kiểm invalid action. Test gọi Execute trực tiếp không tương đương chứng minh nút enabled thật. Chứng cứ: `BusTicketSaleSystem/Common/RelayCommand.cs:33-40`, `BusTicketSaleSystem/ViewModels/MyTicketsViewModel.cs:122-129`.

### 11.3 Hai bước xác nhận và side effects

- Click Hủy/Hoàn chỉ xóa status/error, lưu pending action và pending ticket; không DB write. Prompt hủy gồm ticket code + seat numbers; prompt hoàn code + total price, dùng localization resources. Confirm chụp item/action rồi clear panel **trước await**; dispatch Cancel hoặc Refund. Dismiss clear thôi. Chứng cứ: `BusTicketSaleSystem/ViewModels/MyTicketsViewModel.cs:34-81,122-153`.
- Cancel/Refund đều kiểm session, set loading, clear messages; gọi helper theo ticket/account. Success toast + LoadAsync; thất bại tuple vào ErrorMessage; exception log + thông báo cụ thể; finally reset IsLoading. Reload không polling: thay đổi payment ở nơi khác cần refresh hoặc vào lại page. Chứng cứ: `BusTicketSaleSystem/ViewModels/MyTicketsViewModel.cs:158-260`.
- Cancel SQL: nếu ticket của account có PaymentStatus Paid thì delegate RequestRefund; otherwise transaction lock ticket, yêu cầu có ownership, Status Reserved hoặc Paid và departure>UTC. Thành công Status Cancelled, CancelledAt UTC, clear PaymentExpiresAt; mọi payment Pending → Failed, note mặc định nếu chưa có. Không delete Tickets/TicketSeats; ghế được query active bỏ qua khi cancelled. Lỗi phân biệt không thấy, cancelled/used, đã khởi hành, SQL fail. Chứng cứ: `BusTicketSaleSystem/Data/DatabaseHelper.cs:883-970`.
- RequestRefund SQL: lock ticket thuộc account; reject not found, cancelled, used, không đồng thời paid ở cả hai trạng thái, hoặc đã khởi hành. Thành công **hủy vé/giải phóng ghế ngay**, set CancelledAt, clear expiry; stamp RefundRequestedAt cho Payments Success chưa yêu cầu. Không đổi PaymentStatus sang Refunded, không chuyển tiền trong method này. Payment approval thuộc admin/payment chuyên đề; toast “requested” không phải “money returned”. Chứng cứ: `BusTicketSaleSystem/Data/DatabaseHelper.cs:979-1048`.

## 12. Vé điện tử, in, export, email và bản đồ

### 12.1 Dữ liệu và trạng thái

- TicketViewModel nhận TicketDetails readonly snapshot, không IRefreshable/poll/SELECT khi mở. QR image tạo từ **TicketCode** qua TicketPrintService ngay constructor. MyTickets chỉ mở nếu paid, nhưng TicketViewModel tự nó nhận bất kỳ status và không ownership/payment guard trong constructor. Chứng cứ: `BusTicketSaleSystem/ViewModels/TicketViewModel.cs:10-18,77-88`, `BusTicketSaleSystem/ViewModels/MyTicketsViewModel.cs:95-101`.
- Banner state theo Ticket.Status: Paid = paid and confirmed / đưa boarding pass tại platform; Reserved = payment outstanding / hoàn thành trước hold expiry; Used = already used / không dùng lại; Cancelled = no longer valid; unknown = Ticket issued. Glyph và màu theo status (paid success, reserved warning, cancelled danger, used neutral), không chỉ xanh cố định. Chứng cứ: `BusTicketSaleSystem/ViewModels/TicketViewModel.cs:37-67`, `BusTicketSaleSystem/Common/Converters.cs:177-194`.
- Boarding-pass UI gồm brand tĩnh, code, origin/destination, departure/arrival, route, coach type, số ghế và seat numbers, status, total fare, booking timestamp, QR. UI không render PassengerName/Phone/LicensePlate dù model có; in/export dùng TicketPrintModel có thể có chúng (implementation tích hợp xem báo cáo chuyên đề). Chứng cứ: `BusTicketSaleSystem/Views/TicketView.xaml:43-177,226-228`, `BusTicketSaleSystem/Models/DomainModels.cs:127-132`, `BusTicketSaleSystem/ViewModels/TicketViewModel.cs:91`.
- Barcode dài là dãy Rectangle fixed widths, không encoder theo ticket; text code dưới nó và QR là dynamic. “Verified & Insured” tĩnh không phải kết quả validate backend. Chứng cứ: `BusTicketSaleSystem/Views/TicketView.xaml:172-174,186-228`.

### 12.2 Inventory commands và kết quả lỗi

| Command/control | Thực thi và side effect yêu cầu | Feedback |
|---|---|---|
| PrintTicket | Clear messages → TicketPrintService.PrintTicket(TicketPrintModel.FromTicket) | Sau service return báo sent to printer; exception log và ErrorMessage có ex.Message; không coi return là chứng minh máy in đã in |
| ExportPng | SaveFileDialog PNG, DefaultExt .png, AddExtension, filename Ticket_CODE.png; cancel dialog return | Service export vào path đã chọn; status path; exception log/error |
| ExportPdf | SaveFileDialog PDF tương tự, Ticket_CODE.pdf | Service export; status path; exception log/error |
| EmailTicket | AsyncRelayCommand predicate !IsBusy && email configured && session email nonempty; recipient **email account hiện tại**, không ô nhập địa chỉ và không passenger phone/name | Sending → await ExternalApiServices.SendTicketEmailAsync(Ticket,recipient) → success; ExternalServiceException hiện ex.Message; exception khác thông điệp generic; finally reset busy |
| ViewRouteOnMap | ExternalApiServices.OpenRouteMap(Ticket.Origin,Ticket.Destination) | Thành công báo browser mở; catch ExternalServiceException log/error |
| BackToOverview | Navigate Home | Không giữ ticket page |
| BackToMyTickets | Navigate MyTickets | Collection được load lại qua IRefreshable |

Chứng cứ: `BusTicketSaleSystem/ViewModels/TicketViewModel.cs:69-212`, controls `BusTicketSaleSystem/Views/TicketView.xaml:236-332`.

- EmailAvailabilityNote nêu thiếu cấu hình BREVO_API_KEY/EMAIL_SENDER_ADDRESS, hoặc cần thêm email profile, hoặc địa chỉ sẽ nhận PDF; note hiển thị cả text và tooltip. Không ghi giá trị credential. Nút email không được điều kiện hóa riêng bởi Ticket.Status ở VM. Các nút print/export/map cũng không có busy/paid predicate. Chứng cứ: `BusTicketSaleSystem/ViewModels/TicketViewModel.cs:24-35,84-88`, `BusTicketSaleSystem/Views/TicketView.xaml:262-283`.
- Có status banner xanh nhạt, error đỏ, progress khi emailing; không có upload, share link, resend tự động hay tự email ngay sau đặt vé ở Confirmation. Chứng cứ: `BusTicketSaleSystem/Views/TicketView.xaml:279-303`, `BusTicketSaleSystem/ViewModels/BookingConfirmationViewModel.cs:126-167`.

## 13. Lịch sử chuyến đi

- Refresh yêu cầu session; GetTicketsAsync(account,true) lấy: mọi ticket Used/Cancelled **hoặc** Reserved/Paid có departure<UTC now, sort departure DESC. Vì vậy history bao gồm vé hủy tương lai và vé quá giờ chưa được đổi sang Used; không chỉ “đã hoàn thành”. Chứng cứ: `BusTicketSaleSystem/ViewModels/HistoryViewModel.cs:46-64`, `BusTicketSaleSystem/Data/DatabaseHelper.cs:1055-1061`.
- Bảng hiển thị date, time, route, coach, seats, fare total, ticket status và Book Again. `PastTripItemViewModel` có TicketCode, Origin/Destination, DistanceKm, SeatCount, PaymentStatus nhưng XAML không render các cột này. Không có view detail/in/email/history refund trên dòng. Chứng cứ: `BusTicketSaleSystem/ViewModels/HistoryViewModel.cs:80-101`, `BusTicketSaleSystem/Views/HistoryView.xaml:78-153`.
- BookAgain nhận parameter nhưng bỏ qua, navigate Booking chung; empty state Tìm chuyến xe mới gọi cùng command. Không đặt lại đúng tuyến/ngày/nhà xe/ghế/hành khách cũ. Chứng cứ: `BusTicketSaleSystem/ViewModels/HistoryViewModel.cs:41`, `BusTicketSaleSystem/Views/HistoryView.xaml:143-153,177-181`.
- Stats “Trips Completed” = COUNT ticket row có Used hoặc Reserved/Paid đã departure; “km Traveled” = SUM Routes.DistanceKm mỗi booking row tương ứng. Không nhân SeatCount, không distinct TripId, không tính Cancelled. Vé Reserved chưa thanh toán nhưng đã qua giờ vẫn thuộc điều kiện nếu chưa lifecycle hủy. Default 0 Trips Completed / 0 km Traveled; helper SQL lỗi trả zeros. Chứng cứ: `BusTicketSaleSystem/ViewModels/HistoryViewModel.cs:13-14,62-64`, `BusTicketSaleSystem/Data/DatabaseHelper.cs:1082-1109`.
- IsLoading progress; ErrorMessage đỏ; exception clear list, empty state; helper swallow SQL cũng có thể thành empty và zero mà không banner lỗi. Chứng cứ: `BusTicketSaleSystem/ViewModels/HistoryViewModel.cs:54-76`, `BusTicketSaleSystem/Views/HistoryView.xaml:59-72,160-181`.

## 14. Domain model, giá, trạng thái và datetime cần giữ nguyên khi phân tích

### 14.1 Inventory model phía khách

| Model | Payload và derived fields |
|---|---|
| UserSession / CurrentUser | AccountId, Username, FullName, Email?, Phone?, Role; ambient Account nullable; IsAdmin |
| BusCompanyItem | CompanyId, CompanyName, Hotline, Rating, ReviewCount, OperatingArea, Description, Amenities, LogoText, BrandColor; MinPrice default 120.000, ActiveTripsCount default 0; RatingDisplay F1, ReviewCountDisplay N0, MinPriceDisplay, HasActiveTrips, TripStatusDisplay, AmenityList |
| TripSearchResult | TripId, Origin, Destination, BusType, DepartureTime, ArrivalTime, Price, AvailableSeats; company/color/logo/intermediate stops/driver name/driver phone/license plate có defaults; Route, RouteWithStops, Schedule, PriceDisplay, FormattedFare, AvailableSeatsDisplay, IsAvailable, CardOpacity, StatusBadgeText, StopsList |
| SeatInfo | SeatId, BusId, SeatNumber, IsBooked; DisplayName append booked |
| TicketDetails | TicketId, TicketCode, Route, DepartureTime, ArrivalTime, BusType, SeatNumbers, Price, Status, BookingTime, Origin, Destination, DistanceKm; init SeatCount default1, PassengerName, PassengerPhone?, LicensePlate, PaymentStatus default Unpaid, PaymentExpiresAt?, UsedAt?, CancelledAt?; TotalPrice, IsPaid, IsUsed, IsCancelled, IsAwaitingPayment, CanRequestRefund, formatted datetime |
| PopularRouteItem | RouteId, From, To, Duration, Price dạng string, Tag, MinPrice decimal, company/color/logo/intermediate stops; RouteDisplay, StopsSummary |
| RouteSummaryItem | RouteId, Origin, Destination, DistanceKm?; không thấy control khách sử dụng trong các ViewModel đã khảo sát |
| PaymentOption | Code, Display, IsEnabled, DisabledReason; khai báo tại ConfirmationViewModel, không Models folder |
| SelectableSeatViewModel / TicketItemViewModel / PastTripItemViewModel | Adapter UI cho selection/visibility/display, không entity persistence mới |

Chứng cứ: `BusTicketSaleSystem/Models/UserSession.cs:3-15`, `BusTicketSaleSystem/Models/DomainModels.cs:30-195`, `BusTicketSaleSystem/ViewModels/BookingConfirmationViewModel.cs:12`, `BusTicketSaleSystem/ViewModels/SeatSelectionViewModel.cs:188-203`, `BusTicketSaleSystem/ViewModels/MyTicketsViewModel.cs:263-288`, `BusTicketSaleSystem/ViewModels/HistoryViewModel.cs:80-101`. `AdminModels.cs` được trích xuất ở báo cáo Admin, không biến chúng thành chức năng khách.

### 14.2 Hai trục trạng thái

| Giá trị lưu / constant | Ý nghĩa trong UI khách | Không được đồng nhất |
|---|---|---|
| `Đã đặt` / Reserved | Hold/payment outstanding | Không phải đã thu tiền |
| `Đã thanh toán` / Paid | Có thể là Tickets.Status hoặc PaymentStatus | IsPaid chỉ đọc PaymentStatus |
| `Đã hủy` / Cancelled | Vé không hợp lệ/ghế giải phóng | Có thể PaymentStatus vẫn Paid trong thời gian chờ hoàn |
| `Đã sử dụng` / Used | Đã sử dụng | History còn lấy cả vé qua giờ mà chưa chuyển trạng thái |
| `Chưa thanh toán` / Unpaid | PaymentStatus chưa thu | Không đồng nghĩa luôn còn hạn giữ ghế |
| `Hoàn tiền` / Refunded | PaymentStatus đã hoàn | RequestRefund chưa tự đặt giá trị này |

Chứng cứ: `BusTicketSaleSystem/Models/DomainModels.cs:8-26,142-165`, `BusTicketSaleSystem/Data/DatabaseHelper.cs:1018-1022,1057-1059`.

- IsAwaitingPayment = Reserved && Unpaid && (deadline null hoặc > UTC now). Availability SQL yêu cầu deadline>UTC hoặc paid; do đó null expiry được model coi awaiting nhưng không block theo query map nếu unpaid. Ghi rõ khác biệt, không thay code trong khảo sát. Chứng cứ: `BusTicketSaleSystem/Models/DomainModels.cs:148-151`, `BusTicketSaleSystem/Data/DatabaseHelper.cs:675-677`.
- Tổng tiền khách duy nhất trong đường book: per-seat Trip.Price → snapshot Tickets.Price; TotalPrice=Price×SeatCount. Không cộng phí dịch vụ/VAT riêng, không giảm tier, không áp giá theo chặng ở customer flow đã đọc. Không suy ra nhãn “VAT included/insurance included” là bảng thuế hoặc chính sách hoàn. Chứng cứ: `BusTicketSaleSystem/Models/DomainModels.cs:121-125`, `BusTicketSaleSystem/Data/DatabaseHelper.cs:759-761`, `BusTicketSaleSystem/Views/BookingConfirmationView.xaml:170-190`.
- SQL reader gắn UTC cho departure/arrival/booking/expiry/used/cancelled; một số model formatted property đổi UTC→local. Nhưng nhiều controls bind raw DateTime bằng StringFormat và TicketItem/PastTrip dùng ToString raw, SeatSelection cũng format raw. Không khẳng định mọi màn hiển thị giờ local nhất quán. Chứng cứ: `BusTicketSaleSystem/Data/DatabaseHelper.cs:1164-1186`, `BusTicketSaleSystem/Models/DomainModels.cs:153-169`, `BusTicketSaleSystem/ViewModels/MyTicketsViewModel.cs:268-269`, `BusTicketSaleSystem/ViewModels/HistoryViewModel.cs:88-89`, `BusTicketSaleSystem/ViewModels/SeatSelectionViewModel.cs:53`, `BusTicketSaleSystem/Views/TicketView.xaml:89,102,170`, `BusTicketSaleSystem/Views/BusCompanyDetailView.xaml:209-214`.

## 15. Feedback, interaction kỹ thuật và trạng thái lỗi dùng chung

- AsyncRelayCommand tự chặn re-entry cả khi Execute gọi trực tiếp, await handler, IsRunning, catch log và callback onError tùy command; không transaction hay cancellation token tự động. RelayCommand chỉ CanExecute predicate cho caller/UI, Execute không tự enforce. Chứng cứ: `BusTicketSaleSystem/Common/AsyncRelayCommand.cs:57-86`, `BusTicketSaleSystem/Common/RelayCommand.cs:33-40`.
- NotificationService là singleton một message hiện tại, message mới thay message trước và restart timer; Info/Success/Warning 6 giây, Error 10 giây; Dismiss clear. Toast pinned ở shell ngoài scroller, tồn tại khi page đổi; host dispose subscription khi unload. Chứng cứ: `BusTicketSaleSystem/Services/NotificationService.cs:25-77`, `BusTicketSaleSystem/Views/NotificationHost.xaml.cs:8-14`, `BusTicketSaleSystem/Views/NotificationHost.xaml:5-20,51-59`.
- Các page code-behind Home/Booking/Company/Confirmation/MyTickets/History/Ticket chỉ InitializeComponent, không event handler bổ sung ẩn sau command. Riêng Login/SignUp quản window/password; Profile quản PasswordBox event/reset; Seat dispose khi unload; Main scroll/logout; NotificationHost quản subscription. Chứng cứ: `BusTicketSaleSystem/Views/HomeView.xaml.cs:8-13`, `BusTicketSaleSystem/Views/BookingView.xaml.cs:8-13`, `BusTicketSaleSystem/Views/BusCompanyDetailView.xaml.cs:5-10`, `BusTicketSaleSystem/Views/BookingConfirmationView.xaml.cs:8-13`, `BusTicketSaleSystem/Views/MyTicketsView.xaml.cs:8-13`, `BusTicketSaleSystem/Views/HistoryView.xaml.cs:8-13`, `BusTicketSaleSystem/Views/TicketView.xaml.cs:8-13` và các mục code-behind ở trên.
- SearchTripsAsync **có implementation helper**: origin/destination LIKE escaped substring, date optional, Scheduled, available seat count, sort departure; nhưng tìm source ứng dụng không thấy caller ngoài khai báo/ghi chú migration. Không có màn search kết quả TripSearchResult tổng quát nối với Home Search trong hiện trạng. Chứng cứ: `BusTicketSaleSystem/Data/DatabaseHelper.cs:360-423`, body commands `BusTicketSaleSystem/ViewModels/HomeViewModel.cs:86-87`, `BusTicketSaleSystem/ViewModels/BookingViewModel.cs:60`.
- Read helpers thường catch SqlException, log và trả []/null/zero: dashboard, routes, locations, companies, trips, seats, tickets, history. UI empty state và unavailable thật không phải luôn phân biệt được. Đây là hành vi source, không chứng minh DB thực đang lỗi. Chứng cứ tập trung: `BusTicketSaleSystem/Data/DatabaseHelper.cs:244-249,303-306,333-336,531-535,601-605,662-666,691-695,1074-1078,1105-1109`.

## 16. Đối chiếu source test — không chạy, không báo pass

| Source test đã đọc | Kịch bản chứng minh ý định kiểm thử | Giới hạn kết luận |
|---|---|---|
| `BusTicketSaleSystem.Tests/NavigationAndViewModelTests.cs:16-59` | Navigation current VM, PropertyChanged, thiếu factory, null instance | Không chứng minh routing WPF thật hay role guard |
| `BusTicketSaleSystem.Tests/NavigationAndViewModelTests.cs:75-114` | Signup password thiếu length/upper/lower/digit, confirm mismatch | Test gọi command input invalid; không phải đăng ký thành công DB |
| `BusTicketSaleSystem.Tests/NavigationAndViewModelTests.cs:168-233` | Chọn company, back, book trip future vs past | Source test sử dụng VM/navigation; không xác minh live availability |
| `BusTicketSaleSystem.Tests/NavigationAndViewModelTests.cs:259-304` | Instantiate Company/Booking views trên STA và resource dictionaries | Không tương đương UI interaction/ảnh render |
| `BusTicketSaleSystem.Tests/NavigationAndViewModelTests.cs:568-596,598-662` | Đổi ngôn ngữ/event, async command reentry/fault, palette paid/cancelled/reserved | Không chứng minh mọi hardcoded label đã localize |
| `BusTicketSaleSystem.Tests/NavigationAndViewModelTests.cs:664-717` | Stage/dismiss hủy/hoàn không DB trước confirm | Execute trực tiếp có thể bypass RelayCommand.CanExecute; fixture Ticket.PaymentStatus để mặc định, nên không dùng test này chứng minh nút refund enable đúng |
| `BusTicketSaleSystem.Tests/NavigationAndViewModelTests.cs:744-779` | Normalize province aliases, customer được đổi location, admin bị khóa | Không chứng minh filtering danh sách nhà xe bằng location |
| `BusTicketSaleSystem.Tests/DomainModelTests.cs:108-193` | Pending không view e-ticket, paid có; confirmation nhiều ghế; total=unit×seat count | Các model/unit checks, không live DB |
| `BusTicketSaleSystem.Tests/DomainModelTests.cs:209-262` | Format price/schedule/company và future/past/full availability | Không proof timezone nhất quán mọi view |
| `BusTicketSaleSystem.Tests/BookingConfirmationFlowTests.cs:19-84` | Bank transfer giữ seat, dialog amount, navigate MyTickets; Cash không dialog | SkippableFact cần SQL fixture; dialog fake; không giao dịch ngân hàng thật |
| `BusTicketSaleSystem.Tests/AuthenticationTests.cs:17-97` | Lock sau 5 fail, correct bị reject trong lock; success reset; lock expiry; dummy hash cost unknown login | SkippableFact và có DB mutation trong test; không chạy lần khảo sát này |
| `BusTicketSaleSystem.Tests/NavigationLifetimeTests.cs:22-86` | Dispose outgoing, không dispose same/singleton, không tích lũy Home/Booking location subscribers | Không bao phủ mọi singleton subscriber của Login/Main |
| `BusTicketSaleSystem.Tests/DomainModelTests.cs:23-105` | Print model mapping và export file PDF/PNG | Test source có file side effect; implementation in/email chi tiết ở báo cáo khác |

Không có kết quả runtime/test được tạo bởi tài liệu này. Các test thanh toán, concurrency, expiry, check-in, refund/trip cancellation và schema được phân tích sâu trong báo cáo chuyên đề tương ứng, không suy từ tên test thành chứng minh đã pass.

## 17. Những điểm dễ bị hiểu sai khi dùng tài liệu làm đầu vào chuyển web

1. **Đừng mô tả Home search là backend filter đang chạy:** các field có giao diện nhưng command chỉ navigation; Booking bỏ lọc; lịch company mặc định Today, không phải ngày đã nhập Home. Chứng cứ: `HomeViewModel.cs:86-87`, `BookingViewModel.cs:60`, `BusCompanyDetailViewModel.cs:84` (đều dưới `BusTicketSaleSystem/ViewModels/`).
2. **Đừng coi bộ chọn 1–4 hành khách là giới hạn booking:** chọn nhiều ghế tự do, một passenger contact cho tất cả; definitive rule seat list empty/duplicate ở helper. Chứng cứ: `BusTicketSaleSystem/ViewModels/HomeViewModel.cs:45-46`, `BusTicketSaleSystem/ViewModels/SeatSelectionViewModel.cs:65-85`, `BusTicketSaleSystem/Data/DatabaseHelper.cs:716-720`.
3. **Đừng chuyển pickup/dropoff/rating review/hình ảnh thành tính năng đã tồn tại:** stop selection chỉ VM không exposed; review metadata/read-only và badge tĩnh; hình minh họa không file ảnh phương tiện. Chứng cứ: `BusTicketSaleSystem/ViewModels/BusCompanyDetailViewModel.cs:159-175`, `BusTicketSaleSystem/Views/BusCompanyDetailView.xaml:34-84`.
4. **Đừng coi toast TransferSubmitted hoặc Cash reserved là paid:** UI chuyển MyTickets pending, e-ticket chỉ mở paid; confirmation không settle. Chứng cứ: `BusTicketSaleSystem/ViewModels/BookingConfirmationViewModel.cs:148-165,189-200`, `BusTicketSaleSystem/ViewModels/MyTicketsViewModel.cs:280-285`.
5. **Đừng coi RequestRefund là đã trả tiền:** nó hủy vé và stamp request; payment trạng thái tiền chưa đổi ở method này. Chứng cứ: `BusTicketSaleSystem/Data/DatabaseHelper.cs:1018-1022`.
6. **Đừng coi tất cả danh sách là read-only:** GetBookingCompaniesAsync có sync INSERT lần đầu; startup cũng có migrate/ensure trips/background jobs. Vì thế khảo sát này không gọi app/helper runtime. Chứng cứ: `BusTicketSaleSystem/Data/DatabaseHelper.cs:437-473`, `BusTicketSaleSystem/App.xaml.cs:31-44`.
7. **Đừng lấy comment làm contract độc lập:** SessionService không writer duy nhất và không SignedOut-once guard; navigation không role guard; history “completed” còn tính past Reserved/Paid; company active count không lọc Scheduled. Chứng cứ nêu trong từng mục.
8. **Đừng tự sửa các gap trong khi trích xuất:** empty ShowAllTrips binding, dropped Home filters, missing pickup/dropoff exposure, timezone/validation khác biệt và fallback dữ liệu đều được ghi nhận như hiện trạng; chưa có chấp thuận thay đổi nghiệp vụ.
