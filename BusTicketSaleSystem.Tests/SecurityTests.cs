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
        Assert.StartsWith("PBKDF2$", first, StringComparison.Ordinal);
        Assert.StartsWith("PBKDF2$", second, StringComparison.Ordinal);
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
}
