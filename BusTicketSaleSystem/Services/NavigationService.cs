using BusTicketSaleSystem.Common;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BusTicketSaleSystem.Services
{
    /// <summary>
    /// Implements ViewModel-first navigation.
    /// Manages CurrentViewModel and notifies subscribers via INotifyPropertyChanged.
    /// </summary>
    /// <remarks>
    /// Only ViewModels explicitly handed over through <see cref="Register{TViewModel}"/> are kept
    /// as singletons (shell-owned state). Everything else is constructed fresh on every
    /// <see cref="NavigateTo{TViewModel}"/> so a screen never shows data captured at first
    /// construction. ViewModels implementing <see cref="IRefreshable"/> get
    /// <see cref="IRefreshable.RefreshAsync"/> invoked right after they become current.
    /// </remarks>
    public class NavigationService : ViewModelBase, INavigationService
    {
        private ViewModelBase? _currentViewModel;
        private Func<Type, ViewModelBase>? _viewModelFactory;
        private readonly Dictionary<Type, ViewModelBase> _singletons = new();

        public ViewModelBase? CurrentViewModel
        {
            get => _currentViewModel;
            private set => SetProperty(ref _currentViewModel, value);
        }

        /// <summary>
        /// Registers a factory delegate to resolve ViewModels with dependencies.
        /// </summary>
        public void SetFactory(Func<Type, ViewModelBase> factory)
        {
            _viewModelFactory = factory;
        }

        /// <summary>
        /// Registers a long-lived ViewModel instance that must survive navigation.
        /// </summary>
        public void Register<TViewModel>(TViewModel viewModel) where TViewModel : ViewModelBase
        {
            if (viewModel == null) throw new ArgumentNullException(nameof(viewModel));
            _singletons[typeof(TViewModel)] = viewModel;
        }

        /// <summary>
        /// Navigates to the specified ViewModel type, creating a new instance unless the
        /// type was explicitly registered as a singleton.
        /// </summary>
        public void NavigateTo<TViewModel>() where TViewModel : ViewModelBase
        {
            var type = typeof(TViewModel);

            if (_singletons.TryGetValue(type, out var registered))
            {
                Activate(registered);
                return;
            }

            // No silent Activator fallback: it produced ViewModels wired to a detached
            // NavigationService, so the screen appeared to work while none of its
            // navigation did, with no exception and no log entry (AUD-A-010).
            if (_viewModelFactory is null)
                throw new InvalidOperationException(
                    $"Cannot navigate to {type.Name}: no ViewModel factory has been set on this NavigationService.");

            var instance = _viewModelFactory(type);

            Activate(instance);
        }

        /// <summary>
        /// Navigates directly to a concrete ViewModel instance.
        /// </summary>
        public void NavigateTo(ViewModelBase viewModel)
        {
            if (viewModel == null) throw new ArgumentNullException(nameof(viewModel));
            Activate(viewModel);
        }

        private void Activate(ViewModelBase viewModel)
        {
            var outgoing = CurrentViewModel;
            CurrentViewModel = viewModel;

            // The navigator replaces ViewModels, so the navigator owns their end of
            // life. Without this, every screen that subscribes to a singleton service
            // or runs a timer leaked one live instance per navigation, and those dead
            // instances kept issuing database queries (AUD-E-002, AUD-E-005).
            DisposeIfTransient(outgoing, viewModel);

            if (viewModel is IRefreshable refreshable)
            {
                _ = RefreshSafelyAsync(refreshable);
            }
        }

        /// <summary>
        /// Disposes the ViewModel we navigated away from, unless it is the one we just
        /// navigated to or a registered singleton the shell keeps alive.
        /// </summary>
        private void DisposeIfTransient(ViewModelBase? outgoing, ViewModelBase incoming)
        {
            if (outgoing is null || ReferenceEquals(outgoing, incoming)) return;
            if (outgoing is not IDisposable disposable) return;
            if (_singletons.ContainsValue(outgoing)) return;

            try
            {
                disposable.Dispose();
            }
            catch (Exception ex)
            {
                LoggerService.LogError($"Disposing {outgoing.GetType().Name} after navigation failed.", ex);
            }
        }

        private static async Task RefreshSafelyAsync(IRefreshable refreshable)
        {
            try
            {
                await refreshable.RefreshAsync();
            }
            catch (Exception ex)
            {
                LoggerService.LogError($"Refreshing {refreshable.GetType().Name} after navigation failed.", ex);
            }
        }
    }
}
