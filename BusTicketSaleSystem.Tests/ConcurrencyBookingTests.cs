using BusTicketSaleSystem.Data;
using Xunit;

namespace BusTicketSaleSystem.Tests;

public sealed class ConcurrencyBookingTests
{
    [SkippableFact]
    public async Task BookTicketAsync_AllowsOnlyOneConcurrentReservation()
    {
        var tripId = ReadRequiredInt("BUS_TICKET_TEST_TRIP_ID");
        var seatId = ReadRequiredInt("BUS_TICKET_TEST_SEAT_ID");
        var accountIds = ReadRequiredIds("BUS_TICKET_TEST_ACCOUNT_IDS", 10);

        var results = await Task.WhenAll(accountIds.Select(accountId =>
            DatabaseHelper.BookTicketAsync(
                accountId,
                tripId,
                seatId,
                $"Concurrency Test {accountId}",
                null)));

        Assert.Equal(1, results.Count(result => result.Success));
        Assert.Equal(accountIds.Count - 1, results.Count(result => !result.Success));
    }

    private static int ReadRequiredInt(string variable)
    {
        Skip.If(!int.TryParse(Environment.GetEnvironmentVariable(variable), out var value) || value <= 0, $"Set {variable} to run the isolated SQL Server concurrency fixture.");
        return value;
    }

    private static IReadOnlyList<int> ReadRequiredIds(string variable, int minimum)
    {
        var raw = Environment.GetEnvironmentVariable(variable);
        var values = raw?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(value => int.TryParse(value, out _))
            .Select(int.Parse)
            .Distinct()
            .ToArray();
        Skip.If(values is null || values.Length < minimum, $"Set {variable} to at least {minimum} isolated test account IDs.");
        return values;
    }
}
