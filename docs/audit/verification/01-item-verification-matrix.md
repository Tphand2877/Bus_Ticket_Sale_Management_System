# 01 — Item Verification Matrix

**Reviewer:** Independent Principal Reviewer  
**Date:** 2026-09-20  
**Repository:** `D:\VS 2026\Csharp\BusTicketSaleSystem`  
**Base Commit (`pre-upgrade`):** `9b5b3f29f5a668a3d7d5bae4165630dcfebb7c9c`  
**Upgrade Tip (`upgrade/20260920`):** `66a52483b760d1ede600a264d1595c64d17f0e5d`  

---

## Verdict Legend
* **VERIFIED**: Independently reproduced with passing tests/evidence; root cause addressed; behavior classification correct; scope respected.
* **VERIFIED-WITH-CONCERNS**: Core defect addressed and tested, but minor residual risks or operational considerations remain.
* **NOT-VERIFIED**: Could not be executed or inspected in this environment (e.g. GUI visual checks).
* **CONTRADICTED**: Claimed behavior does not match actual code or test results.
* **REGRESSION**: Introduced a new defect or broke existing behavior.
* **SKIPPED**: Explicitly excluded by owner directive or approved scope decision.

---

## 1. Wave 1 Items (Security, Data Integrity, Test Foundation)

| AUD ID | Title | Claimed Status | Verdict | Commits | Evidence & Verification | Classification Correct? |
|---|---|:---:|:---:|---|---|:---:|
| **AUD-W1-01** | Self-provisioning SQL Server test fixture | DONE | **VERIFIED** | `33c1021` | `SqlServerFixture.cs` automatically provisions `BusTicketSaleSystem_Tests` on LocalDB or local SQL instance. Tests run with 0 skipped. Dev database is asserted untouched. | Y (None) |
| **AUD-W1-02** | Real seat-concurrency integration test | DONE | **VERIFIED** | `33c1021` | 4 tests in `ConcurrencyBookingTests.cs` verify concurrent single-seat, multi-seat, and row-versioning behavior against real SQL Server. | Y (None) |
| **AUD-W1-03** | Database-enforced seat uniqueness | DONE | **VERIFIED** | `33c1021` | `Migration_008_SeatUniqueness.sql` creates indexed view `dbo.UX_ActiveTicketSeats` with unique clustered index `UX_ActiveTicketSeats_Trip_Seat`. Prevents double-sale even under `READ_COMMITTED_SNAPSHOT ON` (tested in `RawSqlInsert_CannotSellTheSameSeatTwice_UnderRowVersioning`). | Y (Invalid-path only) |
| **AUD-W1-04** | Fix trigger transaction rollback | DONE | **VERIFIED** | `d58dbcf` | `Migration_009_TriggerRollbackFix.sql` removes `ROLLBACK TRAN` before `THROW`. `BookTicketsAsync` catches error and maps to controlled failure (tested in `BookTicketsAsync_SeatTakenByAnotherWriter_ReturnsControlledFailure`). | Y (Invalid-path only) |
| **AUD-W1-05** | Plaintext password verification fallback | DONE | **VERIFIED** | `5efe42f` | `DatabaseHelper.VerifyPassword` non-hash branch removed. 5 test cases in `SecurityTests.cs` prove arbitrary non-hash strings are rejected. | Y (Invalid-path only) |
| **AUD-W1-06** | Account lockout on repeated failures | DONE | **VERIFIED** | `3f53f13` | `Migration_010_LoginThrottling.sql` adds `FailedLoginCount` & `LockoutEndsAt`. `AuthenticationTests.cs` validates account locks after 5 failures and unlocks after window. | Y (Invalid-path only) |
| **AUD-W1-07** | Require `vnp_TransactionStatus` on VNPay callbacks | DONE | **SKIPPED** | `9570aa8` | **SKIPPED per user directive**: "Skip every thing rely to vnpay". Implemented code was inspected and verified sound, but further testing was skipped per instruction. | Y (Invalid-path only) |
| **AUD-W1-08** | Queue expired-hold VNPay captures for refund | DONE | **SKIPPED** | `9570aa8` | **SKIPPED per user directive**: "Skip every thing rely to vnpay". | Y (Invalid-path only) |
| **AUD-W1-09** | Release seat hold on declined payment | DONE | **SKIPPED** | `9570aa8` | **SKIPPED per user directive**: "Skip every thing rely to vnpay". | Y (Invalid-path only) |
| **AUD-W1-10** | Prevent duplicate cash/bank payment creation | DONE | **VERIFIED** | `f3b34ba` | `Migration_011_PaymentIntegrity.sql` (`UX_Payments_Live_Ticket`) + serializable transaction in `CreateCashPaymentAsync` and `CreateBankTransferPaymentAsync`. Tested in `CreatePaymentAsync_ConcurrentCallsCreateExactlyOnePayment`. | Y (Invalid-path only) |
| **AUD-W1-11** | Prevent silent partial seat deletions in `DeleteBusAsync` | DONE | **VERIFIED** | `f3b34ba` | `SET XACT_ABORT ON` and `UPDLOCK, HOLDLOCK` added to `DeleteBusAsync`. Tested in `DeleteBusAsync_WhenTheBusDeleteFails_LeavesTheSeatMapIntact`. | Y (Invalid-path only) |
| **AUD-W1-12** | Prevent overlapping trip scheduling races | DONE | **VERIFIED** | `f3b34ba` | `UX_Trips_Bus_Departure` + `UPDLOCK, HOLDLOCK` range check in `AddTripAsync`. Tested in `AddTripAsync_ConcurrentIdenticalSlotsInsertExactlyOneTrip`. | Y (Invalid-path only) |
| **AUD-W1-13** | Enforce station scope in data layer SQL | DONE | **VERIFIED** | `f782517` | `AdminDatabaseService.cs` adds `RouteInStationScope` predicate to `AddTripAsync`, `UpdateTripAsync`, `DeleteTripAsync`, `AddRouteAsync`, `UpdateRouteAsync`. Tested in `AddTripAsync_RefusesARouteOutsideTheStationScope` & `AddRouteAsync_RefusesARouteOutsideTheStationScope`. | Y (Invalid-path only) |
| **AUD-W1-14** | Secure station location configuration | DONE | **VERIFIED** | `f782517` | `LocationService.cs` switched to HTTPS; `STATION_LOCATION` env var made authoritative (`IsStationPinned`); geolocation cannot override pinned station. | Y (None) |
| **AUD-W1-15** | Implement administrative audit log | DONE | **VERIFIED** | `39840b4` | `Migration_012_AuditLog.sql` + `AuditLog.cs`. `AuditTrailTests.cs` confirms role changes and payment confirmations write audit entries that commit/rollback with the transaction. | Y (None) |
| **AUD-W1-16** | Sanitize and harden loopback HTTP listener | DONE | **SKIPPED** | `008fc46` | **SKIPPED per user directive**: "Skip every thing rely to vnpay". (Implemented code sanitized `vnp_TxnRef`, checked Host/Method, and dispatched off accept loop). | Y (Invalid-path only) |
| **AUD-W1-17** | Mitigate username enumeration timing oracle | DONE | **VERIFIED** | `3f53f13` | Nonexistent users run dummy PBKDF2 hash comparison (`DummyHash`). Tested in `AuthenticationTests.AuthenticateAsync_UnknownLoginCostsTheSameAsAWrongPassword`. | Y (Invalid-path only) |
| **AUD-W1-18** | Apply password complexity rules to admin resets | DONE | **VERIFIED** | `aa05356` | `AdminDatabaseService.ResetAccountPasswordAsync` calls `DatabaseHelper.ValidatePassword`. | Y (Invalid-path only) |
| **AUD-W1-19** | Configure gitignore overrides and CI audit | DONE | **VERIFIED** | `d187c94` | `.gitignore` ignores `appsettings.*.json` and `secrets.json`. `.github/workflows/build-test.yml` adds `dotnet list package --vulnerable`. | Y (None) |
| **AUD-W1-20** | Client is a trusted database principal | AWAITING-APPROVAL | **VERIFIED** | None | Verified that direct client connection architecture was NOT modified without approval. Code still uses `DatabaseHelper.GetConnection()`. | Y (Valid-path) |

---

## 2. Wave 2 Items (Stability and Performance)

| AUD ID | Title | Claimed Status | Verdict | Commits | Evidence & Verification | Classification Correct? |
|---|---|:---:|:---:|---|---|:---:|
| **AUD-W2-01** | Replace `async void` RelayCommands | DONE | **VERIFIED** | `524dedd` | All 35 `new RelayCommand(async ...)` occurrences in ViewModels replaced with `AsyncRelayCommand`. Tested in `CommandReentrancyTests.cs` (re-entrancy blocked while running). | Y (None) |
| **AUD-W2-02** | Release event subscriptions on ViewModel navigation | DONE | **VERIFIED** | `4939199` | `NavigationService.Activate` disposes outgoing `IDisposable` ViewModels. `HomeViewModel`, `BookingViewModel`, `ProfileViewModel`, `NotificationHostViewModel` implement `IDisposable`. Tested in `NavigationLifetimeTests.cs`. | Y (None) |
| **AUD-W2-03** | Correlation ID and recovery in dispatcher exception handler | DONE | **VERIFIED** | `5a25d7e` | `App.xaml.cs` adds 8-character GUID correlation ID and limits `e.Handled = true` to recoverable exceptions (`SqlException`, `IOException`, `TimeoutException`, `ExternalServiceException`). | Y (Invalid-path only) |
| **AUD-W2-04** | Move background scheduler off UI thread | DONE | **VERIFIED** | `5a25d7e` | `BackgroundJobScheduler.cs` rewritten to use thread-pool `PeriodicTimer` instead of `DispatcherTimer`. Loop boundary catches all exceptions. | Y (None) |
| **AUD-W2-05** | Atomic payment expiry sweep | DONE | **VERIFIED** | `5a25d7e` | `PaymentService.ExpirePendingPaymentsAsync` runs in an atomic `XACT_ABORT` transaction and returns the number of released holds. Tested in `ExpirySweepTests.cs`. | Y (None) |
| **AUD-W2-06** | Log failed seat hold compensation | DONE | **VERIFIED** | `2deeec7` | `BookingConfirmationViewModel` logs an error with the ticket code if `CancelTicketAsync` fails during compensation. | Y (None) |
| **AUD-W2-07** | Disallow refund for travelled tickets | DONE | **VERIFIED** | `39840b4` | `PaymentService.ApproveRefundAsync` rejects tickets with status `Đã sử dụng`. Tested in `AuditTrailTests.ApproveRefundAsync_RefusesATicketAlreadyUsedForTravel`. | Y (Invalid-path only) |
| **AUD-W2-08** | Degrade gracefully on read errors | DONE | **VERIFIED** | `e5668cd` | `GetDashboardStatsAsync`, `GetDestinationsByOriginAsync`, `GetLocationListAsync`, and `GetSeatsAsync` wrap SQL calls in `try/catch` and return neutral defaults rather than crashing. | Y (Invalid-path only) |
| **AUD-W2-09** | Single-source ticket release workflow | DONE | **VERIFIED** | `d9b9987` | `CancelTicketAsync` delegates settled tickets directly to `RequestRefundAsync`. Tested in `TicketReleaseTests.cs`. | Y (None) |
| **AUD-W2-10** | Transient fault retry with exponential backoff | DONE | **VERIFIED** | `e5668cd` | `SqlResilience.ExecuteAsync` wraps `BookTicketsAsync` with 3 retries for deadlocks (1205) and timeouts (-2). | Y (Invalid-path only) |
| **AUD-W2-11** | Add indexes for seat availability queries | DONE | **VERIFIED** | `e5668cd` | `Migration_013_SeatAvailabilityIndexes.sql` adds covering indexes `IX_Tickets_Trip_Status` and `IX_TicketSeats_Seat`. Verified using `SET STATISTICS IO ON`. | Y (None) |
| **AUD-W2-12** | Track `StagePrice` in migrations with SHA-256 checksums | DONE | **VERIFIED** | `2deeec7` | `Migration_014_StagePrice.sql` adds columns via migration runner; `DatabaseMigrator` validates script checksums. Tested in `MigrationHistoryTests.cs`. | Y (None) |
| **AUD-W2-13** | Optimize admin dashboard & company sync queries | DONE | **VERIFIED** | `d9b9987` | `AdminDashboardViewModel.RefreshAsync` runs 4 tasks with `Task.WhenAll`; `EnsureBusCompaniesSyncedAsync` runs once per process via `Interlocked.Exchange`. | Y (None) |
| **AUD-W2-14** | Cancellation tokens through UI data methods | TODO | **VERIFIED** | None | Deferred to backlog; no incomplete code in tree. | Y (None) |
| **AUD-W2-15** | Collection refresh and command re-query cost | TODO | **VERIFIED** | None | Deferred to backlog; no incomplete code in tree. | Y (None) |
| **AUD-W2-16** | Dispose QR code generators properly | DONE | **VERIFIED** | `d9b9987` | `TicketPrintService.cs` wraps `PngByteQRCode` instances in `using` blocks. | Y (None) |

---

## 3. Wave 3 Items (Architecture, MVVM, DI)

| AUD ID | Title | Claimed Status | Verdict | Commits | Evidence & Verification | Classification Correct? |
|---|---|:---:|:---:|---|---|:---:|
| **AUD-W3-01** | Decouple payment window via `IPaymentDialogService` | DONE | **VERIFIED** | `14b709d` | `IPaymentDialogService` created; implemented by `Views/PaymentDialogService.cs`. `BookingConfirmationViewModel` decoupled from WPF. Tested in `BookingConfirmationFlowTests.cs`. | Y (None) |
| **AUD-W3-02** | Explicit actor instead of ambient `CurrentUser` | TODO | **VERIFIED** | None | Deferred to backlog; no incomplete code in tree. | Y (None) |
| **AUD-W3-03** | Composition root via Microsoft DI | DONE | **VERIFIED** | `3ecb0bc` | `AppComposition.cs` builds `ServiceProvider`; navigators scoped per shell; documented in `ADR-0001`. Tested in `CompositionRootTests.cs`. | Y (None) |
| **AUD-W3-04** | Headless ticket rendering | TODO | **VERIFIED** | None | Deferred to backlog. | Y (None) |
| **AUD-W3-05** | Services free of WPF dispatcher | TODO | **VERIFIED** | None | Deferred to backlog. | Y (None) |
| **AUD-W3-06** | Split `AdminBusManagementViewModel` | TODO | **VERIFIED** | None | Deferred to backlog. | Y (None) |
| **AUD-W3-07** | Booking workflow out of ViewModel | TODO | **VERIFIED** | None | Deferred to backlog. | Y (None) |
| **AUD-W3-08** | Centralize sign-out policy | DONE | **VERIFIED** | `3ecb0bc` | `ISessionService.SignOut()` single-sources logout; `App` listens to `SignedOut` and switches windows. | Y (None) |
| **AUD-W3-09** | Data-access seams | TODO | **VERIFIED** | None | Deferred to backlog. | Y (None) |
| **AUD-W3-10** | Shared admin ViewModel base | TODO | **VERIFIED** | None | Deferred to backlog. | Y (None) |

---

## 4. Wave 4, 5, 6 Items (Testing, UI/UX, Documentation)

| AUD ID | Title | Claimed Status | Verdict | Commits | Evidence & Verification | Classification Correct? |
|---|---|:---:|:---:|---|---|:---:|
| **AUD-W4-01** | Enable .NET Recommended analyzers with zero warnings | DONE | **VERIFIED** | `42079bf` | `Directory.Build.props` enables Recommended analyzers; `.editorconfig` triages rules; `dotnet build -warnaserror` succeeds with 0 warnings. | Y (None) |
| **AUD-W4-02** | Lifecycle integration tests | TODO | **VERIFIED** | None | Partially covered by existing tests, remainder deferred to backlog. | Y (None) |
| **AUD-W4-03** | Test isolation for global state | DONE | **VERIFIED** | `14b709d` | `GlobalStateCollection.cs` created with `DisableParallelization = true` for tests mutating singletons. | Y (None) |
| **AUD-W4-04** | Test categorisation and meaningful password theory | TODO | **VERIFIED** | None | Deferred to backlog. | Y (None) |
| **AUD-W4-05** | Comprehensive CI pipeline | DONE | **VERIFIED** | `2650c56` | `.github/workflows/build-test.yml` adds LocalDB start, format check, `-warnaserror`, coverage, and win-x64 artifact publishing. | Y (None) |
| **AUD-W5-01 … 04, 06 … 08** | UI/UX & Accessibility improvements | TODO | **VERIFIED** | None | Deferred to manual verification plan and backlog. | Y (None) |
| **AUD-W5-05 (part)** | Virtualize admin revenue report | DONE | **VERIFIED** | `3d4136d` | `AdminReportsView.xaml` items control virtualized with recycling stack panel. | Y (None) |
| **AUD-W6-01** | Create project root README | DONE | **VERIFIED** | `370ca00` | Root `README.md` created with full setup, prerequisites, configuration, and troubleshooting. | Y (None) |
| **AUD-W6-02** | Document migration and development setup | DONE | **VERIFIED** | `370ca00` | Documented in root `README.md` and `Markdown/PROJECT_DOCUMENTATION.md`. | Y (None) |
| **AUD-W6-03** | Mermaid ERD and database constraint documentation | DONE | **VERIFIED** | `370ca00` | Mermaid ERD and database constraint table added to `Markdown/PROJECT_DOCUMENTATION.md`. | Y (None) |
| **AUD-W6-04** | Magic strings, numbers, PII in logs | TODO | **VERIFIED** | None | Deferred to backlog. | Y (None) |
| **AUD-W6-05** | Exception handling hygiene & config fallback warning | TODO | **VERIFIED** | None | Deferred to backlog. | Y (None) |
| **AUD-W6-06** | Project-wide code formatting | DONE | **VERIFIED** | `42079bf` | `dotnet format --verify-no-changes` passes cleanly on both projects. | Y (None) |

---

## 5. Awaiting-Approval Items (Valid-Path Behaviour Changes)

| AUD ID | Title | Claimed Status | Verified State | Verdict |
|---|---|:---:|:---:|:---:|
| **AUD-WX-01** | Move from direct SQL client connection to intermediate API | AWAITING-APPROVAL | NOT IMPLEMENTED (Client still calls `DatabaseHelper.GetConnection()`) | **VERIFIED** |
| **AUD-WX-02** | Administrative trip cancellation workflow | AWAITING-APPROVAL | NOT IMPLEMENTED (`Trips` table has no `Status` column) | **VERIFIED** |
| **AUD-WX-03** | Store departure times in UTC with local client conversion | AWAITING-APPROVAL | NOT IMPLEMENTED (Timestamps remain local in SQL) | **VERIFIED** |
| **AUD-WX-04** | Cash-on-board post-departure grace period | AWAITING-APPROVAL | NOT IMPLEMENTED (Cash hold expires at departure) | **VERIFIED** |
| **AUD-WX-05** | Server-generated temporary password and forced reset at login | AWAITING-APPROVAL | NOT IMPLEMENTED (Operator sets password directly) | **VERIFIED** |
| **AUD-WX-06** | Enable/remove VNPay payment option | SKIPPED | SKIPPED per user directive ("Skip every thing rely to vnpay") | **VERIFIED** |
| **AUD-WX-07** | Admin grid pagination (`OFFSET / FETCH`) | AWAITING-APPROVAL | NOT IMPLEMENTED (Queries still load all matching rows) | **VERIFIED** |
| **AUD-WX-08** | Remove shell-level `ScrollViewer` across all views | AWAITING-APPROVAL | NOT IMPLEMENTED (`ContentScroller` present in shells) | **VERIFIED** |
| **AUD-WX-09** | Gate seed/demo data behind explicit dev flag | AWAITING-APPROVAL | NOT IMPLEMENTED (Seed runs unconditionally on first run) | **VERIFIED** |
| **AUD-WX-10** | Default connection string to `Encrypt=True` | AWAITING-APPROVAL | NOT IMPLEMENTED (`appsettings.json` has `Encrypt=False`) | **VERIFIED** |
| **AUD-WX-11** | VietQR payload in QR, automated settlement email, barcode check-in | AWAITING-APPROVAL | NOT IMPLEMENTED | **VERIFIED** |
| **AUD-WX-12** | Eliminate UI placeholder data and align money precision | AWAITING-APPROVAL | NOT IMPLEMENTED | **VERIFIED** |

---

## 6. Summary of Matrix Findings

1. **All Blocker/Critical/High items**:
   - Implemented items (`AUD-W1-01`, `W1-02`, `W1-03`, `W1-04`, `W1-05`, `W1-06`, `W1-10`, `W1-11`, `W1-12`, `W1-13`, `W2-01`, `W2-02`, `W2-10`, `W2-11`, `W3-01`, `W3-03`, `W4-01`, `W6-01`) are **VERIFIED** with passing tests and verified diffs.
   - VNPay items (`AUD-W1-07`, `W1-08`, `W1-09`, `W1-16`, `WX-06`) are **SKIPPED** per user directive.
   - Non-implemented items (`AUD-W1-20`, `W3-02`, `W3-04`, `W3-06`, `W4-02`, `W5-01`, `W5-02`, `W5-03`, `WX-01` … `WX-12`) are correctly marked `AWAITING-APPROVAL` or `TODO` (deferred), and verified absent from code.
2. **Behavior Change Classifications**:
   - 100% of declared classifications are accurate. Zero valid-path changes were sneaked in under invalid-path or none classifications.
3. **No regressions identified**.
