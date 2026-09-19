# BusTicketSaleSystem - Project Logic

## 1. Kiến trúc tổng quan

Project đang sử dụng mô hình gần giống MVVM:

```text
XAML View
   ↓ Binding / Command
ViewModel
   ↓ gọi
Service / Data
   ↓
SQL Server
```

## 2. Các thư mục chính

### `Views/`

Chứa giao diện WPF và code-behind của giao diện:

- `LoginWindow.xaml`: màn hình đăng nhập.
- `SignUpWindow.xaml`: màn hình đăng ký tài khoản.
- `MainWindow.xaml`: khung chính sau khi đăng nhập.
- `HomeView.xaml`: Dashboard.
- `BookingView.xaml`: màn hình đặt vé.
- `ProfileView.xaml`: thông tin cá nhân.
- `HistoryView.xaml`: lịch sử đặt vé.
- `MyTicketsView.xaml`: vé đã đặt.

Các file `.xaml.cs` xử lý sự kiện giao diện, ví dụ click nút Login hoặc Sign Up.

### `ViewModels/`

Chứa logic cho từng màn hình:

- `MainViewModel`: điều khiển khung chính, sidebar, navigation và logout.
- `HomeViewModel`: dữ liệu và logic Dashboard.
- `ProfileViewModel`: dữ liệu và thao tác Profile.
- `BookingViewModel`: logic tìm kiếm và đặt vé.
- `HistoryViewModel`: logic lịch sử vé.
- `MyTicketsViewModel`: logic vé đã đặt.

Ví dụ binding trong XAML:

```xml
<TextBlock Text="{Binding UserName}"/>
```

`UserName` được lấy từ ViewModel hiện tại.

### `Data/`

Chứa phần kết nối database:

- `DatabaseHelper.cs`: mở kết nối SQL Server, đăng nhập và tạo tài khoản.
- `Schema1.sql`: tạo database và các bảng.
- `SeedData.sql`: thêm dữ liệu mẫu.

### `Models/`

Chứa các model dữ liệu:

- `UserSession.cs`: lưu thông tin tài khoản hiện tại sau khi đăng nhập.

### `Services/`

Chứa các service dùng chung:

- `NavigationService.cs`: chuyển đổi giữa các màn hình.
- `INavigationService.cs`: interface định nghĩa navigation.

## 3. Luồng đăng nhập

```text
LoginWindow
    ↓ người dùng click Sign In
LoginWindow.xaml.cs
    ↓ gọi
DatabaseHelper.AuthenticateAsync()
    ↓ truy vấn
SQL Server - Accounts
    ↓ trả về UserSession
CurrentUser.Account
    ↓
MainWindow + MainViewModel
    ↓
Dashboard/Profile hiển thị thông tin user
```

Chi tiết:

1. Người dùng nhập username/email và password.
2. `LoginButton_Click()` trong `LoginWindow.xaml.cs` được gọi.
3. Hàm này gọi `DatabaseHelper.AuthenticateAsync()`.
4. `DatabaseHelper` truy vấn bảng `Accounts`.
5. Nếu thông tin đúng, tài khoản được lưu vào `CurrentUser.Account`.
6. `MainWindow` được mở.
7. `MainViewModel` lấy username từ `CurrentUser.Account`.
8. Các binding trong XAML tự động hiển thị thông tin người dùng.

## 4. Hiển thị username hiện tại

Trong `MainViewModel`:

```csharp
public string UserName { get; set; }
    = CurrentUser.Account?.Username ?? "Guest";
```

Trong `MainWindow.xaml`:

```xml
<TextBlock Text="{Binding UserName}"/>
```

Nếu đăng nhập bằng `user02`, Dashboard và thanh tài khoản sẽ hiển thị `user02`.

`HomeViewModel` cũng đọc username từ `CurrentUser.Account` để hiển thị lời chào trên Dashboard.

## 5. Navigation giữa các màn hình

`MainWindow` là khung chính. Nội dung bên trong được hiển thị qua `ContentControl`:

```xml
<ContentControl Content="{Binding CurrentViewModel}"/>
```

Khi người dùng bấm menu Booking:

```csharp
_navigationService.NavigateTo<BookingViewModel>();
```

`NavigationService` thay đổi `CurrentViewModel`, sau đó WPF hiển thị View tương ứng:

```text
HomeViewModel       → HomeView
BookingViewModel    → BookingView
HistoryViewModel    → HistoryView
ProfileViewModel    → ProfileView
```

Việc tạo ViewModel được cấu hình trong `MainWindow.xaml.cs`.

## 6. Luồng đăng ký tài khoản

```text
SignUpWindow
    ↓
SignUpWindow.xaml.cs
    ↓
DatabaseHelper.CreateAccountAsync()
    ↓
INSERT INTO Accounts
    ↓
SQL Server
```

Tài khoản mới được lưu vào bảng `Accounts` với các thông tin:

- Username
- Password
- FullName
- Email
- Phone

Project cũng kiểm tra username bị trùng và yêu cầu password tối thiểu 6 ký tự.

## 7. Luồng Logout

Trong `MainViewModel`, lệnh Logout phát sinh event:

```csharp
RequestLogout?.Invoke(this, EventArgs.Empty);
```

`MainWindow.xaml.cs` xử lý event:

```csharp
CurrentUser.Account = null;

var loginWindow = new LoginWindow();
loginWindow.Show();
Close();
```

Session cũ bị xóa và người dùng quay lại màn hình đăng nhập.

## 8. Trạng thái logic hiện tại

### Đã kết nối 100% database thật (SQL Server)

Toàn bộ các màn hình và luồng nghiệp vụ hiện đã kết nối trực tiếp với SQL Server:

- **Đăng nhập & Đăng ký**: Kiểm tra tài khoản, mã hóa PBKDF2, kiểm tra trùng lặp email/username.
- **Trang chủ (Dashboard - HomeView)**:
  - Thống kê thực từ bảng `Tickets`: Tổng số vé đã đặt, chuyến đi sắp tới, tổng tiền đã chi.
  - Phân hạng thành viên (`RewardTier`) và điểm thưởng (`LoyaltyPoints`) tính động theo lịch sử đặt vé.
  - Tuyến đường phổ biến (`PopularRoutes`) truy vấn trực tiếp từ bảng `Routes` kết hợp giá sàn từ `Trips`.
  - Nút **Book** trên từng thẻ tuyến phổ biến và form **Search Trips** truyền trực tiếp tham số (Origin, Destination, Date) sang màn hình Đặt vé.
- **Tìm kiếm & Đặt vé (BookingView)**:
  - Tải danh sách điểm đi/điểm đến khả dụng từ SQL Server.
  - Tìm chuyến xe linh hoạt theo ngày khởi hành hoặc toàn bộ chuyến sắp tới.
  - Hiển thị đầy đủ thông tin: giờ đi, giờ đến, tuyến đường, loại xe, giá vé và số ghế còn trống thực tế.
  - Trạng thái rỗng (Empty state) hỗ trợ xem toàn bộ các chuyến khả dụng khi không tìm thấy kết quả.
- **Chọn ghế (SeatSelectionView)**:
  - Tải sơ đồ ghế của xe theo chuyến từ bảng `Seats`.
  - Cho phép chọn một hoặc nhiều ghế còn trống; ghế đã được đặt vẫn bị khóa.
  - Tự động giữ lại các ghế đang chọn khi sơ đồ được làm mới nếu chúng vẫn còn trống.
- **Xác nhận đặt vé & Xuất vé (BookingConfirmationView & TicketView)**:
  - Giao dịch đặt nhiều ghế dùng `IsolationLevel.Serializable`; toàn bộ các ghế trong một lần đặt được gom vào **1 vé duy nhất** với 1 mã vé `BT-YYYYMMDDHHmmss-XXXX`.
  - Danh sách các ghế đã chọn được lưu vào bảng trung gian `TicketSeats` (ví dụ: chỗ 1, 2, 5, 6).
  - Chỉ tạo duy nhất 1 giao dịch thanh toán (`Payments`) cho cả đơn hàng; tổng tiền = đơn giá × số ghế.
  - E-Ticket hiển thị đầy đủ danh sách ghế đã đặt ("ASSIGNED SEATS (N): 01, 02, 05, 06"), in/xuất PDF chỉ cần 1 vé duy nhất.
  - Admin chỉ cần duyệt 1 payment duy nhất cho cả gia đình/nhóm thay vì phải duyệt từng vé lẻ.
  - Nút đặt vé có guard `!IsBusy` ngăn chặn việc click đúp tạo đơn hàng trùng.
- **Vé của tôi (MyTicketsView)**:
  - Truy vấn các vé đang hoạt động (`Đã đặt`, `Đã thanh toán`) có thời gian khởi hành trong tương lai.
  - Với payment chưa được duyệt, vẫn hiển thị đầy đủ thẻ vé nhưng thay nút **View E-Ticket** bằng nút **Pending** không thao tác được.
  - Chỉ nút **View E-Ticket** của ticket đã thanh toán mới mở `TicketView`.
  - Hỗ trợ chức năng **Hủy vé** (`CancelTicketAsync`) cập nhật trạng thái `Đã hủy` và thời gian hủy `CancelledAt` trong database.
  - Empty state hướng dẫn người dùng khi chưa có vé hoạt động.
- **Lịch sử chuyến đi (HistoryView)**:
  - Tải các vé đã sử dụng hoặc đã hủy (`Đã sử dụng`, `Đã hủy`), hoặc các chuyến đã qua thời gian khởi hành.
  - Tính tổng số chuyến đã hoàn thành (`TotalCompletedTripsText`) và tổng số km đã đi (`TotalDistanceTraveledText`) từ database.
  - Nút **Book Again** tự động điền lại tuyến đường cũ để đặt chuyến mới nhanh chóng.
- **Hồ sơ cá nhân & Đổi mật khẩu (ProfileView)**:
  - Cập nhật thông tin cá nhân và thay đổi mật khẩu trực tiếp vào bảng `Accounts`.
- **Quản trị hệ thống (AdminWindow)**:
  - Quản lý Routes, Buses, Trips, Dashboard số liệu thống kê toàn hệ thống.

## 9. Các bảng database chính

```text
Accounts  : tài khoản người dùng và phân quyền (Customer / Admin)
Routes    : tuyến đường liên tỉnh (Origin, Destination, DistanceKm)
Buses     : thông tin đội xe (LicensePlate, SeatCount, BusType)
Trips     : chuyến xe cụ thể theo lịch trình và giá vé (RouteId, BusId, DepartureTime, ArrivalTime, Price)
Seats     : sơ đồ ghế ngồi của từng xe (BusId, SeatNumber)
Tickets   : vé đã xuất, liên kết hành khách, chuyến xe và số ghế ngồi (TicketCode, TripId, SeatId, AccountId, Status, Price)
```

