using System.Collections.Specialized;
using System.Data;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using BusTicketSaleSystem.Data;
using Microsoft.Data.SqlClient;

namespace BusTicketSaleSystem.Services;

public sealed record PaymentRequest(int TicketId, string TicketCode, string PaymentMethod = "VNPay");
public sealed record PaymentResult(bool IsSuccess, int PaymentId = 0, string? TransactionNo = null, string? PaymentUrl = null, string? ErrorMessage = null);
public sealed record PaymentVerificationResult(bool IsSuccess, bool AlreadyProcessed, string? ErrorMessage = null);

public static class PaymentService
{
    private static readonly Uri VnPayEndpoint = new("https://sandbox.vnpayment.vn/paymentv2/vpcpay.html");

    public static async Task<PaymentResult> CreatePaymentAsync(PaymentRequest request)
    {
        if (request.TicketId <= 0 || string.IsNullOrWhiteSpace(request.TicketCode) || string.IsNullOrWhiteSpace(request.PaymentMethod))
            return new(false, ErrorMessage: "Thông tin thanh toán không hợp lệ.");

        await ExpirePendingPaymentsAsync();

        var normalizedMethod = request.PaymentMethod switch
        {
            var m when m.Contains("Cash", StringComparison.OrdinalIgnoreCase) || m.Contains("Tiền mặt", StringComparison.OrdinalIgnoreCase) => "Cash",
            var m when m.Contains("Bank", StringComparison.OrdinalIgnoreCase) || m.Contains("VietQR", StringComparison.OrdinalIgnoreCase) => "BankTransfer",
            var m when m.Contains("MoMo", StringComparison.OrdinalIgnoreCase) => "MoMo",
            _ => "VNPay"
        };

        if (normalizedMethod == "Cash")
        {
            const string cashSql = @"
DECLARE @PaymentId int;
DECLARE @Amount decimal(18,2);
DECLARE @TransactionNo nvarchar(100) = @TransactionNoInput;
SELECT @Amount=t.Price FROM Tickets t WITH (UPDLOCK,HOLDLOCK)
WHERE t.TicketId=@TicketId AND t.TicketCode=@TicketCode AND t.Status=N'Đã đặt';
IF @Amount IS NULL THROW 51010, 'Ticket is not payable.', 1;
IF EXISTS (SELECT 1 FROM Payments WHERE TicketId=@TicketId AND Status IN (N'Pending',N'Success'))
    THROW 51011, 'Payment already exists.', 1;
INSERT INTO Payments(TicketId,TransactionNo,Provider,ProviderTransactionNo,OrderInfo,PaymentMethod,Amount,Status)
VALUES(@TicketId,@TransactionNo,N'Cash',NULL,@OrderInfo,N'Cash',@Amount,N'Pending');
SELECT CAST(SCOPE_IDENTITY() AS int), @Amount;";
            try
            {
                var transactionNo = $"CASH{DateTime.UtcNow:yyyyMMddHHmmssfff}{RandomNumberGenerator.GetInt32(1000, 9999)}";
                await using var connection = DatabaseHelper.GetConnection();
                await connection.OpenAsync();
                await using var command = new SqlCommand(cashSql, connection);
                command.Parameters.Add("@TicketId", SqlDbType.Int).Value = request.TicketId;
                command.Parameters.Add("@TicketCode", SqlDbType.NVarChar, 30).Value = request.TicketCode;
                command.Parameters.Add("@TransactionNoInput", SqlDbType.NVarChar, 100).Value = transactionNo;
                command.Parameters.Add("@OrderInfo", SqlDbType.NVarChar, 255).Value = $"Thanh toan tien mat ve {request.TicketCode}";
                await using var reader = await command.ExecuteReaderAsync();
                if (!await reader.ReadAsync()) return new(false, ErrorMessage: "Không thể tạo giao dịch tiền mặt.");
                var paymentId = reader.GetInt32(0);
                return new(true, paymentId, transactionNo, null);
            }
            catch (SqlException ex) when (ex.Number is 51010 or 51011)
            { return new(false, ErrorMessage: "Vé đã hết hạn hoặc đã có giao dịch."); }
            catch (SqlException) { return new(false, ErrorMessage: "Không thể tạo giao dịch tiền mặt."); }
        }

        if (normalizedMethod == "BankTransfer")
        {
            const string bankTransferSql = @"
DECLARE @PaymentId int;
DECLARE @Amount decimal(18,2);
DECLARE @TransactionNo nvarchar(100) = @TransactionNoInput;
SELECT @Amount=t.Price FROM Tickets t WITH (UPDLOCK,HOLDLOCK)
WHERE t.TicketId=@TicketId AND t.TicketCode=@TicketCode AND t.Status=N'Đã đặt'
  AND t.PaymentStatus=N'Chưa thanh toán' AND t.PaymentExpiresAt>SYSUTCDATETIME();
IF @Amount IS NULL THROW 51010, 'Ticket is not payable.', 1;
IF EXISTS (SELECT 1 FROM Payments WHERE TicketId=@TicketId AND Status IN (N'Pending',N'Success'))
    THROW 51011, 'Payment already exists.', 1;
INSERT INTO Payments(TicketId,TransactionNo,Provider,ProviderTransactionNo,OrderInfo,PaymentMethod,Amount,Status)
VALUES(@TicketId,@TransactionNo,N'BankTransfer',NULL,@OrderInfo,N'BankTransfer',@Amount,N'Pending');
SELECT CAST(SCOPE_IDENTITY() AS int), @Amount;";
            try
            {
                var transactionNo = $"BANK{DateTime.UtcNow:yyyyMMddHHmmssfff}{RandomNumberGenerator.GetInt32(1000, 9999)}";
                await using var connection = DatabaseHelper.GetConnection();
                await connection.OpenAsync();
                await using var command = new SqlCommand(bankTransferSql, connection);
                command.Parameters.Add("@TicketId", SqlDbType.Int).Value = request.TicketId;
                command.Parameters.Add("@TicketCode", SqlDbType.NVarChar, 30).Value = request.TicketCode;
                command.Parameters.Add("@TransactionNoInput", SqlDbType.NVarChar, 100).Value = transactionNo;
                command.Parameters.Add("@OrderInfo", SqlDbType.NVarChar, 255).Value = $"Chuyen khoan ve {request.TicketCode}";
                await using var reader = await command.ExecuteReaderAsync();
                if (!await reader.ReadAsync()) return new(false, ErrorMessage: "Không thể tạo giao dịch chuyển khoản.");
                var paymentId = reader.GetInt32(0);
                return new(true, paymentId, transactionNo, null);
            }
            catch (SqlException ex) when (ex.Number is 51010 or 51011)
            { return new(false, ErrorMessage: "Vé đã hết hạn hoặc đã có giao dịch."); }
            catch (SqlException)
            { return new(false, ErrorMessage: "Không thể tạo giao dịch chuyển khoản."); }
        }

        const string sql = @"
DECLARE @PaymentId int;
DECLARE @Amount decimal(18,2);
DECLARE @TransactionNo nvarchar(100) = @TransactionNoInput;
SELECT @Amount=t.Price FROM Tickets t WITH (UPDLOCK,HOLDLOCK)
WHERE t.TicketId=@TicketId AND t.TicketCode=@TicketCode AND t.Status=N'Đã đặt'
  AND t.PaymentStatus=N'Chưa thanh toán' AND t.PaymentExpiresAt>SYSUTCDATETIME();
IF @Amount IS NULL THROW 51010, 'Ticket is not payable.', 1;
IF EXISTS (SELECT 1 FROM Payments WHERE TicketId=@TicketId AND Status IN (N'Pending',N'Success'))
    THROW 51011, 'Payment already exists.', 1;
INSERT INTO Payments(TicketId,TransactionNo,Provider,ProviderTransactionNo,OrderInfo,PaymentMethod,Amount,Status)
VALUES(@TicketId,@TransactionNo,N'VNPay',NULL,@OrderInfo,@Method,@Amount,N'Pending');
SELECT CAST(SCOPE_IDENTITY() AS int), @Amount;";
        try
        {
            var transactionNo = $"BT{DateTime.UtcNow:yyyyMMddHHmmssfff}{RandomNumberGenerator.GetInt32(1000, 9999)}";
            await using var connection = DatabaseHelper.GetConnection();
            await connection.OpenAsync();
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.Add("@TicketId", SqlDbType.Int).Value = request.TicketId;
            command.Parameters.Add("@TicketCode", SqlDbType.NVarChar, 30).Value = request.TicketCode;
            command.Parameters.Add("@Method", SqlDbType.NVarChar, 50).Value = normalizedMethod;
            command.Parameters.Add("@TransactionNoInput", SqlDbType.NVarChar, 100).Value = transactionNo;
            command.Parameters.Add("@OrderInfo", SqlDbType.NVarChar, 255).Value = $"Thanh toan ve {request.TicketCode}";
            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return new(false, ErrorMessage: "Không thể tạo giao dịch.");
            var paymentId = reader.GetInt32(0);
            var amount = reader.GetDecimal(1);
            return new(true, paymentId, transactionNo, BuildVnPayUrl(transactionNo, request.TicketCode, amount));
        }
        catch (SqlException ex) when (ex.Number is 51010 or 51011)
        { return new(false, ErrorMessage: "Vé đã hết hạn hoặc đã có giao dịch thanh toán."); }
        catch (SqlException) { return new(false, ErrorMessage: "Không thể khởi tạo giao dịch thanh toán."); }
    }

    public static async Task<PaymentVerificationResult> VerifyVnPayCallbackAsync(IReadOnlyDictionary<string, string> callback)
    {
        if (!IsValidSignature(callback) || !callback.TryGetValue("vnp_TxnRef", out var transactionNo) ||
            !callback.TryGetValue("vnp_Amount", out var amountText) || !decimal.TryParse(amountText, out var amountUnits) ||
            !callback.TryGetValue("vnp_ResponseCode", out var responseCode))
            return new(false, false, "Callback không hợp lệ hoặc sai chữ ký.");
        var amount = amountUnits / 100m;
        var providerTransactionNo = callback.GetValueOrDefault("vnp_TransactionNo");
        var raw = string.Join("&", callback.OrderBy(x => x.Key).Select(x => $"{x.Key}={x.Value}"));
        const string sql = @"
UPDATE p SET p.ResponseCode=@ResponseCode,p.RawResponse=@RawResponse,p.ProviderTransactionNo=@ProviderTransactionNo,
 p.VerifiedAt=SYSUTCDATETIME()
FROM Payments p WHERE p.TransactionNo=@TransactionNo AND p.Amount=@Amount AND p.Status=N'Pending'
 AND EXISTS(SELECT 1 FROM Tickets t WHERE t.TicketId=p.TicketId AND t.Status=N'Đã đặt' AND t.PaymentStatus=N'Chưa thanh toán' AND t.PaymentExpiresAt>SYSUTCDATETIME());
IF @@ROWCOUNT=0
BEGIN
 IF EXISTS(SELECT 1 FROM Payments WHERE TransactionNo=@TransactionNo AND Status=N'Success') SELECT 2; ELSE SELECT 0;
END
ELSE IF @ResponseCode=N'00'
BEGIN
 UPDATE Payments SET Status=N'Success',PaidAt=SYSUTCDATETIME() WHERE TransactionNo=@TransactionNo;
 UPDATE Tickets SET PaymentStatus=N'Đã thanh toán',PaymentExpiresAt=NULL,Status=N'Đã thanh toán'
 WHERE TicketId=(SELECT TicketId FROM Payments WHERE TransactionNo=@TransactionNo) AND Status=N'Đã đặt' AND PaymentStatus=N'Chưa thanh toán';
 SELECT 1;
END
ELSE BEGIN UPDATE Payments SET Status=N'Failed' WHERE TransactionNo=@TransactionNo; SELECT 0; END";
        await using var connection = DatabaseHelper.GetConnection(); await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            await using var command = new SqlCommand(sql, connection, transaction);
            command.Parameters.Add("@TransactionNo", SqlDbType.NVarChar, 100).Value = transactionNo;
            var amountParam = command.Parameters.Add("@Amount", SqlDbType.Decimal); amountParam.Precision = 18; amountParam.Scale = 2; amountParam.Value = amount;
            command.Parameters.Add("@ResponseCode", SqlDbType.NVarChar, 20).Value = responseCode;
            command.Parameters.Add("@ProviderTransactionNo", SqlDbType.NVarChar, 100).Value = (object?)providerTransactionNo ?? DBNull.Value;
            command.Parameters.Add("@RawResponse", SqlDbType.NVarChar, -1).Value = raw;
            var result = Convert.ToInt32(await command.ExecuteScalarAsync()); await transaction.CommitAsync();
            return result == 1 ? new(true, false) : result == 2 ? new(true, true) : new(false, false, "Thanh toán bị từ chối hoặc đã hết hạn.");
        }
        catch { await transaction.RollbackAsync(); return new(false, false, "Không thể xác minh callback."); }
    }

    public static async Task<string?> GetPaymentStatusAsync(string transactionNo)
    {
        if (string.IsNullOrWhiteSpace(transactionNo)) return null;
        const string sql = "SELECT Status FROM Payments WHERE TransactionNo=@TransactionNo;";
        await using var connection = DatabaseHelper.GetConnection();
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@TransactionNo", SqlDbType.NVarChar, 100).Value = transactionNo;
        return await command.ExecuteScalarAsync() as string;
    }

    public static async Task<int> ExpirePendingPaymentsAsync()
    {
        const string sql = @"UPDATE Payments SET Status=N'Failed',ResponseCode=N'EXPIRED',VerifiedAt=SYSUTCDATETIME()
WHERE Status=N'Pending' AND TicketId IN (SELECT TicketId FROM Tickets WHERE PaymentExpiresAt<=SYSUTCDATETIME());
UPDATE Tickets SET Status=N'Đã hủy',PaymentStatus=N'Chưa thanh toán',CancelledAt=SYSUTCDATETIME()
WHERE Status=N'Đã đặt' AND PaymentStatus=N'Chưa thanh toán' AND PaymentExpiresAt<=SYSUTCDATETIME();";
        await using var connection = DatabaseHelper.GetConnection(); await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection); return await command.ExecuteNonQueryAsync();
    }

    public static string BuildVnPayUrl(string transactionNo, string orderInfo, decimal amount)
    {
        var tmnCode = AppConfig.VnPayTmnCode;
        var secret = AppConfig.VnPayHashSecret;
        if (string.IsNullOrWhiteSpace(tmnCode) || string.IsNullOrWhiteSpace(secret))
            throw new InvalidOperationException("VNPay configuration is missing.");
        var values = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["vnp_Version"] = "2.1.0", ["vnp_Command"] = "pay", ["vnp_TmnCode"] = tmnCode,
            ["vnp_Amount"] = ((long)(amount * 100)).ToString(CultureInfo.InvariantCulture), ["vnp_CurrCode"] = "VND",
            ["vnp_TxnRef"] = transactionNo, ["vnp_OrderInfo"] = orderInfo, ["vnp_OrderType"] = "other",
            ["vnp_Locale"] = "vn", ["vnp_ReturnUrl"] = AppConfig.VnPayReturnUrl, ["vnp_IpAddr"] = "127.0.0.1",
            ["vnp_CreateDate"] = DateTime.UtcNow.AddHours(7).ToString("yyyyMMddHHmmss"),
            ["vnp_ExpireDate"] = DateTime.UtcNow.AddHours(7).AddMinutes(15).ToString("yyyyMMddHHmmss")
        };
        var query = string.Join("&", values.Select(x => $"{WebUtility.UrlEncode(x.Key)}={WebUtility.UrlEncode(x.Value)}"));
        var sign = Convert.ToHexString(new HMACSHA512(Encoding.UTF8.GetBytes(secret)).ComputeHash(Encoding.UTF8.GetBytes(query))).ToLowerInvariant();
        return $"{VnPayEndpoint}?{query}&vnp_SecureHash={sign}";
    }

    private static bool IsValidSignature(IReadOnlyDictionary<string, string> callback)
    {
        if (!callback.TryGetValue("vnp_SecureHash", out var expected)) return false;
        var data = string.Join("&", callback.Where(x => x.Key != "vnp_SecureHash" && x.Key != "vnp_SecureHashType")
            .OrderBy(x => x.Key).Select(x => $"{WebUtility.UrlEncode(x.Key)}={WebUtility.UrlEncode(x.Value)}"));
        var actual = Convert.ToHexString(new HMACSHA512(Encoding.UTF8.GetBytes(AppConfig.VnPayHashSecret))
            .ComputeHash(Encoding.UTF8.GetBytes(data))).ToLowerInvariant();
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(actual), Encoding.UTF8.GetBytes(expected.ToLowerInvariant()));
    }
}
