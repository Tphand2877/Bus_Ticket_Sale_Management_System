using BusTicketSaleSystem.Common;
using Xunit;

namespace BusTicketSaleSystem.Tests;

/// <summary>
/// Command handlers that return a Task must not be invocable again while the first
/// invocation is still running: <c>new RelayCommand(async () =&gt; ...)</c> compiles to
/// async void and returns at the first await, so a double-click issued the operation
/// twice (AUD-E-001).
/// </summary>
public sealed class CommandReentrancyTests
{
    [Fact]
    public async Task AsyncRelayCommand_RefusesReentryWhileTheHandlerIsRunning()
    {
        var gate = new TaskCompletionSource();
        var invocations = 0;
        var command = new AsyncRelayCommand(async () =>
        {
            Interlocked.Increment(ref invocations);
            await gate.Task;
        });

        command.Execute(null);   // first click
        command.Execute(null);   // second click while still in flight
        command.Execute(null);

        Assert.False(command.CanExecute(null));
        Assert.Equal(1, invocations);

        gate.SetResult();
        await Task.Delay(50);

        Assert.True(command.CanExecute(null));
        command.Execute(null);
        Assert.Equal(2, invocations);
    }

    [Fact]
    public async Task AsyncRelayCommand_HandlerFailureDoesNotEscapeAndReleasesTheCommand()
    {
        Exception? reported = null;
        var command = new AsyncRelayCommand(
            () => Task.FromException(new InvalidOperationException("boom")),
            onError: ex => reported = ex);

        command.Execute(null);
        await Task.Delay(50);

        Assert.IsType<InvalidOperationException>(reported);
        Assert.True(command.CanExecute(null));
    }
}
