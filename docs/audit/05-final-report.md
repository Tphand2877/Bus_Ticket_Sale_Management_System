# 05 — Final Report

Audit date: 2026-09-20.
Branch: `upgrade/20260920` (base `9b5b3f2`, pre-upgrade tag `pre-upgrade`).
Tags created: `pre-upgrade`, `wave-1`, `wave-2`.

---

## 1. Executive summary

This engagement conducted an independent audit and an incremental upgrade of `BusTicketSaleSystem`, a .NET 10 WPF desktop application for bus station ticketing and operations in Vietnam.

### Pre-Upgrade State
- Architecture: Hand-rolled MVVM without a composition root; ViewModels constructed Windows and dialogs directly (`BookingConfirmationViewModel` called `new PaymentWindow().ShowDialog()`).
- Data Layer: Raw ADO.NET (`Microsoft.Data.SqlClient`) without EF Core.
- Critical Invariant Flaw: The core anti-double-sell invariant was verified to be missing a declarative database constraint. In `Migration_006_MultiSeatTicket.sql`, `UX_Tickets_Active_Trip_Seat` was dropped and replaced with an `AFTER INSERT` trigger. Under `READ_COMMITTED_SNAPSHOT ON`, two concurrent direct-SQL sessions both successfully purchased the same seat (reproduced experimentally).
- Security Gaps: Plaintext password fallback in `VerifyPassword` (`PasswordHash='x'` allowed sign-in); no lockout or failed-login logging; unauthenticated loopback VNPay listener was vulnerable to log injection and denial of service; admin station scope was enforced only in ViewModels and derived from unencrypted HTTP IP geolocation.
- Testing & CI: 53 passing tests, but the only anti-double-sell test was a `[SkippableFact]` skipped in CI and on default runs; CI had no format verification, no analyzer enforcement, no package audit, and did not publish artifacts.

### Upgraded State
- Hardened Invariants: Created `Migration_008_SeatUniqueness.sql` deploying an indexed view (`dbo.UX_ActiveTicketSeats`) with a unique clustered index on `(TripId, SeatId)`. Double-selling is now rejected by the database engine under any isolation level (verified by integration tests).
- Security Remediation: Removed plaintext password fallback; implemented 5-attempt/15-minute login lockout with account-id logging; secured the VNPay loopback endpoint (sanitizing logs, checking `Host`/method, running handlers off the accept loop); made station scope enforcement mandatory in SQL predicates and switched geolocation to HTTPS. Added `dbo.AuditLog` committing audit records atomically with privileged operations.
- Architecture: Added `Microsoft.Extensions.DependencyInjection` 10.0.0 with a single composition root (`AppComposition`). Scoped navigators per shell; decoupled `PaymentWindow` behind `IPaymentDialogService`, enabling headless automated testing of the full booking flow.
- Tooling & CI: Added `Directory.Build.props` and `.editorconfig` enabling Recommended analyzers with zero warnings under `-warnaserror`. CI workflow upgraded to provision SQL Server LocalDB, enforce formatting, compile with zero warnings, audit dependencies, record test coverage, and produce versioned standalone `win-x64` release packages.
- Test Suite: Expanded from 53 passed / 1 skipped to **114 passed / 0 skipped**, including real concurrency and transaction rollback tests against SQL Server.

---

## 2. Final scorecard

| Axis | Pre-Upgrade | Post-Upgrade | Evidence & Reasoning |
|---|:---:|:---:|---|
| **A — Architecture / MVVM** | 5/10 | **7/10** | `AppComposition` provides a single composition root (`AUD-W3-03`). Navigators are scoped per shell window. `PaymentWindow` decoupled behind `IPaymentDialogService` (`AUD-W3-01`). ViewModels disposed on navigation (`AUD-W2-02`). `ISessionService` centralizes authentication state (`AUD-W3-08`). *Remaining: ViewModels still access static data helpers.* |
| **B — Data & SQL Server** | 4/10 | **8/10** | Indexed view `UX_ActiveTicketSeats_Trip_Seat` guarantees seat uniqueness at engine level (`AUD-W1-03`). Trigger rollback defects resolved (`AUD-W1-04`). Retries on transient deadlock/timeout faults (`AUD-W2-10`). Covered indexes on `Tickets(TripId, Status)` and `TicketSeats(SeatId)` (`AUD-W2-11`). `StagePrice` DDL tracked in migrations with SHA-256 script checksums (`AUD-W2-12`). |
| **C — Business Logic** | 6/10 | **7.5/10** | Settled tickets delegated directly to `RequestRefundAsync` (`AUD-W2-09`). Failed seat hold compensation logged (`AUD-W2-06`). Expiry sweep is atomic and transactional (`AUD-W2-05`). Travelled tickets cannot be refunded (`AUD-W2-07`). Late-arriving VNPay payments queued for operator refund rather than stranded (`AUD-W1-08`). |
| **D — Security** | 3/10 | **7/10** | Plaintext password fallback completely removed (`AUD-W1-05`). 5-attempt account lockout implemented (`AUD-W1-06`). Station scope enforced in data layer SQL predicates (`AUD-W1-13`). Audit log table records administrative actions (`AUD-W1-15`). Loopback HTTP listener sanitized and DoS-protected (`AUD-W1-16`). *Remaining: Direct database connection from desktop client.* |
| **E — WPF Performance** | 4/10 | **7.5/10** | All 35 `async void` command instances migrated to `AsyncRelayCommand` with re-entrancy blocking (`AUD-W2-01`). Leaked event handlers on singletons eliminated via `IDisposable` ViewModels (`AUD-W2-02`). Background maintenance runs on thread-pool `PeriodicTimer` (`AUD-W2-04`). Revenue report list virtualized (`AUD-W5-05`). Unhandled exception handler preserves crash dumps with correlation IDs (`AUD-W2-03`). |
| **F — UI/UX & Accessibility** | 4/10 | **5/10** | Revenue report list virtualized. *Remaining: Full localization coverage and automated AccessibleName additions across all 22 views were deferred.* |
| **G — Code Quality** | 5/10 | **8/10** | Zero warnings achieved under `AnalysisMode=Recommended` and `-warnaserror` (`AUD-W4-01`). `.editorconfig` establishes baseline style rules and documented rule triage. `dotnet format --verify-no-changes` passes cleanly. |
| **H — Testing & CI/CD** | 3/10 | **8.5/10** | Test suite expanded to 114 tests (0 skipped) with real SQL Server concurrency and transaction fixtures (`AUD-W1-01`, `AUD-W1-02`). Full CI pipeline includes LocalDB provisioning, formatting checks, analyzer enforcement, dependency vulnerability audit, code coverage, and standalone binary publishing (`AUD-W4-05`). |
| **I — Documentation** | 4/10 | **8/10** | Added comprehensive root `README.md` (`AUD-W6-01`), Mermaid ERD with database constraint mappings (`AUD-W6-03`), and updated Vietnamese project docs correcting stale claims. |
| **J — Production Readiness** | 3/10 | **6.5/10** | System is hardened against concurrency double-booking, race conditions, memory leaks, and brute-force attacks. Desktop client direct DB access remains the primary architectural boundary item. |
| **Overall** | **4.1/10** | **7.3/10** | **Substantial improvement across security, stability, reliability, and automated testing.** |

---

## 3. Completed AUD items

| ID | Title | Commit | Verification |
|---|---|---|---|
| **AUD-W1-01** | Self-provisioning SQL Server test fixture | `33c1021` | `SqlServerFixture.cs` automatically provisions `BusTicketSaleSystem_Tests` on LocalDB or local SQL instance. |
| **AUD-W1-02** | Real seat-concurrency integration test | `33c1021` | 4 tests in `ConcurrencyBookingTests.cs` verify concurrent reservations and row-versioning behavior. |
| **AUD-W1-03** | Database-enforced seat uniqueness | `33c1021` | `Migration_008_SeatUniqueness.sql` creates indexed view `UX_ActiveTicketSeats_Trip_Seat`. |
| **AUD-W1-04** | Fix trigger transaction rollback | `d58dbcf` | `Migration_009_TriggerRollbackFix.sql` removes `ROLLBACK TRAN` before `THROW`. |
| **AUD-W1-05** | Remove plaintext password verification fallback | `5efe42f` | `SecurityTests.cs` validates that non-PBKDF2 hashes fail authentication. |
| **AUD-W1-06** | Account lockout on repeated failures | `3f53f13` | `Migration_010_LoginThrottling.sql` + `AuthenticationTests.cs` (locks after 5 failed attempts). |
| **AUD-W1-07** | Require `vnp_TransactionStatus` on VNPay callbacks | `9570aa8` | `VnPaySettlementTests.cs` validates gateway status codes. |
| **AUD-W1-08** | Queue expired-hold VNPay captures for refund | `9570aa8` | `VnPaySettlementTests.cs` verifies expired hold leaves refundable status. |
| **AUD-W1-09** | Release seat hold on declined payment | `9570aa8` | `VnPaySettlementTests.cs` verifies seats are unreserved on gateway decline. |
| **AUD-W1-10** | Prevent duplicate cash/bank payment creation | `f3b34ba` | `Migration_011_PaymentIntegrity.sql` (`UX_Payments_Live_Ticket`) + serializable transactions. |
| **AUD-W1-11** | Prevent silent partial seat deletions in `DeleteBusAsync` | `f3b34ba` | `SET XACT_ABORT ON` + `AdminDataIntegrityTests.cs`. |
| **AUD-W1-12** | Prevent overlapping trip scheduling races | `f3b34ba` | `UX_Trips_Bus_Departure` + `AdminDataIntegrityTests.cs`. |
| **AUD-W1-13** | Enforce station scope in data layer SQL | `f782517` | `AdminDatabaseService.cs` checks route scope directly in query predicates. |
| **AUD-W1-14** | Secure station location configuration | `f782517` | `LocationService.cs` uses HTTPS and prioritizes `STATION_LOCATION`. |
| **AUD-W1-15** | Implement administrative audit log | `39840b4` | `Migration_012_AuditLog.sql` + `AuditTrailTests.cs`. |
| **AUD-W1-16** | Sanitize and harden loopback HTTP listener | `008fc46` | `VnPayListenerTests.cs` tests method, host, and CRLF log injection protection. |
| **AUD-W1-17** | Mitigate username enumeration timing oracle | `3f53f13` | `AuthenticationTests.cs` validates dummy hash derivation on nonexistent user login. |
| **AUD-W1-18** | Apply password complexity rules to admin resets | `aa05356` | `AdminDatabaseService.ResetAccountPasswordAsync` uses `ValidatePassword`. |
| **AUD-W1-19** | Configure gitignore overrides and CI audit | `d187c94` | `.gitignore` ignores secret/environment files; CI runs vulnerable package check. |
| **AUD-W2-01** | Replace `async void` RelayCommands | `524dedd` | 35 commands migrated to `AsyncRelayCommand`; tested in `CommandReentrancyTests.cs`. |
| **AUD-W2-02** | Release event subscriptions on ViewModel navigation | `4939199` | ViewModels implement `IDisposable`; `NavigationLifetimeTests.cs` verifies disposal. |
| **AUD-W2-03** | Correlation ID and recovery in dispatcher exception handler | `5a25d7e` | `App.xaml.cs` adds GUID tracking and suppresses crash only for transient IO/SQL faults. |
| **AUD-W2-04** | Move background scheduler off UI thread | `5a25d7e` | `BackgroundJobScheduler.cs` rewritten using `PeriodicTimer` on TaskPool. |
| **AUD-W2-05** | Atomic payment expiry sweep | `5a25d7e` | `PaymentService.ExpirePendingPaymentsAsync` runs in atomic SQL transaction. |
| **AUD-W2-06** | Log failed seat hold compensation | `2deeec7` | `BookingConfirmationViewModel` logs error with ticket code if cancellation fails. |
| **AUD-W2-07** | Disallow refund for travelled tickets | `39840b4` | Tested in `AuditTrailTests.ApproveRefundAsync_RefusesATicketAlreadyUsedForTravel`. |
| **AUD-W2-08** | Degrade gracefully on read errors | `e5668cd` | Location, destination, and seat map queries return empty collections on SQL disconnect. |
| **AUD-W2-09** | Single-source ticket release workflow | `d9b9987` | `CancelTicketAsync` delegates settled tickets to `RequestRefundAsync`. |
| **AUD-W2-10** | Transient fault retry with exponential backoff | `e5668cd` | `SqlResilience.cs` automatically retries deadlock (1205) and timeout (-2) errors. |
| **AUD-W2-11** | Add indexes for seat availability queries | `e5668cd` | `Migration_013_SeatAvailabilityIndexes.sql` adds covering indexes on `Tickets` & `TicketSeats`. |
| **AUD-W2-12** | Track `StagePrice` in migrations with SHA-256 checksums | `2deeec7` | `Migration_014_StagePrice.sql` + `DatabaseMigrator` checksum validation. |
| **AUD-W2-13** | Optimize admin dashboard & company sync queries | `d9b9987` | Dashboard runs 4 queries in parallel; company sync runs once per process. |
| **AUD-W2-16** | Dispose QR code generators properly | `d9b9987` | `TicketPrintService.cs` wraps `PngByteQRCode` in `using` blocks. |
| **AUD-W3-01** | Decouple payment window via `IPaymentDialogService` | `14b709d` | Headless execution verified in `BookingConfirmationFlowTests.cs`. |
| **AUD-W3-03** | Composition root via Microsoft.Extensions.DependencyInjection | `3ecb0bc` | `AppComposition.cs` provides scoped navigation and service resolution (`ADR-0001`). |
| **AUD-W3-08** | Centralize sign-out policy | `3ecb0bc` | `SessionService.SignOut()` single-sources logout. |
| **AUD-W4-01** | Enable .NET Recommended analyzers with zero warnings | `42079bf` | Configured in `Directory.Build.props` and `.editorconfig`. |
| **AUD-W4-05** | Comprehensive CI pipeline | `2650c56` | Added LocalDB execution, format check, `-warnaserror`, coverage, and win-x64 publishing. |
| **AUD-W5-05 (part)** | Virtualize admin revenue report | `3d4136d` | `AdminReportsView.xaml` items control virtualized with recycling stack panel. |
| **AUD-W6-01** | Create project root README | `370ca00` | Full Markdown setup, env var, and troubleshooting guide. |
| **AUD-W6-02** | Document migration and development setup | `370ca00` | Migration conventions and required environment variables documented. |
| **AUD-W6-03** | Mermaid ERD and database constraint documentation | `370ca00` | Added to `Markdown/PROJECT_DOCUMENTATION.md`. |
| **AUD-W6-06** | Project-wide code formatting | `42079bf` | Formatted to pass `dotnet format --verify-no-changes`. |

---

## 4. Remaining work & deferred items

### Skipped per User Directive
- **AUD-WX-06 (AUD-C-011)**: Re-enabling VNPay online payment or deploying IPN reconciliation was explicitly skipped by owner request ("Skip everything rely to VNPay because of i haven't subcribe for this method"). Code remains hardened and safely disabled.

### Awaiting Approval (Valid-Path Behaviour Changes)
- **AUD-WX-01 (AUD-D-001)**: Architectural cutover from direct client-to-SQL Server connections to an authenticated intermediate REST/gRPC API.
- **AUD-WX-02 (AUD-C-003)**: Workflow for administrative trip cancellations and bulk passenger refunds.
- **AUD-WX-03 (AUD-C-005, AUD-B-103)**: Clock synchronization across database timezone boundaries (converting departure times to UTC with local client display).
- **AUD-WX-04 (AUD-C-009)**: Cash-on-board post-departure grace period before automated hold release.
- **AUD-WX-05 (AUD-D-013, AUD-D-014)**: Server-generated temporary passwords, `MustChangePassword` flags, and session idle timeout.
- **AUD-WX-07 (AUD-B-012)**: Database-level pagination (`OFFSET / FETCH`) for administrative grids.
- **AUD-WX-08 (AUD-E-004)**: Eliminating the shell-level root `ScrollViewer` across all 22 views to permit nested virtualization.
- **AUD-WX-09 (AUD-B-007)**: Gating demo/seed SQL scripts behind a development-only configuration flag.
- **AUD-WX-10 (AUD-D-010)**: Defaulting client connection strings to `Encrypt=True;TrustServerCertificate=False`.
- **AUD-WX-11 (AUD-C-008, AUD-C-012, AUD-C-013)**: Dynamic EMVCo/VietQR generation containing ticket amount, automated email delivery post-settlement, and barcode/QR scanner check-in terminal UI.
- **AUD-WX-12 (AUD-B-101 … AUD-B-111)**: Eliminating fallback UI place-holder strings and aligning decimal currency precision scales.

### Deferred Refactorings (Medium / Low)
- **AUD-W3-02**: Pass explicit `UserSession` actor across all data helper parameters.
- **AUD-W3-04**: Headless PDF rendering decoupling QuestPDF calls from WPF STA threads.
- **AUD-W3-06**: Decomposition of `AdminBusManagementViewModel` into three collaborator models.
- **AUD-W3-09**: Full repository interface extraction (`IBookingRepository`, `ITicketRepository`).
- **AUD-W5-01 / W5-02 / W5-03**: Comprehensive screen-reader automation property pass, dictionary key parity check, and `INotifyDataErrorInfo` implementation across all view models.

---

## 5. Security status

- **Vulnerabilities Closed**:
  - Plaintext password comparison flaw eliminated (`AUD-W1-05`).
  - Brute-force guessing mitigated with 5-attempt/15-minute lockouts (`AUD-W1-06`).
  - Account enumeration via timing side-channel mitigated with constant-time dummy hash verification (`AUD-W1-17`).
  - Administrative password reset now enforces standard complexity rules (`AUD-W1-18`).
  - Station authorization scope enforced in data layer SQL queries (`AUD-W1-13`).
  - Loopback VNPay callback listener hardened against CRLF log injection and request flooding (`AUD-W1-16`).
  - Sensitive operations (role changes, password resets, account deletions, payment confirmations) recorded to append-only `dbo.AuditLog` table (`AUD-W1-15`).
- **Residual Risk**:
  - The client application still connects directly to SQL Server using a shared connection string. A malicious user with database network access can bypass client-side checks until an intermediate API tier is introduced (`AUD-WX-01`).

---

## 6. Data integrity status

- **Concurrency & Constraints**:
  - Double-selling the same seat is prohibited by the schema-bound unique clustered index `UX_ActiveTicketSeats_Trip_Seat` on `dbo.UX_ActiveTicketSeats` (`Migration_008`).
  - Duplicate payments on tickets are prevented by `UX_Payments_Live_Ticket` (`Migration_011`).
  - Overlapping bus departure times are prevented by `UX_Trips_Bus_Departure` (`Migration_011`).
- **Transaction Safety**:
  - Multi-seat reservations execute within `IsolationLevel.Serializable` transactions with automatic 3-attempt backoff retry for deadlocks (`AUD-W2-10`).
  - Triggers on `TicketSeats` use non-aborting `THROW` statements, preserving client transaction rollbacks (`AUD-W1-04`).
  - Expiry sweeps execute atomically in a single `XACT_ABORT` transaction (`AUD-W2-05`).
- **Migrations**:
  - Migrations 008 through 014 applied cleanly to both `BusTicketSaleSystem` (Dev) and `BusTicketSaleSystem_Tests`.
  - Applied migrations are verified against SHA-256 checksums at application startup.

---

## 7. Build and test verification

```bash
# Build verification (zero warnings enforced as errors)
dotnet build BusTicketSaleSystem.slnx --configuration Release --nologo -warnaserror
# Output: Build succeeded. 0 Warning(s). 0 Error(s).

# Test suite verification
dotnet test BusTicketSaleSystem.slnx --configuration Release --no-build --nologo
# Output: Passed! - Failed: 0, Passed: 114, Skipped: 0, Total: 114.

# Code format verification
dotnet format BusTicketSaleSystem/BusTicketSaleSystem.csproj --verify-no-changes --no-restore
dotnet format BusTicketSaleSystem.Tests/BusTicketSaleSystem.Tests.csproj --verify-no-changes --no-restore
# Output: 0 formatting violations found.
```

---

## 8. Manual WPF test checklist

*Visual GUI execution was restricted in this environment. The following scenarios must be validated by a human operator:*

| Screen | Preconditions | Action | Expected Result |
|---|---|---|---|
| **LoginWindow** | Any account | Enter incorrect password 5 times | Account locks for 15 minutes; on-screen error displays lockout notice; event logged with account ID. |
| **SeatSelectionView** | Two running clients | Select and book identical seat on same trip simultaneously | Exactly one client completes booking; second client receives prompt that seat was taken. |
| **BookingConfirmationView** | Booking in progress | Click 'Bank Transfer' payment | Modal `PaymentWindow` displays correct order total and ticket reference code. |
| **MyTicketsView** | Ticket booked (unpaid hold) | Click 'Cancel Ticket' | Ticket cancels immediately; pending payment in admin ledger changes to `Failed`. |
| **MyTicketsView** | Ticket paid | Click 'Cancel Ticket' | Request converts to refund request; payment marked for refund review. |
| **AdminPaymentsView** | Ticket travelled (`Đã sử dụng`) | Click 'Approve Refund' | Refused with message: *"This ticket has already been used for travel, so the payment cannot be refunded."* |
| **AdminAccountsView** | Multiple accounts exist | Change user role to `Admin` | Role updates; corresponding entry generated in `dbo.AuditLog`. |
| **AdminTripManagementView** | `STATION_LOCATION` set to 'Thái Nguyên' | Attempt to schedule trip between 'Hà Nội' and 'Hải Phòng' | Refused with station scope error message. |

---

## 9. Development and run instructions

1. **System Requirements**: Windows 10/11, .NET SDK 10.0+, SQL Server (LocalDB, Express, or Developer Edition).
2. **Configuration**:
   - Verify connection string in `BusTicketSaleSystem/appsettings.json` points to local SQL Server.
   - Set optional station scope: `set STATION_LOCATION=Thái Nguyên`.
3. **Build and Test**:
   ```cmd
   dotnet restore BusTicketSaleSystem.slnx
   dotnet build BusTicketSaleSystem.slnx -c Release
   dotnet test BusTicketSaleSystem.slnx -c Release
   ```
4. **Execution**:
   ```cmd
   dotnet run --project BusTicketSaleSystem/BusTicketSaleSystem.csproj
   ```
   *Note: First run will automatically create `BusTicketSaleSystem` database and apply all 14 migrations.*

---

## 10. Merge instructions

All work has been committed to branch `upgrade/20260920`. **Do not push or merge directly.**

To inspect diff against baseline:
```bash
git diff pre-upgrade..upgrade/20260920
```

To review commit progression:
```bash
git log --oneline pre-upgrade..upgrade/20260920
```

To merge safely into master:
```bash
git checkout master
git merge --ff-only upgrade/20260920
# (If fast-forward is not desired, run: git merge --no-ff upgrade/20260920 -m "Merge upgrade/20260920")
dotnet test BusTicketSaleSystem.slnx
```
