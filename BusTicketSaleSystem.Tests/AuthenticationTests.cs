using BusTicketSaleSystem.Data;
using Xunit;

namespace BusTicketSaleSystem.Tests;

/// <summary>
/// Authentication behaviour against the real account store: lockout, counter reset
/// and the absence of an account-enumeration oracle.
/// </summary>
[Collection(SqlServerCollection.Name)]
public sealed class AuthenticationTests(SqlServerFixture fixture)
{
    private const string GoodPassword = "IntegrationPass123";

    private readonly SqlServerFixture _fixture = fixture;

    [SkippableFact]
    public async Task AuthenticateAsync_LocksTheAccountAfterRepeatedFailures()
    {
        _fixture.SkipIfUnavailable();
        var username = await CreateAccountAsync();

        for (var attempt = 1; attempt < DatabaseHelper.MaxFailedLoginAttempts; attempt++)
        {
            var interim = await DatabaseHelper.AuthenticateAsync(username, "WrongPassword123");
            Assert.False(interim.Success);
            Assert.Contains("Incorrect", interim.Error);
        }

        var locking = await DatabaseHelper.AuthenticateAsync(username, "WrongPassword123");
        Assert.False(locking.Success);
        Assert.Contains("locked", locking.Error);

        // The correct password must also be refused while the lockout is in force.
        var duringLockout = await DatabaseHelper.AuthenticateAsync(username, GoodPassword);
        Assert.False(duringLockout.Success);
        Assert.Contains("locked", duringLockout.Error);
    }

    [SkippableFact]
    public async Task AuthenticateAsync_ClearsTheCounterAfterASuccessfulSignIn()
    {
        _fixture.SkipIfUnavailable();
        var username = await CreateAccountAsync();

        await DatabaseHelper.AuthenticateAsync(username, "WrongPassword123");
        await DatabaseHelper.AuthenticateAsync(username, "WrongPassword123");

        var success = await DatabaseHelper.AuthenticateAsync(username, GoodPassword);
        Assert.True(success.Success, success.Error);

        var counter = await _fixture.ScalarAsync<int>(
            $"SELECT FailedLoginCount FROM dbo.Accounts WHERE Username = N'{username}';");
        Assert.Equal(0, counter);

        // A fresh run of failures must start from zero, not from the earlier two.
        for (var attempt = 1; attempt < DatabaseHelper.MaxFailedLoginAttempts; attempt++)
            Assert.Contains("Incorrect", (await DatabaseHelper.AuthenticateAsync(username, "WrongPassword123")).Error);
    }

    [SkippableFact]
    public async Task AuthenticateAsync_LockoutExpiresAndLetsTheOwnerBackIn()
    {
        _fixture.SkipIfUnavailable();
        var username = await CreateAccountAsync();

        for (var attempt = 0; attempt < DatabaseHelper.MaxFailedLoginAttempts; attempt++)
            await DatabaseHelper.AuthenticateAsync(username, "WrongPassword123");

        Assert.Contains("locked", (await DatabaseHelper.AuthenticateAsync(username, GoodPassword)).Error);

        // Move the lockout into the past rather than waiting 15 minutes.
        await _fixture.ExecuteAsync(
            $"UPDATE dbo.Accounts SET LockoutEndsAt = DATEADD(MINUTE, -1, SYSUTCDATETIME()) WHERE Username = N'{username}';");

        var afterWindow = await DatabaseHelper.AuthenticateAsync(username, GoodPassword);
        Assert.True(afterWindow.Success, afterWindow.Error);
    }

    /// <summary>
    /// An unknown login must cost the same as a wrong password, otherwise response
    /// time reveals which accounts exist (AUD-D-011). The threshold is deliberately
    /// loose: it catches the old behaviour, which skipped a 310,000-iteration
    /// derivation entirely, without being sensitive to machine noise.
    /// </summary>
    [SkippableFact]
    public async Task AuthenticateAsync_UnknownLoginCostsTheSameAsAWrongPassword()
    {
        _fixture.SkipIfUnavailable();
        var username = await CreateAccountAsync();

        var knownAccount = await MeasureAsync(() => DatabaseHelper.AuthenticateAsync(username, "WrongPassword123"));
        var unknownAccount = await MeasureAsync(() => DatabaseHelper.AuthenticateAsync($"no_such_user_{Guid.NewGuid():N}", "WrongPassword123"));

        Assert.True(unknownAccount >= knownAccount * 0.5,
            $"An unknown login took {unknownAccount:0} ms against {knownAccount:0} ms for a wrong password: the miss path is skipping key derivation.");
    }

    private static async Task<double> MeasureAsync(Func<Task> action)
    {
        // One warm-up pass so connection-pool and JIT costs do not dominate.
        await action();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        for (var i = 0; i < 3; i++) await action();
        return stopwatch.Elapsed.TotalMilliseconds / 3;
    }

    private async Task<string> CreateAccountAsync()
    {
        var username = $"auth_{Guid.NewGuid():N}"[..20];
        var (created, error) = await DatabaseHelper.CreateAccountAsync(
            username, GoodPassword, "Auth Test", null, $"{username}@test.local");
        Assert.True(created, error);
        return username;
    }
}
