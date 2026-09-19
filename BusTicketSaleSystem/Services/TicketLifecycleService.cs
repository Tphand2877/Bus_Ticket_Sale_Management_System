using System.Data;
using System.Globalization;
using BusTicketSaleSystem.Data;
using Microsoft.Data.SqlClient;

namespace BusTicketSaleSystem.Services;

/// <summary>
/// Moves paid tickets into their terminal <c>N'Đã sử dụng'</c> state, either automatically once
/// the coach has left or explicitly when an operator checks a passenger in at the door.
/// </summary>
public static class TicketLifecycleService
{
    /// <summary>Marks every paid ticket whose trip has already departed as used. Returns the number of tickets updated.</summary>
    public static async Task<int> MarkDepartedTicketsUsedAsync()
    {
        const string sql = @"
UPDATE t SET t.Status=N'Đã sử dụng', t.UsedAt=SYSUTCDATETIME()
FROM Tickets t WITH (UPDLOCK,HOLDLOCK)
JOIN Trips tr ON tr.TripId=t.TripId
WHERE t.Status=N'Đã thanh toán' AND tr.DepartureTime<=SYSUTCDATETIME();";
        await using var connection = DatabaseHelper.GetConnection();
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        return await command.ExecuteNonQueryAsync();
    }

    /// <summary>Boarding check-in performed by an operator: a paid ticket becomes used.</summary>
    public static async Task<(bool Success, string? Error)> CheckInTicketAsync(int ticketId, int adminAccountId)
    {
        if (PaymentService.ValidateAdminSession(adminAccountId) is { } authError) return (false, authError);
        if (ticketId <= 0) return (false, "No ticket was selected.");

        const string sql = @"
DECLARE @Result int = 0;
DECLARE @State nvarchar(50);
SELECT @State=t.Status FROM Tickets t WITH (UPDLOCK,HOLDLOCK) WHERE t.TicketId=@TicketId;
IF @State IS NULL SET @Result=1;
ELSE IF @State=N'Đã sử dụng' SET @Result=2;
ELSE IF @State=N'Đã hủy' SET @Result=3;
ELSE IF @State<>N'Đã thanh toán' SET @Result=4;
ELSE UPDATE Tickets SET Status=N'Đã sử dụng', UsedAt=SYSUTCDATETIME() WHERE TicketId=@TicketId;
SELECT @Result;";

        SqlTransaction? transaction = null;
        try
        {
            await using var connection = DatabaseHelper.GetConnection();
            await connection.OpenAsync();
            transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable);
            await using var command = new SqlCommand(sql, connection, transaction);
            command.Parameters.Add("@TicketId", SqlDbType.Int).Value = ticketId;
            var outcome = Convert.ToInt32(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
            if (outcome != 0)
            {
                await transaction.RollbackAsync();
                return (false, outcome switch
                {
                    1 => "That ticket no longer exists.",
                    2 => "This ticket has already been checked in.",
                    3 => "This ticket was cancelled and cannot be checked in.",
                    _ => "This ticket is not paid yet, so it cannot be checked in."
                });
            }
            await transaction.CommitAsync();
            LoggerService.LogInfo($"Ticket {ticketId} checked in by operator {adminAccountId}.");
            return (true, null);
        }
        catch (SqlException ex)
        {
            await RollbackQuietlyAsync(transaction);
            LoggerService.LogError($"Database failure while checking in ticket {ticketId}.", ex);
            return (false, "The ticket store is unavailable. Please try again.");
        }
        catch (InvalidOperationException ex)
        {
            await RollbackQuietlyAsync(transaction);
            LoggerService.LogError($"Invalid state while checking in ticket {ticketId}.", ex);
            return (false, "The check-in could not be completed.");
        }
        finally
        {
            if (transaction is not null) await transaction.DisposeAsync();
        }
    }

    private static async Task RollbackQuietlyAsync(SqlTransaction? transaction)
    {
        if (transaction?.Connection is null) return;
        try { await transaction.RollbackAsync(); }
        catch (InvalidOperationException ex) { LoggerService.LogWarning($"Transaction rollback skipped: {ex.Message}"); }
        catch (SqlException ex) { LoggerService.LogError("Transaction rollback failed.", ex); }
    }
}
