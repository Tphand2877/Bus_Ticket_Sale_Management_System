using BusTicketSaleSystem.Models;

namespace BusTicketSaleSystem.Services;

/// <summary>Owns sign-in and sign-out for the whole application.</summary>
public interface ISessionService
{
    UserSession? Account { get; }
    bool IsAdmin { get; }

    void SignIn(UserSession account);

    /// <summary>Clears the session. Raises <see cref="SignedOut"/> exactly once.</summary>
    void SignOut();

    event EventHandler? SignedOut;
}

/// <summary>
/// Single implementation of session teardown (AUD-A-008).
/// </summary>
/// <remarks>
/// Both shells used to clear <c>CurrentUser.Account</c> and swap windows in duplicated
/// code-behind, so anything added later — clearing caches, cancelling in-flight work,
/// writing an audit row — had to be remembered twice.
/// <para>
/// <see cref="CurrentUser"/> remains the ambient identity the data layer reads; this
/// service is the only thing that writes it. Removing that ambient read entirely is
/// AUD-W3-02.
/// </para>
/// </remarks>
public sealed class SessionService : ISessionService
{
    public UserSession? Account => CurrentUser.Account;

    public bool IsAdmin => CurrentUser.IsAdmin;

    public event EventHandler? SignedOut;

    public void SignIn(UserSession account)
    {
        ArgumentNullException.ThrowIfNull(account);
        CurrentUser.Account = account;
        LoggerService.LogInfo($"Account {account.AccountId} signed in as {account.Role}.");
    }

    public void SignOut()
    {
        var accountId = CurrentUser.Account?.AccountId;
        CurrentUser.Account = null;
        if (accountId is not null) LoggerService.LogInfo($"Account {accountId} signed out.");
        SignedOut?.Invoke(this, EventArgs.Empty);
    }
}
