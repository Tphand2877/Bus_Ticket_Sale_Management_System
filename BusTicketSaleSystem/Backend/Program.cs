using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
var connectionString = Environment.GetEnvironmentVariable("BUS_TICKET_CONNECTION_STRING")
    ?? throw new InvalidOperationException("BUS_TICKET_CONNECTION_STRING is required.");
var tmnCode = Environment.GetEnvironmentVariable("VNPAY_TMN_CODE") ?? throw new InvalidOperationException("VNPAY_TMN_CODE is required.");
var hashSecret = Environment.GetEnvironmentVariable("VNPAY_HASH_SECRET") ?? throw new InvalidOperationException("VNPAY_HASH_SECRET is required.");
var returnUrl = Environment.GetEnvironmentVariable("VNPAY_RETURN_URL") ?? "https://localhost:5001/api/payments/return";
const string endpoint = "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";

app.MapPost("/api/payments/create", async (CreatePayment request) =>
{
    await using var db = new SqlConnection(connectionString); await db.OpenAsync();
    await using var tx = (SqlTransaction)await db.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
    try
    {
        const string sql = @"SELECT t.Price FROM Tickets t WITH (UPDLOCK,HOLDLOCK)
WHERE t.TicketId=@TicketId AND t.TicketCode=@TicketCode AND t.Status=N'Đã đặt' AND t.PaymentStatus=N'Chưa thanh toán' AND t.PaymentExpiresAt>SYSUTCDATETIME();";
        await using var check = new SqlCommand(sql, db, tx); check.Parameters.AddWithValue("@TicketId", request.TicketId); check.Parameters.AddWithValue("@TicketCode", request.TicketCode);
        var value = await check.ExecuteScalarAsync(); if (value is null) return Results.BadRequest("Vé không còn hiệu lực.");
        var amount = Convert.ToDecimal(value, CultureInfo.InvariantCulture); var transactionNo = $"BT{DateTime.UtcNow:yyyyMMddHHmmssfff}{RandomNumberGenerator.GetInt32(1000, 9999)}";
        const string insert = @"INSERT INTO Payments(TicketId,TransactionNo,Provider,PaymentMethod,Amount,Status,OrderInfo) VALUES(@TicketId,@TransactionNo,N'VNPay',N'VNPay',@Amount,N'Pending',@OrderInfo);";
        await using var cmd = new SqlCommand(insert, db, tx); cmd.Parameters.AddWithValue("@TicketId", request.TicketId); cmd.Parameters.AddWithValue("@TransactionNo", transactionNo); cmd.Parameters.AddWithValue("@Amount", amount); cmd.Parameters.AddWithValue("@OrderInfo", $"Thanh toan ve {request.TicketCode}"); await cmd.ExecuteNonQueryAsync(); await tx.CommitAsync();
        return Results.Ok(new { transactionNo, paymentUrl = BuildUrl(transactionNo, request.TicketCode, amount) });
    }
    catch (SqlException) { await tx.RollbackAsync(); return Results.Conflict("Không thể tạo giao dịch hoặc vé đã có giao dịch."); }
});

app.MapGet("/api/payments/return", (HttpRequest request) => Results.Ok(new { status = "Đang được xác minh", transactionNo = request.Query["vnp_TxnRef"].ToString() }));
app.MapPost("/api/payments/ipn", async (HttpRequest request) => await Verify(await request.ReadFormAsync()));
app.MapGet("/api/payments/{transactionNo}/status", async (string transactionNo) =>
{
    await using var db = new SqlConnection(connectionString); await db.OpenAsync(); await using var cmd = new SqlCommand("SELECT Status,ResponseCode,VerifiedAt FROM Payments WHERE TransactionNo=@TransactionNo", db); cmd.Parameters.AddWithValue("@TransactionNo", transactionNo); await using var r = await cmd.ExecuteReaderAsync(); return await r.ReadAsync() ? Results.Ok(new { status = r.GetString(0), responseCode = r.IsDBNull(1) ? null : r.GetString(1), verifiedAt = r.IsDBNull(2) ? (DateTime?)null : r.GetDateTime(2) }) : Results.NotFound();
});
app.MapPost("/api/payments/{transactionNo}/refund", async (string transactionNo) =>
{
    await using var db = new SqlConnection(connectionString); await db.OpenAsync(); await using var cmd = new SqlCommand("UPDATE Payments SET RefundRequestedAt=SYSUTCDATETIME() WHERE TransactionNo=@TransactionNo AND Status=N'Success' AND RefundRequestedAt IS NULL", db); cmd.Parameters.AddWithValue("@TransactionNo", transactionNo); return await cmd.ExecuteNonQueryAsync() == 1 ? Results.Accepted(value: new { status = "RefundPending" }) : Results.Conflict("Giao dịch không đủ điều kiện hoàn tiền hoặc đã yêu cầu trước đó.");
});

await app.RunAsync();
async Task<IResult> Verify(IFormCollection form)
{
    var values = form.ToDictionary(x => x.Key, x => x.Value.ToString(), StringComparer.Ordinal); if (!ValidSignature(values)) return Results.Ok(new { RspCode = "97", Message = "Invalid signature" });
    if (!values.TryGetValue("vnp_TxnRef", out var transactionNo) || !decimal.TryParse(values.GetValueOrDefault("vnp_Amount"), out var units)) return Results.Ok(new { RspCode = "99", Message = "Invalid request" });
    var amount = units / 100m; var code = values.GetValueOrDefault("vnp_ResponseCode") ?? "99";
    await using var db = new SqlConnection(connectionString); await db.OpenAsync(); await using var tx = (SqlTransaction)await db.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
    const string sql = @"UPDATE p SET ResponseCode=@Code,ProviderTransactionNo=@ProviderNo,RawResponse=@Raw,VerifiedAt=SYSUTCDATETIME(),Status=CASE WHEN @Code=N'00' THEN N'Success' ELSE N'Failed' END,PaidAt=CASE WHEN @Code=N'00' THEN SYSUTCDATETIME() ELSE PaidAt END FROM Payments p JOIN Tickets t ON t.TicketId=p.TicketId WHERE p.TransactionNo=@TransactionNo AND p.Amount=@Amount AND p.Status=N'Pending' AND t.Status=N'Đã đặt' AND t.PaymentStatus=N'Chưa thanh toán' AND t.PaymentExpiresAt>SYSUTCDATETIME(); UPDATE Tickets SET Status=N'Đã thanh toán',PaymentStatus=N'Đã thanh toán',PaymentExpiresAt=NULL WHERE TicketId=(SELECT TicketId FROM Payments WHERE TransactionNo=@TransactionNo) AND @Code=N'00' AND Status=N'Đã đặt' AND PaymentStatus=N'Chưa thanh toán';";
    await using var cmd = new SqlCommand(sql, db, tx); cmd.Parameters.AddWithValue("@TransactionNo", transactionNo); cmd.Parameters.AddWithValue("@Amount", amount); cmd.Parameters.AddWithValue("@Code", code); cmd.Parameters.AddWithValue("@ProviderNo", values.GetValueOrDefault("vnp_TransactionNo") ?? (object)DBNull.Value); cmd.Parameters.AddWithValue("@Raw", string.Join("&", values.OrderBy(x => x.Key).Select(x => $"{x.Key}={x.Value}"))); var changed = await cmd.ExecuteNonQueryAsync(); await tx.CommitAsync(); return Results.Ok(new { RspCode = changed > 0 ? "00" : "02", Message = changed > 0 ? "Confirm Success" : "Order already processed or invalid" });
}

string BuildUrl(string transactionNo, string orderInfo, decimal amount)
{
    var p = new SortedDictionary<string, string>(StringComparer.Ordinal) { ["vnp_Version"] = "2.1.0", ["vnp_Command"] = "pay", ["vnp_TmnCode"] = tmnCode, ["vnp_Amount"] = ((long)(amount * 100)).ToString(CultureInfo.InvariantCulture), ["vnp_CurrCode"] = "VND", ["vnp_TxnRef"] = transactionNo, ["vnp_OrderInfo"] = orderInfo, ["vnp_OrderType"] = "other", ["vnp_Locale"] = "vn", ["vnp_ReturnUrl"] = returnUrl, ["vnp_IpAddr"] = "127.0.0.1", ["vnp_CreateDate"] = DateTime.UtcNow.AddHours(7).ToString("yyyyMMddHHmmss"), ["vnp_ExpireDate"] = DateTime.UtcNow.AddHours(7).AddMinutes(15).ToString("yyyyMMddHHmmss") };
    var query = string.Join("&", p.Select(x => $"{Uri.EscapeDataString(x.Key)}={Uri.EscapeDataString(x.Value)}")); var sign = Convert.ToHexString(new HMACSHA512(Encoding.UTF8.GetBytes(hashSecret)).ComputeHash(Encoding.UTF8.GetBytes(query))).ToLowerInvariant(); return $"{endpoint}?{query}&vnp_SecureHash={sign}";
}
bool ValidSignature(IReadOnlyDictionary<string, string> p) { if (!p.TryGetValue("vnp_SecureHash", out var expected)) return false; var query = string.Join("&", p.Where(x => x.Key is not "vnp_SecureHash" and not "vnp_SecureHashType").OrderBy(x => x.Key).Select(x => $"{Uri.EscapeDataString(x.Key)}={Uri.EscapeDataString(x.Value)}")); var actual = Convert.ToHexString(new HMACSHA512(Encoding.UTF8.GetBytes(hashSecret)).ComputeHash(Encoding.UTF8.GetBytes(query))).ToLowerInvariant(); return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(actual), Encoding.UTF8.GetBytes(expected.ToLowerInvariant())); }
record CreatePayment(int TicketId, string TicketCode);
