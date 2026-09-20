using BusTicketSaleSystem.Data;
using Xunit;

namespace BusTicketSaleSystem.Tests;

/// <summary>
/// The migration history must describe the schema truthfully: every DDL statement the
/// application runs is a recorded migration, and an applied script cannot be edited
/// behind the history's back (AUD-B-008, AUD-B-009).
/// </summary>
[Collection(SqlServerCollection.Name)]
public sealed class MigrationHistoryTests(SqlServerFixture fixture)
{
    private readonly SqlServerFixture _fixture = fixture;

    [SkippableFact]
    public async Task Migrator_RecordsEveryScriptItRanWithAChecksum()
    {
        _fixture.SkipIfUnavailable();

        var withoutChecksum = await _fixture.ScalarAsync<int>(
            "SELECT COUNT(*) FROM dbo.__DatabaseMigrations WHERE Checksum IS NULL;");
        Assert.Equal(0, withoutChecksum);
    }

    /// <summary>
    /// The StagePrice columns used to be added by a startup probe outside the history,
    /// so a database rebuilt from the scripts alone was missing them.
    /// </summary>
    [SkippableFact]
    public async Task Migrator_CreatesTheStagePriceColumnsAsATrackedMigration()
    {
        _fixture.SkipIfUnavailable();

        Assert.Equal(1, await _fixture.ScalarAsync<int>(
            "SELECT COUNT(*) FROM dbo.__DatabaseMigrations WHERE ScriptName = N'017_StagePrice';"));
        Assert.True(await _fixture.ScalarAsync<int>(
            "SELECT CASE WHEN COL_LENGTH(N'dbo.Buses', N'StagePrice') IS NULL THEN 0 ELSE 1 END;") == 1);
        Assert.True(await _fixture.ScalarAsync<int>(
            "SELECT CASE WHEN COL_LENGTH(N'dbo.Trips', N'StagePrice') IS NULL THEN 0 ELSE 1 END;") == 1);
    }

    [SkippableFact]
    public async Task Migrator_FailsWhenAnAppliedScriptHasBeenEdited()
    {
        _fixture.SkipIfUnavailable();

        var original = await _fixture.ScalarAsync<string>(
            "SELECT Checksum FROM dbo.__DatabaseMigrations WHERE ScriptName = N'016_SeatAvailabilityIndexes';");
        Assert.NotNull(original);

        // Simulate an edit by corrupting the recorded checksum.
        await _fixture.ExecuteAsync(
            "UPDATE dbo.__DatabaseMigrations SET Checksum = REPLICATE('0', 64) WHERE ScriptName = N'016_SeatAvailabilityIndexes';");
        try
        {
            Assert.False(await DatabaseMigrator.MigrateAsync());
            Assert.Contains("has changed since it was applied", DatabaseMigrator.LastError);
        }
        finally
        {
            await _fixture.ExecuteAsync(
                $"UPDATE dbo.__DatabaseMigrations SET Checksum = '{original}' WHERE ScriptName = N'016_SeatAvailabilityIndexes';");
        }

        // With the history restored, migration succeeds again.
        Assert.True(await DatabaseMigrator.MigrateAsync());
    }
}
