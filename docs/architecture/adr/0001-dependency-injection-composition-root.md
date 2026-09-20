# ADR 0001 — A single dependency-injection composition root

Status: accepted (2026-09-20) · Items: AUD-A-003, AUD-A-005, AUD-A-008, AUD-A-009, AUD-A-010

## Context

Object graphs were hand-wired in two places: a ternary factory inside
`Views/MainWindow.xaml.cs` and a second one inside `ViewModels/Admin/AdminMainViewModel.cs`.
`NavigationService` fell back to `Activator.CreateInstance` when no factory matched,
`AdminMainViewModel` depended on the concrete `NavigationService` while `MainViewModel`
depended on `INavigationService`, and both shells duplicated session teardown in
code-behind.

## Problem

Adding a dependency to any ViewModel required editing two unrelated files. The
`Activator` fallback produced a ViewModel wired to a detached navigator, so a screen
rendered correctly but none of its navigation worked, with no exception and no log
entry. There was no seam at which a test or a second front end could substitute
anything.

## Decision

Introduce `Microsoft.Extensions.DependencyInjection` 10.0.0 and one composition root,
`Services/AppComposition.cs`, built in `App.OnStartup` and exposed as `App.Services`.

- ViewModels are **transient**, so a screen never shows data captured at first construction.
- `NavigationService` is **scoped**, and each shell window creates a scope via
  `CreateShellScope()`, which also points that navigator's factory at the scope's
  provider. This is what keeps the customer and admin navigation stacks independent.
- `ISessionService` is a **singleton** and is the only writer of the session; `App`
  listens for `SignedOut` and performs the single shell switch.
- `INavigationService` now requires `INotifyPropertyChanged`, removing the runtime
  type test in `MainViewModel`.
- The `Activator` fallback is gone: navigating without a factory throws an exception
  naming the ViewModel.

## Alternatives considered

- **Keep hand-wiring, extract a shared factory class.** Cheaper, but leaves every
  dependency addition a manual two-place edit and offers no scoping for per-shell
  navigators.
- **A service-locator static.** Would have preserved the ambient-state problem the
  audit already flags (`AUD-A-002`) rather than reducing it.
- **A full MVVM framework (Prism, CommunityToolkit.Mvvm).** Far larger surface than the
  single problem being solved; the project's hand-rolled `ViewModelBase`, `RelayCommand`
  and `AsyncRelayCommand` are adequate and already understood.

## Consequences

- One registration line now adds a dependency anywhere.
- ViewModel construction is testable: `CompositionRootTests` resolves all thirteen
  navigable ViewModels and asserts shell isolation.
- Cost: one new package (no transitive additions beyond what the configuration stack
  already pulled in) and a mandatory registration for each new page — a missing
  registration now fails loudly at navigation instead of silently half-working.
- `CurrentUser` remains the ambient identity read by the data layer. Replacing it with
  an explicit actor is `AUD-W3-02` and is not part of this decision.

## Evidence

- Before: `Views/MainWindow.xaml.cs:15-21`, `ViewModels/Admin/AdminMainViewModel.cs:73-81`
  (two ternary factories), `Services/NavigationService.cs:62-64` (`Activator` fallback).
- After: `Services/AppComposition.cs`, `Services/SessionService.cs`;
  `dotnet test` 112 passed / 0 failed / 0 skipped.

## Reversal

Removing the package means restoring the two ternary factories and the duplicated
logout handlers. The `INavigationService : INotifyPropertyChanged` change and the
removal of the `Activator` fallback are independent of the container and should be
kept regardless.
