using BusTicketSaleSystem.Data;

namespace BusTicketSaleSystem.Tests;

public sealed class SecurityTests
{
    [Fact]
    public void HashPassword_UsesRandomSaltForSamePassword()
    {
        var first = DatabaseHelper.HashPassword("UserSecurePass123!");
        var second = DatabaseHelper.HashPassword("UserSecurePass123!");

        Assert.NotEqual(first, second);
        Assert.StartsWith("PBKDF2-SHA256$", first, StringComparison.Ordinal);
        Assert.StartsWith("PBKDF2-SHA256$", second, StringComparison.Ordinal);

        var parts = first.Split('$');
        Assert.Equal(4, parts.Length);
        Assert.True(int.TryParse(parts[1], out var iterations) && iterations >= 310_000, "PBKDF2 iterations must be at least 310,000.");
    }

    [Theory]
    [InlineData("CorrectPassword123")]
    [InlineData("ComplexP@ssw0rd!2026")]
    public void VerifyPassword_ReturnsTrueForOriginalPassword(string password)
    {
        var hash = DatabaseHelper.HashPassword(password);

        Assert.True(DatabaseHelper.VerifyPassword(password, hash));
    }

    [Fact]
    public void VerifyPassword_RejectsWrongPassword()
    {
        var hash = DatabaseHelper.HashPassword("RightPassword123");

        Assert.False(DatabaseHelper.VerifyPassword("WrongPassword123", hash));
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("password", "not-a-pbkdf2-value")]
    [InlineData("password", null)]
    public void VerifyPassword_RejectsInvalidStoredValues(string password, string? stored)
    {
        Assert.False(DatabaseHelper.VerifyPassword(password, stored));
    }

    [Fact]
    public void VerifyPassword_AcceptsLegacyPBKDF2Prefix()
    {
        // Manually build a legacy-format hash: PBKDF2$iterations$salt$hash
        var salt = System.Security.Cryptography.RandomNumberGenerator.GetBytes(16);
        var derived = System.Security.Cryptography.Rfc2898DeriveBytes.Pbkdf2(
            "LegacyPassword123", salt, 310_000, System.Security.Cryptography.HashAlgorithmName.SHA256, 32);
        var legacy = $"PBKDF2$310000${Convert.ToBase64String(salt)}${Convert.ToBase64String(derived)}";

        Assert.True(DatabaseHelper.VerifyPassword("LegacyPassword123", legacy));
        Assert.False(DatabaseHelper.VerifyPassword("WrongPassword", legacy));
    }

    /// <summary>
    /// AUD-D-002: a stored value that is not a derived hash must never authenticate.
    /// Before the fix, writing an arbitrary string into Accounts.PasswordHash made
    /// that string a working password for the account.
    /// </summary>
    [Theory]
    [InlineData("x")]
    [InlineData("password")]
    [InlineData("PBKDF2-SHA256")]
    [InlineData("PBKDF2-SHA256$310000$notbase64$alsonotbase64")]
    [InlineData("PBKDF2-SHA256$0$c2FsdA==$aGFzaA==")]
    public void VerifyPassword_RejectsStoredValueThatIsNotADerivedHash(string stored)
    {
        Assert.False(DatabaseHelper.VerifyPassword(stored, stored));
    }
}
