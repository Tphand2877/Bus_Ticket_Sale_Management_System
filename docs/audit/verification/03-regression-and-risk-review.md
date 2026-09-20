# 03 — Regression and Risk Review

**Reviewer:** Independent Principal Reviewer  
**Date:** 2026-09-20  
**Repository:** `D:\VS 2026\Csharp\BusTicketSaleSystem`  
**Base Commit (`pre-upgrade`):** `9b5b3f29f5a668a3d7d5bae4165630dcfebb7c9c`  
**Upgrade Tip (`upgrade/20260920`):** `66a52483b760d1ede600a264d1595c64d17f0e5d`  

---

## 1. Database and migrations deep review (Phase 4.1)

### 1.1 Added migrations inventory

| Migration Script | Step Name | Primary Objects Created / Altered | Reversible? | Destructive Operations? |
|---|---|---|:---:|:---:|
| `Migration_008_SeatUniqueness.sql` | `011_SeatUniqueness` | View `dbo.UX_ActiveTicketSeats`, Unique Clustered Index `UX_ActiveTicketSeats_Trip_Seat` | Yes (`DROP INDEX`, `DROP VIEW`) | None. Checks for existing duplicates before creation and raises error 51020 if violated. |
| `Migration_009_TriggerRollbackFix.sql` | `012_TriggerRollbackFix` | Re-creates `TR_TicketSeats_AntiDoubleSeat` and `TR_TicketSeats_ValidateSeatBus` | Yes | Drops and recreates triggers only. |
| `Migration_010_LoginThrottling.sql` | `013_LoginThrottling` | Adds `Accounts.FailedLoginCount INT NOT NULL DEFAULT 0`, `Accounts.LockoutEndsAt DATETIME2 NULL` | Yes | None. Defaulted and nullable columns. |
| `Migration_011_PaymentIntegrity.sql` | `014_PaymentIntegrity` | Unique Filtered Index `UX_Payments_Live_Ticket` on `Payments(TicketId)`, Unique Index `UX_Trips_Bus_Departure` on `Trips(BusId, DepartureTime)` | Yes | None. Filtered / unique indexes. |
| `Migration_012_AuditLog.sql` | `015_AuditLog` | Table `dbo.AuditLog`, Index `IX_AuditLog_OccurredAt` | Yes | None. Standalone table. |
| `Migration_013_SeatAvailabilityIndexes.sql` | `016_SeatAvailabilityIndexes` | Covering Index `IX_Tickets_Trip_Status` on `Tickets`, Covering Index `IX_TicketSeats_Seat` on `TicketSeats` | Yes | None. Performance indexes only. |
| `Migration_014_StagePrice.sql` | `017_StagePrice` | Adds `Buses.StagePrice DECIMAL(10,0) NULL`, `Trips.StagePrice DECIMAL(10,0) NULL` | Yes | None. Nullable columns. |

### 1.2 Dev database verification (SELECT-only)
* Current row counts in Dev database: `Accounts`: 6, `Routes`: 17, `Buses`: 7, `Trips`: 619, `Seats`: 250, `Tickets`: 57, `TicketSeats`: 63, `Payments`: 52, `AuditLog`: 0.
* Zero rows in Dev violate the new uniqueness constraint `UX_ActiveTicketSeats_Trip_Seat` (`DupCount = 0`).
* Zero rows were deleted or mutated in the Dev database during the upgrade.

### 1.3 Invariant filter verification
* The view `dbo.UX_ActiveTicketSeats` filters: `WHERE tk.Status IN (N'Đã đặt', N'Đã thanh toán', N'Đã sử dụng')`.
* When a ticket is cancelled (`Status = N'Đã hủy'`), its seats are immediately excluded from the view. This allows legitimate re-sale of previously cancelled seats, verified in `TicketReleaseTests.CancelTicketAsync_UnpaidHold_ReleasesTheSeatAndFailsItsPendingPayment`.

---

## 2. Seat-sale concurrency and mutation review (Phase 4.2)

### 2.1 Concurrency test verification
* Located in `BusTicketSaleSystem.Tests/ConcurrencyBookingTests.cs`.
* Uses real SQL Server via `SqlServerFixture` (not EF InMemory or SQLite).
* Each test caller opens a dedicated `SqlConnection` and runs in a separate Task.
* Tests:
  1. `BookTicketAsync_AllowsOnlyOneConcurrentReservation`: 12 concurrent accounts competing for 1 seat. Exactly 1 succeeds, 11 receive mapped failure message; database verifies `CountActiveSeatHoldersAsync == 1`.
  2. `BookTicketsAsync_MultiSeat_AllowsOnlyOneConcurrentReservationPerSeat`: Two 3-seat requests overlapping on 1 shared seat. Exactly 1 succeeds, 0 stranded seats.
  3. `BookTicketsAsync_SeatTakenByAnotherWriter_ReturnsControlledFailure`: Proves controlled failure when seat is taken between guard and insert.
  4. `RawSqlInsert_CannotSellTheSameSeatTwice_UnderRowVersioning`: Two direct SQL sessions under `READ_COMMITTED_SNAPSHOT ON` using `WAITFOR TIME` synchronization. Exactly 1 commits.
  5. `Database_EnforcesSeatUniquenessDeclaratively`: Asserts the existence of `UX_ActiveTicketSeats_Trip_Seat`.

### 2.2 Mutation sanity check (verified)
* **Mutation**: Dropped `UX_ActiveTicketSeats_Trip_Seat` in `BusTicketSaleSystem_Tests` during the execution of `RawSqlInsert_CannotSellTheSameSeatTwice_UnderRowVersioning`.
* **Result with protection removed**: Test **FAILED** with `Expected: 1, Actual: 2`. Both concurrent direct-SQL sessions committed, producing 2 active tickets for the same seat.
* **Result with protection restored**: Test **PASSED** with `Expected: 1, Actual: 1`. Exactly one session committed; the second was rejected by the engine.
* **Working tree status**: Restored and clean (`git status` clean).
* **Verdict**: The concurrency protection is real, authoritative, and prevents double-selling under row versioning.

### 2.3 Production write path analysis
* Repository-wide search for `INSERT INTO.*TicketSeats` confirmed that `DatabaseHelper.BookSeatsCoreAsync` (lines 763–795) is the **only** write path assigning seats in the application.
* No alternate write paths bypass this protection.

---

## 3. Security deep review (Phase 4.3)

| Security Aspect | Pre-Upgrade State | Post-Upgrade State | Reviewer Verdict |
|---|---|---|:---:|
| **Password Storage** | PBKDF2-HMAC-SHA256 (310,000 iterations, 16-byte salt, 32-byte digest). Plaintext fallback in `VerifyPassword`. | Plaintext comparison removed; non-derived hash formats return `false` (`AUD-W1-05`). Tested in `SecurityTests.cs`. | **VERIFIED** |
| **Brute-Force Protection** | Zero lockout, zero attempt counter, no failed-login logs. | Account locked for 15 minutes after 5 consecutive failures (`AUD-W1-06`). Each failure logged by account ID. Tested in `AuthenticationTests.cs`. | **VERIFIED** |
| **Username Enumeration** | Timing oracle: nonexistent accounts returned immediately before PBKDF2 derivation. | Nonexistent accounts compute PBKDF2 against fixed dummy hash (`DummyHash`), equalizing response latency (`AUD-W1-17`). Tested in `AuthenticationTests.cs`. | **VERIFIED** |
| **Admin Password Reset** | Enforced length >= 8 only. | Calls `DatabaseHelper.ValidatePassword` (requires 8-128 chars, uppercase, lowercase, digit) (`AUD-W1-18`). | **VERIFIED** |
| **Station Scope Authorization** | Enforced only in UI ViewModels; derived from unencrypted HTTP geolocation. | Enforced directly in data-layer SQL predicates (`AUD-W1-13`). Geolocation switched to HTTPS; `STATION_LOCATION` env var made authoritative (`AUD-W1-14`). Tested in `AdminDataIntegrityTests.cs`. | **VERIFIED** |
| **Audit Logging** | Local text file only. Privilege grants logged nothing. | Append-only `dbo.AuditLog` table records role changes, password resets, account deletions, and payment confirmations inside the transaction (`AUD-W1-15`). Tested in `AuditTrailTests.cs`. | **VERIFIED** |
| **SQL Injection** | Parameterized SQL throughout; compile-time string interpolation for literals only. | Verified across all `SqlCommand` instances. Zero dynamic SQL with user input. | **VERIFIED** |
| **Secrets in Repository** | None in tracked files. | `.gitignore` expanded to cover `appsettings.*.json` and `secrets.json`. CI vulnerability audit added (`AUD-W1-19`). | **VERIFIED** |
| **VNPay Loopback Listener** | Vulnerable to CRLF log injection and connection flooding. | Sanitized log output, checked `Host`/Method, dispatched off accept loop (`AUD-W1-16`). **Note:** Further VNPay work skipped per user directive. | **VERIFIED** |

---

## 4. Behavior-preservation and regression analysis (Phase 4.4)

* **Status Transitions**:
  * Ticket: `Đã đặt` -> `Đã thanh toán` -> `Đã sử dụng` / `Đã hủy`.
  * Payment: `Pending` -> `Success` -> `Refunded` / `Failed`.
  * In `PaymentService.ApproveRefundAsync`, tickets with status `Đã sử dụng` (travelled) are now refused for refund (`AUD-W2-07`). This is an `Invalid-path only` change that prevents invalid business behavior.
  * In `DatabaseHelper.CancelTicketAsync`, settled tickets are delegated to `RequestRefundAsync` (`AUD-W2-09`), consolidating preconditions without altering behavior.
* **Pricing & Calculations**:
  * All monetary calculations preserve `decimal` types. No floating-point rounding errors introduced.
* **Exception Handling**:
  * `App.xaml.cs`: `DispatcherUnhandledException` now logs an 8-character correlation GUID and only continues for recoverable IO/SQL faults (`AUD-W2-03`). Programming errors terminate the process rather than corrupting in-memory state.
* **Data Layer Degradation**:
  * `GetDashboardStatsAsync`, `GetDestinationsByOriginAsync`, `GetLocationListAsync`, and `GetSeatsAsync` wrap SQL calls in `try/catch` and return neutral defaults (`AUD-W2-08`).
* **Transient Fault Retries**:
  * `SqlResilience.ExecuteAsync` retries deadlocks (1205) and timeouts (-2) up to 3 times with backoff (`AUD-W2-10`).

---

## 5. Architecture, DI, and MVVM review (Phase 4.5)

* **Composition Root (`ADR-0001`, AUD-W3-03)**:
  * Uses `Microsoft.Extensions.DependencyInjection` 10.0.0.
  * ViewModels are registered as `Transient`.
  * `NavigationService` is registered as `Scoped` per shell window via `CreateShellScope()`.
  * `ISessionService` manages authentication state as a `Singleton` (`AUD-W3-08`).
  * Tested in `CompositionRootTests.cs` (13 navigable ViewModels resolve successfully).
* **Payment Dialog Decoupling (`AUD-W3-01`)**:
  * `BookingConfirmationViewModel` depends on `IPaymentDialogService`.
  * `PaymentWindow` is instantiated only by `Views/PaymentDialogService.cs`.
  * Full booking flow is now tested headlessly in `BookingConfirmationFlowTests.cs`.
* **ViewModel Disposal (`AUD-W2-02`)**:
  * `NavigationService.Activate` disposes outgoing ViewModels implementing `IDisposable`.
  * `HomeViewModel`, `BookingViewModel`, `ProfileViewModel`, `NotificationHostViewModel` implement `IDisposable` and unsubscribe from singletons.
  * Tested in `NavigationLifetimeTests.cs`.
* **Async Command Re-entrancy (`AUD-W2-01`)**:
  * 35 command instances migrated to `AsyncRelayCommand`.
  * Tested in `CommandReentrancyTests.cs`.

---

## 6. WPF static verification (Phase 4.6)

* `Views/AdminReportsView.xaml:144`: Virtualization enabled with `VirtualizingPanel.IsVirtualizing="True"` and `VirtualizingStackPanel`.
* `Views/PaymentWindow.xaml.cs`: Implements `IDisposable` with `GC.SuppressFinalize(this)` and disposes `_viewModel`.
* `Views/NotificationHost.xaml.cs`: Disposes `DataContext` on `Unloaded`.
* **Visual Elements (UNVERIFIED-GUI)**:
  * Visual rendering, font scaling at 150% DPI, tab order, and color contrast cannot be verified headlessly and are mapped to `04-manual-verification-plan.md`.

---

## 7. Residual risks and limitations (Phase 4.8)

1. **Direct SQL Connection (AUD-W1-20 / AUD-WX-01, Blocker)**:
   * The client connects directly to SQL Server using a shared connection string. A malicious user with database network access could theoretically bypass client checks. *Recommendation: Implement an intermediate Web API tier before external commercial release.*
2. **Timezone Disparity (AUD-WX-03, High)**:
   * Timestamps remain stored as local wall-clock time while SQL predicates compare against `SYSUTCDATETIME()`. Item remains in `AWAITING-APPROVAL`.
3. **VNPay Gateway Integration (AUD-WX-06)**:
   * Skipped per owner directive due to lack of merchant subscription. The code is hardened but disabled.
