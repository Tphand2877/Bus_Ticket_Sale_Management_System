using BusTicketSaleSystem.Common;
using System;

namespace BusTicketSaleSystem.Services
{
    /// <summary>
    /// Contract for ViewModel-first navigation service.
    /// Manages switching the CurrentViewModel displayed in the central ContentControl.
    /// </summary>
    public interface INavigationService
    {
        /// <summary>
        /// The ViewModel currently active and displayed in the main window.
        /// </summary>
        ViewModelBase? CurrentViewModel { get; }

        /// <summary>
        /// Navigates to a registered ViewModel of type TViewModel.
        /// </summary>
        void NavigateTo<TViewModel>() where TViewModel : ViewModelBase;

        /// <summary>
        /// Navigates to a specific ViewModel instance.
        /// </summary>
        void NavigateTo(ViewModelBase viewModel);
    }
}
