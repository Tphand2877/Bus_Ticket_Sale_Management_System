using Xunit;

namespace BusTicketSaleSystem.Tests;

/// <summary>
/// Tests that touch process-global state — <c>CurrentUser</c>, the
/// <c>LocationService</c>/<c>LocalizationService</c> singletons and their event
/// subscriber lists — must not run beside each other. xunit parallelises distinct test
/// classes by default, which made those tests observe one another's mutations
/// (AUD-H-007).
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class GlobalStateCollection
{
    public const string Name = "GlobalState";
}
