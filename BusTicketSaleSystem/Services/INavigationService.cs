using BusTicketSaleSystem.Common;
using System;
using System.ComponentModel;

namespace BusTicketSaleSystem.Services
{
    /// <summary>
    /// Contract for ViewModel-first navigation service.
    /// Manages switching the CurrentViewModel displayed in the central ContentControl.
    /// </summary>
    /// <remarks>
    /// <see cref="INotifyPropertyChanged"/> is part of the contract: the shell binds to
    /// <see cref="CurrentViewModel"/>, so an implementation that does not raise change
    /// notifications produces a shell that silently never updates. MainViewModel used to
    /// discover this with a runtime type test (AUD-A-009).
    /// </remarks>
    public interface INavigationService : INotifyPropertyChanged
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
