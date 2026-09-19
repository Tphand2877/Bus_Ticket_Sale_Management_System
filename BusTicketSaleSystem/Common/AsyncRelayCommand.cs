using System;
using System.Threading.Tasks;
using System.Windows.Input;
using BusTicketSaleSystem.Services;

namespace BusTicketSaleSystem.Common
{
    /// <summary>
    /// ICommand for asynchronous handlers.
    /// <para>
    /// Exists because <see cref="RelayCommand"/> only accepts <see cref="Action"/>: writing
    /// <c>new RelayCommand(async () =&gt; await SaveAsync())</c> compiles to <c>async void</c>, which
    /// (a) returns to the caller at the first await so the command is immediately re-invokable —
    /// a double-click issues two bookings — and (b) rethrows on the dispatcher, killing the app.
    /// </para>
    /// <para>
    /// This command awaits the handler, reports <see cref="IsRunning"/> while in flight, refuses
    /// re-entry for that whole window, and routes faults to <see cref="LoggerService"/>.
    /// </para>
    /// </summary>
    public sealed class AsyncRelayCommand : ICommand
    {
        private readonly Func<object?, Task> _executeAsync;
        private readonly Predicate<object?>? _canExecute;
        private readonly Action<Exception>? _onError;
        private bool _isRunning;

        public event EventHandler? CanExecuteChanged;

        /// <summary>True from invocation until the handler's task completes.</summary>
        public bool IsRunning
        {
            get => _isRunning;
            private set
            {
                if (_isRunning == value) return;
                _isRunning = value;
                RaiseCanExecuteChanged();
            }
        }

        public AsyncRelayCommand(Func<Task> executeAsync, Func<bool>? canExecute = null, Action<Exception>? onError = null)
            : this(
                executeAsync is null ? throw new ArgumentNullException(nameof(executeAsync)) : _ => executeAsync(),
                canExecute != null ? _ => canExecute() : null,
                onError)
        {
        }

        public AsyncRelayCommand(Func<object?, Task> executeAsync, Predicate<object?>? canExecute = null, Action<Exception>? onError = null)
        {
            _executeAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
            _canExecute = canExecute;
            _onError = onError;
        }

        /// <summary>Blocked while the previous invocation is still running.</summary>
        public bool CanExecute(object? parameter)
            => !_isRunning && (_canExecute == null || _canExecute(parameter));

        public async void Execute(object? parameter)
        {
            if (!CanExecute(parameter)) return;

            IsRunning = true;
            try
            {
                await _executeAsync(parameter);
            }
            catch (Exception ex)
            {
                // A faulted command must never take down the dispatcher.
                LoggerService.LogError("An asynchronous command failed.", ex);
                _onError?.Invoke(ex);
            }
            finally
            {
                IsRunning = false;
            }
        }

        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
            // Buttons bound via CommandManager still need a requery to pick the new state up.
            CommandManager.InvalidateRequerySuggested();
        }
    }
}
