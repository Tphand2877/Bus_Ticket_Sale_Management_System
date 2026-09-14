namespace BusTicketSaleSystem.Models;

public sealed record UserSession(
    int AccountId,
    string Username,
    string FullName,
    string? Email,
    string? Phone,
    string Role);

public static class CurrentUser
{
    public static UserSession? Account { get; set; }
    public static bool IsAdmin => string.Equals(Account?.Role, "Admin", StringComparison.OrdinalIgnoreCase);
}
