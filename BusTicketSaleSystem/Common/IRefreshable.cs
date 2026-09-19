namespace BusTicketSaleSystem.Common;

/// <summary>
/// Implemented by ViewModels whose content is loaded from the database and must be
/// reloaded every time the screen becomes current. <see cref="Services.NavigationService"/>
/// invokes <see cref="RefreshAsync"/> after each navigation.
/// </summary>
public interface IRefreshable
{
    Task RefreshAsync();
}
