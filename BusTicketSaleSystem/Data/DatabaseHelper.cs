using BusTicketSaleSystem.Models;
using BusTicketSaleSystem.Services;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace BusTicketSaleSystem.Data;

public static class DatabaseHelper
{
    private const int PasswordIterations = 100_000;

    public static SqlConnection GetConnection() => new(DatabaseOptions.ConnectionString);

    public static bool TestConnection()
    {
        try { using var connection = GetConnection(); connection.Open(); return true; }
        catch (SqlException) { return false; }
    }

    public static async Task<(bool Success, UserSession? Account, string? Error)> AuthenticateAsync(string usernameOrEmail, string password)
    {
        const string sql = "SELECT TOP (1) AccountId, Username, FullName, Email, Phone, Role, PasswordHash FROM Accounts WHERE Username=@Login OR Email=@Login;";
        try
        {
            await using var connection = GetConnection(); await connection.OpenAsync();
            await using var command = new SqlCommand(sql, connection); command.Parameters.Add("@Login", SqlDbType.NVarChar, 100).Value = usernameOrEmail;
            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return (false, null, "Tên đăng nhập hoặc mật khẩu không đúng.");
            var accountId = reader.GetInt32(0); var username = reader.GetString(1); var fullName = reader.GetString(2);
            var email = reader.IsDBNull(3) ? null : reader.GetString(3); var phone = reader.IsDBNull(4) ? null : reader.GetString(4);
            var role = reader.IsDBNull(5) ? "Customer" : reader.GetString(5); var storedHash = reader.GetString(6);
            if (!VerifyPassword(password, storedHash)) return (false, null, "Tên đăng nhập hoặc mật khẩu không đúng.");
            return (true, new UserSession(accountId, username, fullName, email, phone, role), null);
        }
        catch (SqlException) { return (false, null, "Không thể kết nối tới cơ sở dữ liệu."); }
    }

    public static async Task<(bool Success, string? Error)> CreateAccountAsync(string username, string password, string fullName, string? phone, string? email)
    {
        if (!ValidateAccountInput(username, password, fullName, phone, email, out var validationError))
            return (false, validationError);
        const string sql = "INSERT INTO Accounts (Username, PasswordHash, FullName, Phone, Email) VALUES (@Username,@PasswordHash,@FullName,@Phone,@Email);";
        try
        {
            await using var connection = GetConnection(); await connection.OpenAsync(); await using var command = new SqlCommand(sql, connection);
            command.Parameters.Add("@Username", SqlDbType.NVarChar, 50).Value = username; command.Parameters.Add("@PasswordHash", SqlDbType.NVarChar, 255).Value = HashPassword(password);
            command.Parameters.Add("@FullName", SqlDbType.NVarChar, 100).Value = fullName; command.Parameters.Add("@Phone", SqlDbType.NVarChar, 20).Value = (object?)phone ?? DBNull.Value; command.Parameters.Add("@Email", SqlDbType.NVarChar, 100).Value = (object?)email ?? DBNull.Value;
            await command.ExecuteNonQueryAsync(); return (true, null);
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627) { return (false, "Username hoặc email này đã tồn tại."); }
        catch (SqlException) { return (false, "Không thể tạo tài khoản."); }
    }
    public static async Task<(bool Success, UserSession? Account, string? Error)> UpdateProfileAsync(UserSession account, string fullName, string? email, string? phone)
    {
        var validationError = string.Empty;
        if (string.IsNullOrWhiteSpace(fullName) || fullName.Length > 100 || !ValidateContact(email, phone, out validationError))
            return (false, null, validationError);
        const string sql = "UPDATE Accounts SET FullName=@FullName, Email=@Email, Phone=@Phone WHERE AccountId=@AccountId;";
        try
        {
            await using var connection = GetConnection(); await connection.OpenAsync(); await using var command = new SqlCommand(sql, connection);
            command.Parameters.Add("@FullName", SqlDbType.NVarChar, 100).Value = fullName; command.Parameters.Add("@Email", SqlDbType.NVarChar, 100).Value = (object?)email ?? DBNull.Value; command.Parameters.Add("@Phone", SqlDbType.NVarChar, 20).Value = (object?)phone ?? DBNull.Value; command.Parameters.Add("@AccountId", SqlDbType.Int).Value = account.AccountId;
            await command.ExecuteNonQueryAsync(); return (true, account with { FullName = fullName, Email = email, Phone = phone }, null);
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627) { return (false, null, "Email này đã được sử dụng."); }
        catch (SqlException) { return (false, null, "Không thể cập nhật thông tin tài khoản."); }
    }

    public static async Task<(bool Success, string? Error)> ChangePasswordAsync(int accountId, string currentPassword, string newPassword)
    {
        if (!ValidatePassword(newPassword, out var validationError))
            return (false, validationError);
        const string select = "SELECT PasswordHash FROM Accounts WHERE AccountId=@AccountId;"; const string update = "UPDATE Accounts SET PasswordHash=@PasswordHash WHERE AccountId=@AccountId;";
        try
        {
            await using var connection = GetConnection(); await connection.OpenAsync(); await using var selectCommand = new SqlCommand(select, connection); selectCommand.Parameters.Add("@AccountId", SqlDbType.Int).Value = accountId;
            var stored = await selectCommand.ExecuteScalarAsync() as string; if (stored is null || !VerifyPassword(currentPassword, stored)) return (false, "Mật khẩu hiện tại không đúng.");
            await using var updateCommand = new SqlCommand(update, connection); updateCommand.Parameters.Add("@PasswordHash", SqlDbType.NVarChar, 255).Value = HashPassword(newPassword); updateCommand.Parameters.Add("@AccountId", SqlDbType.Int).Value = accountId; await updateCommand.ExecuteNonQueryAsync(); return (true, null);
        }
        catch (SqlException) { return (false, "Không thể đổi mật khẩu."); }
    }

    public static async Task<(int TotalTickets, int UpcomingTrips, decimal TotalSpent)> GetDashboardStatsAsync(int accountId)
    {
        const string sql = "SELECT COUNT(*), COALESCE(SUM(CASE WHEN Status IN (N'Đã đặt',N'Đã thanh toán') THEN 1 ELSE 0 END),0), COALESCE(SUM(CASE WHEN Status IN (N'Đã đặt',N'Đã thanh toán') THEN Price ELSE 0 END),0) FROM Tickets WHERE AccountId=@AccountId;";
        await using var connection = GetConnection(); await connection.OpenAsync(); await using var command = new SqlCommand(sql, connection); command.Parameters.Add("@AccountId", SqlDbType.Int).Value = accountId;
        await using var reader = await command.ExecuteReaderAsync(); await reader.ReadAsync(); return (reader.GetInt32(0), reader.GetInt32(1), reader.GetDecimal(2));
    }

    public static async Task<IReadOnlyList<PopularRouteItem>> GetPopularRoutesAsync(int top = 4)
    {
        const string sql = @"
SELECT TOP (@Top)
    r.RouteId,
    r.Origin,
    r.Destination,
    r.DistanceKm,
    COALESCE(MIN(t.Price), 120000) AS MinPrice
FROM Routes r
LEFT JOIN Trips t ON t.RouteId = r.RouteId
GROUP BY r.RouteId, r.Origin, r.Destination, r.DistanceKm
ORDER BY COUNT(t.TripId) DESC, r.RouteId;";

        var list = new List<PopularRouteItem>();
        try
        {
            await using var connection = GetConnection();
            await connection.OpenAsync();
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.Add("@Top", SqlDbType.Int).Value = top;
            await using var reader = await command.ExecuteReaderAsync();
            int idx = 1;
            while (await reader.ReadAsync())
            {
                var routeId = reader.GetInt32(0);
                var origin = reader.GetString(1);
                var destination = reader.GetString(2);
                var distance = reader.IsDBNull(3) ? 100m : reader.GetDecimal(3);
                var minPrice = reader.GetDecimal(4);
                var hours = (int)(distance / 50m);
                var minutes = (int)(((distance / 50m) - hours) * 60m);
                var duration = hours > 0 ? $"{hours}h {minutes}m" : $"{minutes}m";
                var tag = idx switch { 1 => "Most Popular", 2 => "Express Daily", 3 => "Direct Route", _ => "Featured" };
                list.Add(new PopularRouteItem(routeId, origin, destination, duration, $"{minPrice:N0} VND", tag, minPrice));
                idx++;
            }
        }
        catch { }
        return list;
    }

    public static async Task<(IReadOnlyList<string> Origins, IReadOnlyList<string> Destinations)> GetAvailableLocationsAsync()
    {
        var origins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var destinations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            await using var connection = GetConnection();
            await connection.OpenAsync();
            await using var command = new SqlCommand("SELECT DISTINCT Origin, Destination FROM Routes ORDER BY Origin, Destination;", connection);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                origins.Add(reader.GetString(0));
                destinations.Add(reader.GetString(1));
            }
        }
        catch { }
        return (origins.ToList(), destinations.ToList());
    }
    public static async Task<IReadOnlyList<string>> GetDistinctOriginsAsync()
    {
        const string sql = "SELECT DISTINCT Origin FROM Routes ORDER BY Origin;";
        return await GetLocationListAsync(sql);
    }

    public static async Task<IReadOnlyList<string>> GetDestinationsByOriginAsync(string? origin)
    {
        const string filteredSql = "SELECT DISTINCT Destination FROM Routes WHERE Origin = @Origin ORDER BY Destination;";
        const string allSql = "SELECT DISTINCT Destination FROM Routes ORDER BY Destination;";
        await using var connection = GetConnection();
        await connection.OpenAsync();
        await using var command = new SqlCommand(string.IsNullOrWhiteSpace(origin) ? allSql : filteredSql, connection);
        if (!string.IsNullOrWhiteSpace(origin))
            command.Parameters.Add("@Origin", SqlDbType.NVarChar, 100).Value = origin.Trim();
        var list = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            list.Add(reader.GetString(0));
        return list;
    }

    private static async Task<IReadOnlyList<string>> GetLocationListAsync(string sql)
    {
        await using var connection = GetConnection();
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        var list = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            list.Add(reader.GetString(0));
        return list;
    }

    public static async Task<IReadOnlyList<TripSearchResult>> SearchTripsAsync(string? origin, string? destination, DateTime? date)
    {
        var originFilter = string.IsNullOrWhiteSpace(origin) ? "%" : $"%{origin.Trim()}%";
        var destFilter = string.IsNullOrWhiteSpace(destination) ? "%" : $"%{destination.Trim()}%";
        var dateClause = date.HasValue ? "AND CAST(t.DepartureTime AS date)=@Date" : "AND t.DepartureTime >= SYSUTCDATETIME()";
        var sql = $@"SELECT t.TripId,r.Origin,r.Destination,b.BusType,t.DepartureTime,t.ArrivalTime,t.Price,
                       b.SeatCount-COUNT(tk.TicketId)
                FROM Trips t
                JOIN Routes r ON r.RouteId=t.RouteId
                JOIN Buses b ON b.BusId=t.BusId
                LEFT JOIN Seats s ON s.BusId=b.BusId
                LEFT JOIN Tickets tk ON tk.TripId=t.TripId AND tk.SeatId=s.SeatId
                    AND tk.Status IN (N'Đã đặt',N'Đã thanh toán')
                    AND (tk.PaymentStatus = N'Đã thanh toán' OR tk.PaymentExpiresAt > SYSUTCDATETIME())
                WHERE r.Origin LIKE @Origin AND r.Destination LIKE @Destination {dateClause}
                GROUP BY t.TripId,r.Origin,r.Destination,b.BusType,b.SeatCount,t.DepartureTime,t.ArrivalTime,t.Price
                ORDER BY t.DepartureTime;";
        var results = new List<TripSearchResult>();
        try
        {
            await using var connection = GetConnection();
            await connection.OpenAsync();
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.Add("@Origin", SqlDbType.NVarChar, 100).Value = originFilter;
            command.Parameters.Add("@Destination", SqlDbType.NVarChar, 100).Value = destFilter;
            if (date.HasValue) command.Parameters.Add("@Date", SqlDbType.Date).Value = date.Value.Date;
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                results.Add(new TripSearchResult(reader.GetInt32(0), reader.GetString(1), reader.GetString(2),
                    reader.IsDBNull(3) ? "Ghế ngồi" : reader.GetString(3), reader.GetDateTime(4),
                    reader.GetDateTime(5), reader.GetDecimal(6), reader.GetInt32(7)));
        }
        catch (SqlException ex)
        {
            LoggerService.LogError("Could not search trips.", ex);
        }
        return results;
    }

    public static async Task<IReadOnlyList<SeatInfo>> GetSeatsAsync(int tripId)
    {
        const string sql = "SELECT s.SeatId,s.BusId,s.SeatNumber,CONVERT(bit,CASE WHEN EXISTS(SELECT 1 FROM Tickets t WHERE t.TripId=@TripId AND t.SeatId=s.SeatId AND t.Status IN(N'Đã đặt',N'Đã thanh toán') AND (t.PaymentStatus=N'Đã thanh toán' OR t.PaymentExpiresAt>SYSUTCDATETIME())) THEN 1 ELSE 0 END) FROM Seats s JOIN Trips tr ON tr.BusId=s.BusId WHERE tr.TripId=@TripId ORDER BY s.SeatId;";
        var seats = new List<SeatInfo>(); await using var connection = GetConnection(); await connection.OpenAsync(); await using var command = new SqlCommand(sql, connection); command.Parameters.Add("@TripId", SqlDbType.Int).Value = tripId; await using var reader = await command.ExecuteReaderAsync(); while (await reader.ReadAsync()) seats.Add(new SeatInfo(reader.GetInt32(0),reader.GetInt32(1),reader.GetString(2),reader.GetBoolean(3))); return seats;
    }

    public static async Task<(bool Success, TicketDetails? Ticket, string? Error)> BookTicketAsync(int accountId,int tripId,int seatId,string passengerName,string? passengerPhone)
    {
        await Services.PaymentService.ExpirePendingPaymentsAsync();
        const string insert = "INSERT INTO Tickets(TicketCode,TripId,SeatId,AccountId,PassengerName,PassengerPhone,Price,Status,PaymentExpiresAt) SELECT @Code,tr.TripId,@SeatId,@AccountId,@Name,@Phone,tr.Price,N'Đã đặt',DATEADD(MINUTE,15,SYSUTCDATETIME()) FROM Trips tr JOIN Seats s ON s.BusId=tr.BusId WHERE tr.TripId=@TripId AND s.SeatId=@SeatId AND NOT EXISTS(SELECT 1 FROM Tickets x WHERE x.TripId=@TripId AND x.SeatId=@SeatId AND x.Status IN(N'Đã đặt',N'Đã thanh toán') AND (x.PaymentStatus=N'Đã thanh toán' OR x.PaymentExpiresAt>SYSUTCDATETIME()));";
        const string select = "SELECT TOP 1 tk.TicketId,tk.TicketCode,r.Origin+N' → '+r.Destination,tr.DepartureTime,tr.ArrivalTime,b.BusType,s.SeatNumber,tk.Price,tk.Status,tk.BookingTime,r.Origin,r.Destination,COALESCE(r.DistanceKm,0),tk.PassengerName,tk.PassengerPhone,b.LicensePlate FROM Tickets tk JOIN Trips tr ON tr.TripId=tk.TripId JOIN Routes r ON r.RouteId=tr.RouteId JOIN Buses b ON b.BusId=tr.BusId JOIN Seats s ON s.SeatId=tk.SeatId WHERE tk.TicketCode=@Code;";
        await using var connection = GetConnection(); await connection.OpenAsync(); await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var code=$"BT-{DateTime.UtcNow:yyyyMMddHHmmss}-{RandomNumberGenerator.GetInt32(1000,9999)}";
            await using var insertCommand=new SqlCommand(insert,connection,(SqlTransaction)transaction);
            insertCommand.Parameters.Add("@Code",SqlDbType.NVarChar,30).Value=code;
            insertCommand.Parameters.Add("@TripId",SqlDbType.Int).Value=tripId;
            insertCommand.Parameters.Add("@SeatId",SqlDbType.Int).Value=seatId;
            insertCommand.Parameters.Add("@AccountId",SqlDbType.Int).Value=accountId;
            insertCommand.Parameters.Add("@Name",SqlDbType.NVarChar,100).Value=passengerName;
            insertCommand.Parameters.Add("@Phone",SqlDbType.NVarChar,20).Value=(object?)passengerPhone??DBNull.Value;
            if(await insertCommand.ExecuteNonQueryAsync()==0)
            {
                return(false,null,"Ghế vừa được người khác đặt hoặc không hợp lệ.");
            }
            await using var selectCommand=new SqlCommand(select,connection,(SqlTransaction)transaction);
            selectCommand.Parameters.Add("@Code",SqlDbType.NVarChar,30).Value=code;
            await using var reader=await selectCommand.ExecuteReaderAsync();
            await reader.ReadAsync();
            var ticket=new TicketDetails(reader.GetInt32(0),reader.GetString(1),reader.GetString(2),reader.GetDateTime(3),reader.GetDateTime(4),reader.IsDBNull(5)?"":reader.GetString(5),reader.GetString(6),reader.GetDecimal(7),reader.GetString(8),reader.GetDateTime(9),reader.GetString(10),reader.GetString(11),reader.GetDecimal(12)) { PassengerName=reader.GetString(13), PassengerPhone=reader.IsDBNull(14)?null:reader.GetString(14), LicensePlate=reader.GetString(15) };
            await transaction.CommitAsync();
            return(true,ticket,null);
        }
        catch(SqlException)
        {
            await transaction.RollbackAsync();
            return(false,null,"Không thể đặt vé. Vui lòng thử lại.");
        }
    }

    public static async Task<(bool Success, string? Error)> CancelTicketAsync(int ticketId, int accountId)
    {
        const string sql = "UPDATE Tickets SET Status = N'Đã hủy', CancelledAt = SYSUTCDATETIME() WHERE TicketId = @TicketId AND AccountId = @AccountId AND Status IN (N'Đã đặt', N'Đã thanh toán'); UPDATE Payments SET RefundRequestedAt=SYSUTCDATETIME() WHERE TicketId=@TicketId AND Status=N'Success' AND RefundRequestedAt IS NULL;";
        try
        {
            await using var connection = GetConnection();
            await connection.OpenAsync();
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.Add("@TicketId", SqlDbType.Int).Value = ticketId;
            command.Parameters.Add("@AccountId", SqlDbType.Int).Value = accountId;
            int affected = await command.ExecuteNonQueryAsync();
            return affected > 0 ? (true, null) : (false, "Vé không thể hủy hoặc không tồn tại.");
        }
        catch (SqlException)
        {
            return (false, "Không thể hủy vé vào thời điểm này.");
        }
    }

    public static async Task<IReadOnlyList<TicketDetails>> GetTicketsAsync(int accountId, bool history)
    {
        const string sql = @"
SELECT tk.TicketId,
       tk.TicketCode,
       r.Origin + N' → ' + r.Destination,
       tr.DepartureTime,
       tr.ArrivalTime,
       b.BusType,
       s.SeatNumber,
       tk.Price,
       tk.Status,
       tk.BookingTime,
       r.Origin,
       r.Destination,
       COALESCE(r.DistanceKm, 0),
       tk.PassengerName,
       tk.PassengerPhone,
       b.LicensePlate
FROM Tickets tk
JOIN Trips tr ON tr.TripId = tk.TripId
JOIN Routes r ON r.RouteId = tr.RouteId
JOIN Buses b ON b.BusId = tr.BusId
JOIN Seats s ON s.SeatId = tk.SeatId
WHERE tk.AccountId = @AccountId
  AND (
    (@History = 1 AND (tk.Status IN (N'Đã sử dụng', N'Đã hủy') OR (tk.Status IN (N'Đã đặt', N'Đã thanh toán') AND tr.DepartureTime < SYSUTCDATETIME())))
    OR
    (@History = 0 AND tk.Status IN (N'Đã đặt', N'Đã thanh toán') AND tr.DepartureTime >= SYSUTCDATETIME())
  )
ORDER BY tr.DepartureTime DESC;";

        var list = new List<TicketDetails>();
        try
        {
            await using var connection = GetConnection();
            await connection.OpenAsync();
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.Add("@AccountId", SqlDbType.Int).Value = accountId;
            command.Parameters.Add("@History", SqlDbType.Bit).Value = history;
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new TicketDetails(
                    reader.GetInt32(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetDateTime(3),
                    reader.GetDateTime(4),
                    reader.IsDBNull(5) ? "" : reader.GetString(5),
                    reader.GetString(6),
                    reader.GetDecimal(7),
                    reader.GetString(8),
                    reader.GetDateTime(9),
                    reader.GetString(10),
                    reader.GetString(11),
                    reader.GetDecimal(12))
                {
                    PassengerName = reader.GetString(13),
                    PassengerPhone = reader.IsDBNull(14) ? null : reader.GetString(14),
                    LicensePlate = reader.GetString(15)
                });
            }
        }
        catch { }
        return list;
    }

    public static async Task<(int TotalCompleted, decimal TotalDistanceKm)> GetHistoryStatsAsync(int accountId)
    {
        const string sql = @"
SELECT COUNT(*),
       COALESCE(SUM(r.DistanceKm), 0)
FROM Tickets tk
JOIN Trips tr ON tr.TripId = tk.TripId
JOIN Routes r ON r.RouteId = tr.RouteId
WHERE tk.AccountId = @AccountId
  AND (tk.Status = N'Đã sử dụng' OR (tk.Status IN (N'Đã đặt', N'Đã thanh toán') AND tr.DepartureTime < SYSUTCDATETIME()));";

        try
        {
            await using var connection = GetConnection();
            await connection.OpenAsync();
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.Add("@AccountId", SqlDbType.Int).Value = accountId;
            await using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return (reader.GetInt32(0), reader.GetDecimal(1));
            }
        }
        catch { }
        return (0, 0m);
    }

    internal static string HashPassword(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, PasswordIterations, HashAlgorithmName.SHA256, 32);
        return $"PBKDF2${PasswordIterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    internal static bool VerifyPassword(string password, string? stored)
    {
        if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(stored) ||
            !stored.StartsWith("PBKDF2$", StringComparison.Ordinal))
            return false;

        var parts = stored.Split('$');
        if (parts.Length != 4 || !int.TryParse(parts[1], out var iterations) || iterations <= 0)
            return false;
        try
        {
            var expected = Convert.FromBase64String(parts[3]);
            var salt = Convert.FromBase64String(parts[2]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }
    private static bool ValidateAccountInput(string username, string password, string fullName, string? phone, string? email, out string error)
    {
        if (string.IsNullOrWhiteSpace(username) || username.Length > 50) { error = "Tên đăng nhập không hợp lệ."; return false; }
        if (string.IsNullOrWhiteSpace(fullName) || fullName.Length > 100) { error = "Họ tên không hợp lệ."; return false; }
        if (!ValidatePassword(password, out error)) return false;
        return ValidateContact(email, phone, out error);
    }

    private static bool ValidatePassword(string password, out string error)
    {
        if (password.Length < 8 || password.Length > 128 || !password.Any(char.IsUpper) || !password.Any(char.IsLower) || !password.Any(char.IsDigit))
        { error = "Mật khẩu phải dài 8-128 ký tự, có chữ hoa, chữ thường và chữ số."; return false; }
        error = string.Empty; return true;
    }

    private static bool ValidateContact(string? email, string? phone, out string error)
    {
        if (!string.IsNullOrWhiteSpace(email) && !Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
        { error = "Email không hợp lệ."; return false; }
        if (!string.IsNullOrWhiteSpace(phone) && !Regex.IsMatch(phone, @"^\+?[0-9\s-]{8,20}$"))
        { error = "Số điện thoại không hợp lệ."; return false; }
        error = string.Empty; return true;
    }
}
