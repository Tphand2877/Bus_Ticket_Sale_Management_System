using BusTicketSaleSystem.Common;
using System;
using System.Collections.Generic;

namespace BusTicketSaleSystem.Services
{
    /// <summary>
    /// Implements ViewModel-first navigation.
    /// Manages CurrentViewModel and notifies subscribers via INotifyPropertyChanged.
    /// </summary>
    public class NavigationService : ViewModelBase, INavigationService
    {
        private ViewModelBase? _currentViewModel;
        private Func<Type, ViewModelBase>? _viewModelFactory;
        private readonly Dictionary<Type, ViewModelBase> _cache = new();

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
        /// Registers an existing ViewModel instance.
        /// </summary>
        public void Register<TViewModel>(TViewModel viewModel) where TViewModel : ViewModelBase
        {
            if (viewModel == null) throw new ArgumentNullException(nameof(viewModel));
            _cache[typeof(TViewModel)] = viewModel;
        }

        /// <summary>
        /// Navigates to the specified ViewModel type. Reuses existing instance if cached.
        /// </summary>
        public void NavigateTo<TViewModel>() where TViewModel : ViewModelBase
        {
            var type = typeof(TViewModel);

            if (_cache.TryGetValue(type, out var cached))
            {
                CurrentViewModel = cached;
                return;
            }

            if (_viewModelFactory != null)
            {
                var created = _viewModelFactory(type);
                _cache[type] = created;
                CurrentViewModel = created;
                return;
            }

            // Fallback: parameterless activation
            var instance = (ViewModelBase)Activator.CreateInstance(type)!;
            _cache[type] = instance;
            CurrentViewModel = instance;
        }

        /// <summary>
        /// Navigates directly to a concrete ViewModel instance.
        /// </summary>
        public void NavigateTo(ViewModelBase viewModel)
        {
            if (viewModel == null) throw new ArgumentNullException(nameof(viewModel));
            _cache[viewModel.GetType()] = viewModel;
            CurrentViewModel = viewModel;
        }
    }
}
