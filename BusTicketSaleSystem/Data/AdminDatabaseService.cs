using System.Data;
using BusTicketSaleSystem.Models;
using Microsoft.Data.SqlClient;

namespace BusTicketSaleSystem.Data;

public static class AdminDatabaseService
{
    private static void RequireAdmin()
    {
        if (!CurrentUser.IsAdmin)
            throw new UnauthorizedAccessException("Admin access required.");
    }

    public static async Task<AdminDashboardStats> GetDashboardStatsAsync()
    {
        RequireAdmin();
        const string sql = @"
SELECT
    (SELECT COUNT(*) FROM Routes),
    (SELECT COUNT(*) FROM Buses),
    (SELECT COUNT(*) FROM Trips WHERE DepartureTime >= SYSUTCDATETIME()),
    (SELECT COUNT(*) FROM Tickets WHERE Status IN (N'Đã đặt', N'Đã thanh toán')),
    (SELECT COALESCE(SUM(Price), 0) FROM Tickets WHERE Status IN (N'Đã đặt', N'Đã thanh toán')),
    (SELECT COUNT(*) FROM Accounts WHERE Role = N'Customer' OR Role IS NULL);";

        await using var connection = DatabaseHelper.GetConnection();
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return new AdminDashboardStats(0, 0, 0, 0, 0, 0);
        return new AdminDashboardStats(
            reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2),
            reader.GetInt32(3), reader.GetDecimal(4), reader.GetInt32(5));
    }

    public static async Task<IReadOnlyList<RecentBookingAdminItem>> GetRecentBookingsAsync(int top = 8)
    {
        RequireAdmin();
        const string sql = @"
SELECT TOP (@Top)
    tk.TicketId, tk.TicketCode, r.Origin + N' → ' + r.Destination,
    tk.PassengerName, tk.PassengerPhone, s.SeatNumber, tk.Price, tk.Status, tk.BookingTime
FROM Tickets tk
JOIN Trips tr ON tr.TripId = tk.TripId
JOIN Routes r ON r.RouteId = tr.RouteId
JOIN Seats s ON s.SeatId = tk.SeatId
ORDER BY tk.BookingTime DESC;";

        var list = new List<RecentBookingAdminItem>();
        await using var connection = DatabaseHelper.GetConnection();
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@Top", SqlDbType.Int).Value = Math.Clamp(top, 1, 100);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new RecentBookingAdminItem(
                reader.GetInt32(0), reader.GetString(1), reader.GetString(2),
                reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.GetString(5), reader.GetDecimal(6), reader.GetString(7), reader.GetDateTime(8)));
        }
        return list;
    }

    public static async Task<IReadOnlyList<TripAdminItem>> GetUpcomingTripsAsync(int top = 6)
    {
        RequireAdmin();
        const string sql = @"
SELECT TOP (@Top)
    t.TripId, t.RouteId, t.BusId, r.Origin + N' → ' + r.Destination,
    b.LicensePlate, t.DepartureTime, t.ArrivalTime, t.Price
FROM Trips t
JOIN Routes r ON r.RouteId = t.RouteId
JOIN Buses b ON b.BusId = t.BusId
ORDER BY CASE WHEN t.DepartureTime >= SYSUTCDATETIME() THEN 0 ELSE 1 END,
         t.DepartureTime ASC;";

        var list = new List<TripAdminItem>();
        await using var connection = DatabaseHelper.GetConnection();
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@Top", SqlDbType.Int).Value = Math.Clamp(top, 1, 100);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new TripAdminItem(
                reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2),
                reader.GetString(3), reader.GetString(4), reader.GetDateTime(5),
                reader.GetDateTime(6), reader.GetDecimal(7)));
        }
        return list;
    }

    public static async Task<IReadOnlyList<RouteAdminItem>> GetRoutesAsync()
    {
        RequireAdmin();
        const string sql = "SELECT RouteId, Origin, Destination, DistanceKm FROM Routes ORDER BY Origin, Destination;";
        var list = new List<RouteAdminItem>();
        await using var connection = DatabaseHelper.GetConnection();
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            list.Add(new RouteAdminItem(reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetDecimal(3)));
        return list;
    }

    public static async Task<(bool Success, string? Error)> AddRouteAsync(string origin, string destination, decimal? distance)
    {
        RequireAdmin();
        origin = origin.Trim();
        destination = destination.Trim();
        if (string.IsNullOrWhiteSpace(origin) || string.IsNullOrWhiteSpace(destination)) return (false, "Vui lòng nhập đầy đủ điểm đi và điểm đến.");
        if (string.Equals(origin, destination, StringComparison.OrdinalIgnoreCase)) return (false, "Điểm đi và điểm đến phải khác nhau.");
        if (distance is < 0) return (false, "Khoảng cách không được âm.");

        const string sql = @"
INSERT INTO Routes (Origin, Destination, DistanceKm)
SELECT @Origin, @Destination, @Distance
WHERE NOT EXISTS (SELECT 1 FROM Routes WHERE Origin = @Origin AND Destination = @Destination);";
        try
        {
            await using var connection = DatabaseHelper.GetConnection();
            await connection.OpenAsync();
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.Add("@Origin", SqlDbType.NVarChar, 100).Value = origin;
            command.Parameters.Add("@Destination", SqlDbType.NVarChar, 100).Value = destination;
            command.Parameters.Add("@Distance", SqlDbType.Decimal).Value = distance is null ? DBNull.Value : distance.Value;
            return await command.ExecuteNonQueryAsync() == 1 ? (true, null) : (false, "Tuyến này đã tồn tại.");
        }
        catch (SqlException) { return (false, "Không thể thêm tuyến đường."); }
    }

    public static async Task<(bool Success, string? Error)> DeleteRouteAsync(int routeId)
    {
        RequireAdmin();
        const string sql = "DELETE FROM Routes WHERE RouteId = @Id AND NOT EXISTS (SELECT 1 FROM Trips WHERE RouteId = @Id);";
        await using var connection = DatabaseHelper.GetConnection();
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@Id", SqlDbType.Int).Value = routeId;
        return await command.ExecuteNonQueryAsync() == 1 ? (true, null) : (false, "Không thể xóa tuyến đang được sử dụng.");
    }

    public static async Task<IReadOnlyList<BusAdminItem>> GetBusesAsync()
    {
        RequireAdmin();
        const string sql = @"
SELECT b.BusId, b.LicensePlate, b.SeatCount, COALESCE(b.BusType, N''),
       COUNT(s.SeatId), COUNT(DISTINCT CASE WHEN t.Status IN (N'Đã đặt', N'Đã thanh toán') THEN t.TicketId END)
FROM Buses b
LEFT JOIN Seats s ON s.BusId = b.BusId
LEFT JOIN Tickets t ON t.SeatId = s.SeatId
GROUP BY b.BusId, b.LicensePlate, b.SeatCount, b.BusType
ORDER BY b.LicensePlate;";
        var list = new List<BusAdminItem>();
        await using var connection = DatabaseHelper.GetConnection();
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) list.Add(new BusAdminItem(reader.GetInt32(0), reader.GetString(1), reader.GetInt32(2), reader.GetString(3), reader.GetInt32(5)));
        return list;
    }

    public static async Task<(bool Success, string? Error)> AddBusAsync(string plate, int seatCount, string busType)
    {
        RequireAdmin();
        plate = plate.Trim();
        busType = busType.Trim();
        if (string.IsNullOrWhiteSpace(plate) || string.IsNullOrWhiteSpace(busType)) return (false, "Vui lòng nhập biển số và loại xe.");
        if (seatCount is < 1 or > 200) return (false, "Số ghế phải từ 1 đến 200.");

        const string insertBus = "INSERT INTO Buses (LicensePlate, SeatCount, BusType) OUTPUT INSERTED.BusId VALUES (@Plate, @Count, @Type);";
        const string insertSeats = @"
;WITH Numbers AS
(
    SELECT 1 AS Number
    UNION ALL SELECT Number + 1 FROM Numbers WHERE Number < @Count
)
INSERT INTO Seats (BusId, SeatNumber)
SELECT @BusId, CONVERT(NVARCHAR(10), Number) FROM Numbers
OPTION (MAXRECURSION 0);";
        await using var connection = DatabaseHelper.GetConnection();
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        try
        {
            await using var busCommand = new SqlCommand(insertBus, connection, transaction);
            busCommand.Parameters.Add("@Plate", SqlDbType.NVarChar, 20).Value = plate;
            busCommand.Parameters.Add("@Count", SqlDbType.Int).Value = seatCount;
            busCommand.Parameters.Add("@Type", SqlDbType.NVarChar, 50).Value = busType;
            var busId = (int)(await busCommand.ExecuteScalarAsync())!;

            await using var seatCommand = new SqlCommand(insertSeats, connection, transaction);
            seatCommand.Parameters.Add("@BusId", SqlDbType.Int).Value = busId;
            seatCommand.Parameters.Add("@Count", SqlDbType.Int).Value = seatCount;
            await seatCommand.ExecuteNonQueryAsync();
            await transaction.CommitAsync();
            return (true, null);
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627) { await transaction.RollbackAsync(); return (false, "Biển số xe đã tồn tại."); }
        catch (SqlException) { await transaction.RollbackAsync(); return (false, "Không thể thêm xe."); }
    }

    public static async Task<(bool Success, string? Error)> DeleteBusAsync(int busId)
    {
        RequireAdmin();
        const string sql = "DELETE FROM Buses WHERE BusId = @Id AND NOT EXISTS (SELECT 1 FROM Trips WHERE BusId = @Id) AND NOT EXISTS (SELECT 1 FROM Tickets tk JOIN Seats s ON s.SeatId = tk.SeatId WHERE s.BusId = @Id);";
        await using var connection = DatabaseHelper.GetConnection();
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@Id", SqlDbType.Int).Value = busId;
        return await command.ExecuteNonQueryAsync() == 1 ? (true, null) : (false, "Không thể xóa xe đang được sử dụng.");
    }

    public static async Task<IReadOnlyList<TripAdminItem>> GetTripsAsync()
    {
        RequireAdmin();
        const string sql = @"
SELECT t.TripId, t.RouteId, t.BusId, r.Origin + N' → ' + r.Destination,
       b.LicensePlate, t.DepartureTime, t.ArrivalTime, t.Price
FROM Trips t JOIN Routes r ON r.RouteId = t.RouteId JOIN Buses b ON b.BusId = t.BusId
ORDER BY t.DepartureTime DESC;";
        var list = new List<TripAdminItem>();
        await using var connection = DatabaseHelper.GetConnection();
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) list.Add(new TripAdminItem(reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetString(3), reader.GetString(4), reader.GetDateTime(5), reader.GetDateTime(6), reader.GetDecimal(7)));
        return list;
    }

    public static async Task<(bool Success, string? Error)> AddTripAsync(int routeId, int busId, DateTime departure, DateTime arrival, decimal price)
    {
        RequireAdmin();
        if (arrival <= departure) return (false, "Thời gian đến phải sau thời gian khởi hành.");
        if (price < 0) return (false, "Giá vé không được âm.");
        const string sql = @"
INSERT INTO Trips (RouteId, BusId, DepartureTime, ArrivalTime, Price)
SELECT @Route, @Bus, @Departure, @Arrival, @Price
WHERE EXISTS (SELECT 1 FROM Routes WHERE RouteId = @Route)
  AND EXISTS (SELECT 1 FROM Buses WHERE BusId = @Bus)
  AND NOT EXISTS (SELECT 1 FROM Trips WHERE BusId = @Bus AND DepartureTime < @Arrival AND ArrivalTime > @Departure);";
        await using var connection = DatabaseHelper.GetConnection();
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@Route", SqlDbType.Int).Value = routeId;
        command.Parameters.Add("@Bus", SqlDbType.Int).Value = busId;
        command.Parameters.Add("@Departure", SqlDbType.DateTime2).Value = departure;
        command.Parameters.Add("@Arrival", SqlDbType.DateTime2).Value = arrival;
        var amount = command.Parameters.Add("@Price", SqlDbType.Decimal);
        amount.Precision = 10;
        amount.Scale = 0;
        amount.Value = price;
        return await command.ExecuteNonQueryAsync() == 1 ? (true, null) : (false, "Xe đã có chuyến bị trùng thời gian hoặc dữ liệu không hợp lệ.");
    }

    public static async Task<(bool Success, string? Error)> DeleteTripAsync(int tripId)
    {
        RequireAdmin();
        const string sql = "DELETE FROM Trips WHERE TripId = @Id AND NOT EXISTS (SELECT 1 FROM Tickets WHERE TripId = @Id);";
        await using var connection = DatabaseHelper.GetConnection();
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@Id", SqlDbType.Int).Value = tripId;
        return await command.ExecuteNonQueryAsync() == 1 ? (true, null) : (false, "Không thể xóa chuyến đã có vé.");
    }

    public static async Task<(bool Success, string? Error)> UpdateRouteDistanceAsync(int routeId, decimal? distance)
    {
        RequireAdmin();
        if (distance is < 0) return (false, "Khoảng cách không được âm.");
        const string sql = "UPDATE Routes SET DistanceKm = @Distance WHERE RouteId = @Id;";
        await using var connection = DatabaseHelper.GetConnection();
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@Id", SqlDbType.Int).Value = routeId;
        command.Parameters.Add("@Distance", SqlDbType.Decimal).Value = (object?)distance ?? DBNull.Value;
        return await command.ExecuteNonQueryAsync() == 1 ? (true, null) : (false, "Không tìm thấy tuyến.");
    }

    public static async Task<(bool Success, string? Error)> UpdateTripAsync(int tripId, DateTime departure, DateTime arrival, decimal price)
    {
        RequireAdmin();
        if (arrival <= departure) return (false, "Thời gian đến phải sau thời gian khởi hành.");
        if (price < 0) return (false, "Giá vé không được âm.");
        const string sql = @"
UPDATE Trips SET DepartureTime = @Departure, ArrivalTime = @Arrival, Price = @Price
WHERE TripId = @TripId
  AND NOT EXISTS (
    SELECT 1 FROM Trips x
    WHERE x.BusId = Trips.BusId AND x.TripId <> @TripId
      AND x.DepartureTime < @Arrival AND x.ArrivalTime > @Departure
  );";
        await using var connection = DatabaseHelper.GetConnection();
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@TripId", SqlDbType.Int).Value = tripId;
        command.Parameters.Add("@Departure", SqlDbType.DateTime2).Value = departure;
        command.Parameters.Add("@Arrival", SqlDbType.DateTime2).Value = arrival;
        var pPrice = command.Parameters.Add("@Price", SqlDbType.Decimal);
        pPrice.Precision = 10; pPrice.Scale = 0; pPrice.Value = price;
        return await command.ExecuteNonQueryAsync() == 1 ? (true, null) : (false, "Xe đã có chuyến khác bị trùng thời gian hoặc dữ liệu không hợp lệ.");
    }

    public static async Task<IReadOnlyList<AccountAdminItem>> GetAccountsAsync()
    {
        RequireAdmin();
        const string sql = "SELECT AccountId, Username, FullName, Email, Phone, Role, CreatedAt FROM Accounts ORDER BY CreatedAt DESC;";
        var list = new List<AccountAdminItem>();
        await using var connection = DatabaseHelper.GetConnection();
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new AccountAdminItem(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.GetString(5),
                reader.GetDateTime(6)));
        }
        return list;
    }

    public static async Task<(bool Success, string? Error)> UpdateAccountRoleAsync(int accountId, string newRole)
    {
        RequireAdmin();
        if (newRole is not ("Customer" or "Admin")) return (false, "Vai trò không hợp lệ.");
        if (accountId == CurrentUser.Account?.AccountId && newRole == "Customer")
            return (false, "Không thể tự hạ quyền của chính mình.");
        const string sql = "UPDATE Accounts SET Role = @Role WHERE AccountId = @Id;";
        await using var connection = DatabaseHelper.GetConnection();
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@Id", SqlDbType.Int).Value = accountId;
        command.Parameters.Add("@Role", SqlDbType.NVarChar, 20).Value = newRole;
        return await command.ExecuteNonQueryAsync() == 1 ? (true, null) : (false, "Không thể cập nhật quyền tài khoản.");
    }
}
