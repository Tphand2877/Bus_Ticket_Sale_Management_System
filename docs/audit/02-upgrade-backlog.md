# 02 — Upgrade backlog

Authoritative tracker. Ordered by execution sequence (security → data integrity → correctness → stability → performance → architecture → testing/CI → UI/UX → documentation). Every Blocker/Critical/High finding has its own item; Medium/Low items are grouped only where they share scope, dependencies, acceptance criteria and verification.

Status values: `TODO` · `IN-PROGRESS` · `DONE` · `BLOCKED` · `SKIPPED` · `AWAITING-APPROVAL`.

Behaviour change classification per R2. `Valid-path` items are **not** implemented in this session; they are recorded `AWAITING-APPROVAL` with old behaviour, proposed behaviour, reason, affected workflows, risks and required tests.

---

## Wave 1 — Test foundation, security, data integrity

### AUD-W1-01 · Self-provisioning SQL Server test fixture
- **Findings:** AUD-H-001, AUD-A-015
- **Severity:** High · **Type:** Gap
- **Evidence:** `BusTicketSaleSystem.Tests/ConcurrencyBookingTests.cs:8-13` is a `[SkippableFact]` requiring `BUS_TICKET_TEST_TRIP_ID`/`BUS_TICKET_TEST_SEAT_ID`/`BUS_TICKET_TEST_ACCOUNT_IDS`; `.github/workflows/build-test.yml:28-29` sets none. Baseline run: 53 passed, 1 skipped.
- **Impact:** The invariant declared authoritative by R5 has no executing test.
- **Proposed fix:** An xunit fixture that creates/migrates `BusTicketSaleSystem_Tests` (dropping and recreating it, which R6 permits only for the Test database), seeds a trip, seats and accounts, and exposes their ids. Never touches the Dev database.
- **Behavior change:** None · **Effort:** M · **Risk:** Low · **Dependencies:** none
- **Acceptance:** `dotnet test` provisions the test database automatically and reports 0 skipped when SQL Server is reachable; the Dev database is untouched (verified by row counts before/after).
- **Verification:** `dotnet test`; `SELECT COUNT(*)` on Dev `Tickets`/`Trips` before and after.
- **Status:** TODO

### AUD-W1-02 · Executable seat-concurrency test against real SQL Server
- **Findings:** AUD-H-001, AUD-B-017
- **Severity:** High · **Type:** Gap
- **Evidence:** as above; the existing test also covers only the single-seat overload.
- **Impact:** A regression in the Serializable transaction, the `NOT EXISTS` guard or the trigger merges green.
- **Proposed fix:** N concurrent `BookTicketsAsync` calls on one seat across separate connections (no InMemory, no SQLite), asserting exactly one success and no duplicate active seat row; plus a test asserting the DB-level uniqueness object exists.
- **Behavior change:** None · **Effort:** M · **Risk:** Low · **Dependencies:** AUD-W1-01
- **Acceptance:** Test executes (not skipped), passes, and fails if the uniqueness object is dropped.
- **Verification:** `dotnet test`; then drop the object in the test DB and observe failure.
- **Status:** TODO

### AUD-W1-03 · Database-enforced seat uniqueness
- **Findings:** AUD-B-001 (Critical)
- **Severity:** Critical · **Type:** Gap
- **Evidence:** `Data/Migration_006_MultiSeatTicket.sql:48-49` drops `UX_Tickets_Active_Trip_Seat`; `:23` the only replacement `UNIQUE` is `(TicketId, SeatId)`, which cannot constrain two different tickets. **Proven:** with `READ_COMMITTED_SNAPSHOT ON`, two concurrent direct-SQL sessions both inserted `(TripId=1, SeatId=3)` and both committed — 2 active tickets on one seat (see 01-audit-report §1).
- **Impact:** The core business invariant is isolation-dependent and absent when the UI is bypassed. R5 violation.
- **Proposed fix:** New migration adding a uniqueness object that holds regardless of isolation level — a `SCHEMABINDING` indexed view over active `TicketSeats`×`Tickets` with a unique clustered index on `(TripId, SeatId)`, or a persisted `TripId` on `TicketSeats` with a filtered unique index. Smallest correct option chosen at implementation time after checking existing data for violations.
- **Behavior change:** Invalid-path only (the rule is already evidenced by `Schema1.sql:71-72`, `Migration_005_Settlement.sql:75-76`, the trigger, and `Markdown/PROJECT_DOCUMENTATION.md:119`)
- **Effort:** M · **Risk:** Medium · **Dependencies:** AUD-W1-01, AUD-W1-02; AUD-W1-04 interacts
- **Acceptance:** Two direct SQL inserts of the same active `(TripId, SeatId)` for different tickets are refused by the engine (error 2601/2627) under both `READ_COMMITTED_SNAPSHOT` settings; all existing legitimate flows (cancel then rebook the same seat) still work.
- **Verification:** AUD-W1-02 test run with RCSI off and on; manual re-book-after-cancel check in the test DB.
- **Status:** TODO

### AUD-W1-04 · Trigger rollback corrupts the caller's transaction
- **Findings:** AUD-B-002
- **Severity:** High · **Type:** Risk
- **Evidence:** `Migration_006_MultiSeatTicket.sql:81-82` `ROLLBACK TRANSACTION; THROW 51002 …`; caller `Data/DatabaseHelper.cs:662-667` then calls `transaction.RollbackAsync()` on an already-aborted transaction inside a `catch (SqlException)` filter. Same shape via `TR_TicketSeats_ValidateSeatBus` from `AdminDatabaseService.cs`.
- **Impact:** The friendly "seat has just been taken" message is replaced by an `InvalidOperationException` escaping to the dispatcher handler.
- **Proposed fix:** Replace `ROLLBACK TRANSACTION` with a plain `THROW` in both `TicketSeats` triggers (letting `XACT_ABORT`/the caller own the rollback) and guard the managed rollback against a completed transaction.
- **Behavior change:** Invalid-path only · **Effort:** S · **Risk:** Low · **Dependencies:** AUD-W1-03
- **Acceptance:** Forcing a seat conflict returns `(false, null, "One of the selected seats has just been taken …")`, never an unhandled exception.
- **Verification:** Integration test inserting a conflicting row from a second connection, then calling `BookTicketsAsync`.
- **Status:** TODO

### AUD-W1-05 · Plaintext passwords must stop authenticating
- **Findings:** AUD-D-002 (Critical)
- **Severity:** Critical · **Type:** Gap
- **Evidence:** `Data/DatabaseHelper.cs:960-963` — when `TrySplitDerivedHash` fails, `VerifyPassword` returns `FixedTimeEquals(UTF8(password), UTF8(stored))`, i.e. a plaintext comparison.
- **Impact:** Any non-hash value in `PasswordHash` is a working password; combined with AUD-D-001 this is a one-statement admin takeover whose evidence is erased by the rehash-on-login path.
- **Proposed fix:** Return `false` when the stored value is not a recognised derived hash. Keep the legacy `PBKDF2$` branch (it is a real hash) and the rehash-on-login upgrade. Preserve user access per R6: no stored hash is modified.
- **Behavior change:** Invalid-path only · **Effort:** S · **Risk:** Low · **Dependencies:** none
- **Acceptance:** `VerifyPassword("x","x")` is false; `VerifyPassword(p, HashPassword(p))` and the `PBKDF2$` legacy format remain true; existing `SecurityTests` stay green.
- **Verification:** New cases in `BusTicketSaleSystem.Tests/SecurityTests.cs`; `dotnet test`.
- **Status:** TODO

### AUD-W1-06 · Login lockout and failed-login logging
- **Findings:** AUD-D-003
- **Severity:** High · **Type:** Gap
- **Evidence:** `Data/DatabaseHelper.cs:28-67` has no attempt counter, no backoff, no lockout and logs only `SqlException` (`:64`). Repo-wide search for `lockout|attempts|throttl|ratelimit` → 0 hits.
- **Impact:** Unlimited silent online guessing against any account including Admin, with no trace.
- **Proposed fix:** `Accounts.FailedLoginCount` + `LockoutEndsAt` via a new migration; increment on failure, refuse while locked, reset on success; `LogWarning` on every failed attempt using the account id, not the typed identifier (see AUD-W6-04).
- **Behavior change:** Invalid-path only · **Effort:** M · **Risk:** Low · **Dependencies:** AUD-W1-01 for the test fixture
- **Acceptance:** N consecutive failures lock for the configured window; a correct password inside the window is still refused; after the window a correct password succeeds and resets the counter; each failure produces exactly one warning line.
- **Verification:** Integration test against the test database; inspect `Logs/app-*.log`.
- **Status:** TODO

### AUD-W1-07 · VNPay: require `vnp_TransactionStatus`
- **Findings:** AUD-D-004 (status check portion)
- **Severity:** High · **Type:** Gap
- **Evidence:** `Services/PaymentService.cs:263` branches on `@ResponseCode=N'00'` alone; repo-wide search for `vnp_TransactionStatus` → 0 hits.
- **Impact:** A correctly signed callback with `vnp_ResponseCode=00` but a non-successful `vnp_TransactionStatus` marks the ticket paid.
- **Proposed fix:** Require `vnp_TransactionStatus == "00"` in addition; treat a missing field as failure.
- **Behavior change:** Invalid-path only · **Effort:** S · **Risk:** Low · **Dependencies:** none
- **Acceptance:** A signed callback with `ResponseCode=00, TransactionStatus=02` leaves the payment unsettled and the ticket unpaid; `00/00` settles as today.
- **Verification:** Unit test over `VerifyVnPayCallbackAsync` using the `internal` `BuildSignature` helper for both combinations.
- **Status:** TODO

### AUD-W1-08 · VNPay late-success leaves the customer charged with no ticket
- **Findings:** AUD-C-001 (Critical)
- **Severity:** Critical · **Type:** Gap
- **Evidence:** `Services/PaymentService.cs:255-262` matches only `Status=N'Pending'` **and** `t.PaymentExpiresAt>SYSUTCDATETIME()`; on zero rows the caller reports "declined or the seat hold had already expired" (`:294`) and the sweep later marks the row `Failed/EXPIRED`. No reconciliation path exists.
- **Impact:** Money captured at the gateway, no ticket, no refund record, no operator visibility.
- **Proposed fix:** When zero rows match and `ResponseCode`/`TransactionStatus` indicate success, still persist `ResponseCode`, `ProviderTransactionNo` and `RawResponse`, move the payment to a terminal state that the settlement console surfaces for manual refund, and log it.
- **Behavior change:** Invalid-path only · **Effort:** M · **Risk:** Medium · **Dependencies:** `CK_Payments_Status` (`Schema_Payments.sql:25`) if a new literal is introduced; AUD-W1-07
- **Acceptance:** A successful signed callback on an expired hold leaves a ledger row carrying the raw response, not `Pending`, listed in the admin console flagged for refund.
- **Verification:** Integration test: ticket with past `PaymentExpiresAt` + pending VNPay payment + signed success callback; assert row state and that `GetPaymentsAsync` surfaces it.
- **Status:** TODO

### AUD-W1-09 · Declined VNPay callback keeps the seat held
- **Findings:** AUD-C-004
- **Severity:** High · **Type:** Gap
- **Evidence:** `Services/PaymentService.cs:270` decline branch updates only `Payments`; `ViewModels/PaymentWindowViewModel.cs:213` tells the user the seat was released.
- **Impact:** Seat blocked up to 15 minutes, retry blocked by the payable guard (`THROW 51010/51011`), UI message contradicts stored state.
- **Proposed fix:** In the same transaction, release the hold (`Tickets → Đã hủy`, `CancelledAt`, `PaymentExpiresAt=NULL`) when the ticket is still `Đã đặt`.
- **Behavior change:** Invalid-path only · **Effort:** S · **Risk:** Low · **Dependencies:** none
- **Acceptance:** After a declined callback the seat is immediately free in `GetSeatsAsync` and the message matches stored state.
- **Verification:** Integration test: book → create VNPay payment → signed callback `ResponseCode=24` → assert ticket status and seat availability.
- **Status:** TODO

### AUD-W1-10 · Duplicate payment race for cash and bank transfer
- **Findings:** AUD-C-002
- **Severity:** High · **Type:** Risk
- **Evidence:** `Services/PaymentService.cs:60-67` guard + `:93-103`/`:126-136` execute with no explicit transaction, unlike the VNPay path at `:173`. In autocommit the `UPDLOCK,HOLDLOCK` is released at statement end.
- **Impact:** Two concurrent create-payment calls both insert `Pending` rows; `GetPaymentStatsAsync` double-counts and an operator can confirm one while the other lingers.
- **Proposed fix:** Wrap both batches in the same Serializable transaction the VNPay path uses, and/or add a filtered unique index on `Payments(TicketId) WHERE Status IN (N'Pending',N'Success')` mapping 2601/2627 to the existing 51011 message.
- **Behavior change:** Invalid-path only · **Effort:** S · **Risk:** Low · **Dependencies:** AUD-W1-01
- **Acceptance:** Two parallel `CreatePaymentAsync` calls on one ticket yield exactly one `Pending` row; the loser gets the existing friendly message.
- **Verification:** Concurrency test looping parallel calls and asserting a single row each iteration.
- **Status:** TODO

### AUD-W1-11 · `DeleteBusAsync` can wipe a seat map and report success
- **Findings:** AUD-B-004
- **Severity:** High · **Type:** Risk
- **Evidence:** `Data/AdminDatabaseService.cs:928-935` — `BEGIN TRANSACTION; DELETE FROM Seats …; DELETE FROM Buses …; COMMIT; SELECT 0;` with no `SET XACT_ABORT ON` and no TRY/CATCH, unlike `DatabaseHelper.cs:678`.
- **Impact:** A non-batch-aborting error on the second delete commits the first: the bus survives with its entire seat map destroyed, and the caller is told it succeeded.
- **Proposed fix:** `SET XACT_ABORT ON` at the top of the batch and take the preflight checks under `UPDLOCK, HOLDLOCK`.
- **Behavior change:** Invalid-path only · **Effort:** S · **Risk:** Low · **Dependencies:** none
- **Acceptance:** With the bus delete blocked, `Seats` is unchanged and the call reports failure.
- **Verification:** Integration test adding a temporary blocking FK, calling `DeleteBusAsync`, asserting seat count and `Success=false`.
- **Status:** TODO

### AUD-W1-12 · Unlocked check-then-insert for trip overlap
- **Findings:** AUD-B-005
- **Severity:** High · **Type:** Gap
- **Evidence:** `Data/AdminDatabaseService.cs:985-989` — `INSERT … WHERE NOT EXISTS (overlap)` with no transaction, no `HOLDLOCK`, no supporting constraint.
- **Impact:** Two admins (or the auto-scheduler at `:855`) can both pass the check, double-booking one bus onto overlapping trips — which then double-sells the same physical seats through two `TripId`s.
- **Proposed fix:** Wrap check+insert in a Serializable transaction (or `WITH (UPDLOCK, HOLDLOCK)` on the subquery) and add `UNIQUE (BusId, DepartureTime)` after de-duplicating existing rows.
- **Behavior change:** Invalid-path only · **Effort:** S · **Risk:** Medium (existing duplicates) · **Dependencies:** none
- **Acceptance:** Ten concurrent `AddTripAsync` calls for one slot insert exactly one row.
- **Verification:** Concurrency test; pre-check query for existing duplicates before adding the constraint.
- **Status:** TODO

### AUD-W1-13 · Station scope enforced only in ViewModels
- **Findings:** AUD-D-005
- **Severity:** High · **Type:** Gap
- **Evidence:** `ViewModels/Admin/AdminTripManagementViewModel.cs:156-162` and `AdminRouteManagementViewModel.cs:145-150, :172-176` hold the only scope checks; `AdminDatabaseService.cs:271, :302, :979, :1018, :1079` call `RequireAdmin()` only.
- **Impact:** The documented per-station authorization model is bypassed by any path not going through those two ViewModels. The check is also a substring match that over-matches province names.
- **Proposed fix:** Pass the scope into the data layer and enforce it in the SQL predicate; source it from the account, not the workstation (see AUD-W1-14).
- **Behavior change:** Invalid-path only · **Effort:** M · **Risk:** Medium · **Dependencies:** AUD-W1-14
- **Acceptance:** The five admin methods invoked directly with an out-of-scope route refuse and mutate nothing.
- **Verification:** Integration test with an admin scoped to station A operating on a B↔C route.
- **Status:** TODO

### AUD-W1-14 · Admin station scope derives from cleartext IP geolocation
- **Findings:** AUD-D-008
- **Severity:** Medium (security-critical dependency of W1-13) · **Type:** Risk
- **Evidence:** `Services/LocationService.cs:135` `GetStringAsync("http://ip-api.com/json/…")` (plain HTTP) → `:148` assigns `MachineLocation` → `:57-59` `CurrentLocation => CurrentUser.IsAdmin ? MachineLocation : …`.
- **Impact:** An on-path attacker or hostile DNS/DHCP can set an operator's station to any province, changing what the admin console permits. Also discloses the workstation's public IP/location to a third party at every launch.
- **Proposed fix (implementable part):** Stop letting IP detection feed `CurrentLocation` for Admin sessions; honour `STATION_LOCATION` as authoritative; switch the lookup to HTTPS and treat it as a non-security hint.
- **Behavior change:** Valid-path (admin station source changes) → the account-sourced station is **AWAITING-APPROVAL**; the HTTPS switch and "never authoritative for Admin" hardening are Invalid-path only and are in scope.
- **Effort:** M · **Risk:** Medium · **Dependencies:** none for the hardening part
- **Acceptance:** With the geolocation endpoint blocked or returning an arbitrary city, an Admin session's effective station is unchanged; no cleartext HTTP request is issued.
- **Verification:** Run with the endpoint blocked and with a local responder returning a different city; assert `AdminMainViewModel.StationScope`.
- **Status:** TODO (hardening) / AWAITING-APPROVAL (account-sourced station)

### AUD-W1-15 · Audit trail for privileged operations
- **Findings:** AUD-D-009
- **Severity:** Medium · **Type:** Gap
- **Evidence:** `Data/AdminDatabaseService.cs:1122-1143` `UpdateAccountRoleAsync` logs only on `SqlException`; the success path writes nothing. Route/bus/trip DML logs only failures. `Services/LoggerService.cs:9-20` — the only sink is a local rolling text file.
- **Impact:** Promoting an account to Admin leaves no trace; master-data tampering is invisible; the log file is rewritable by the app, any local user, and the AUD-D-006 injection path.
- **Proposed fix:** Append-only `AuditLog` table written inside the same transaction as each sensitive mutation (role change, password reset/change, account delete, settlement confirm/reject/refund, check-in, route/bus/trip DML).
- **Behavior change:** None (additive) · **Effort:** M · **Risk:** Low · **Dependencies:** new migration
- **Acceptance:** Each listed operation writes exactly one audit row naming actor and target; rolling back the operation rolls back the audit row.
- **Verification:** Integration test per operation.
- **Status:** TODO

### AUD-W1-16 · Log injection from the unauthenticated loopback endpoint
- **Findings:** AUD-D-006, AUD-D-007
- **Severity:** Medium · **Type:** Risk
- **Evidence:** `Services/VnPayCallbackListener.cs:129-132` logs request-derived `vnp_TxnRef` **before** signature verification (`:141`); `:206` URL-decodes so `%0D%0A` becomes a real CRLF; `:91-125` awaits each handler inside the accept loop, with no method/`Host`/rate checks.
- **Impact:** Any local process or any web page the operator visits can forge multi-line log entries into the system's only audit trail, and can wedge the settlement channel with one slow request.
- **Proposed fix:** Move the log call after verification; sanitise and length-cap request-derived values (or use structured Serilog properties); reject non-GET and foreign `Host`; dispatch handlers with bounded concurrency.
- **Behavior change:** Invalid-path only · **Effort:** S · **Risk:** Low · **Dependencies:** none
- **Acceptance:** A request with `vnp_TxnRef=a%0D%0A…forged` produces one sanitised line and none before verification fails; 100 concurrent bogus requests do not delay a valid callback.
- **Verification:** Throwaway harness issuing crafted and concurrent loopback requests; inspect `Logs/app-*.log`.
- **Status:** TODO

### AUD-W1-17 · Username-enumeration timing oracle
- **Findings:** AUD-D-011
- **Severity:** Medium · **Type:** Gap
- **Evidence:** `Data/DatabaseHelper.cs:43` returns before any key derivation when no row matches; `:53` pays the full 310k-iteration cost when a row exists.
- **Impact:** Reliable account enumeration, silent (no lockout, no failed-login log).
- **Proposed fix:** Derive against a fixed dummy hash with the same parameters on the miss path.
- **Behavior change:** Invalid-path only · **Effort:** S · **Risk:** Low · **Dependencies:** AUD-W1-06
- **Acceptance:** Mean latency for an unknown login is within noise of a known login with a wrong password.
- **Verification:** Throwaway benchmark, 200 iterations each branch.
- **Status:** TODO

### AUD-W1-18 · Admin password reset bypasses the password policy
- **Findings:** AUD-D-013
- **Severity:** Low (security) · **Type:** Gap
- **Evidence:** `Data/AdminDatabaseService.cs:1149-1150` checks length ≥ 8 only, versus the real policy at `Data/DatabaseHelper.cs:1031-1035`.
- **Impact:** An administrator can set `"password"` on any account, bypassing the rule every other path enforces.
- **Proposed fix:** Call the same validator (make it `internal`).
- **Behavior change:** Invalid-path only · **Effort:** S · **Risk:** Low · **Dependencies:** none
- **Acceptance:** `ResetAccountPasswordAsync(id, "password")` is refused with the policy message.
- **Verification:** xunit test.
- **Status:** TODO
- **Not in scope here (Valid-path):** generating the temporary secret server-side and forcing a change at next login → AUD-WX-05.

### AUD-W1-19 · `.gitignore` and CI supply-chain gaps
- **Findings:** AUD-D-015
- **Severity:** Low · **Type:** Gap
- **Evidence:** `.gitignore` excludes `*.env` and `[Ll]ogs/` but not `appsettings*.json` overrides or `secrets.json`; `.github/workflows/build-test.yml` has no secret scan and no `dotnet list package --vulnerable`.
- **Impact:** The one tracked, runtime-honoured config file is the natural place a password will be pasted, and nothing would stop that commit.
- **Proposed fix:** Ignore `appsettings.Development.json`/`appsettings.*.local.json`/`secrets.json`; add a vulnerability-audit CI step.
- **Behavior change:** None · **Effort:** S · **Risk:** Low · **Dependencies:** none
- **Acceptance:** A local `appsettings.Development.json` shows as ignored; CI fails on a vulnerable package.
- **Verification:** `git status`; CI run.
- **Status:** TODO

### AUD-W1-20 · Client is a trusted database principal
- **Findings:** AUD-D-001 (Blocker)
- **Severity:** Blocker · **Type:** Risk
- **Evidence:** `Data/DatabaseHelper.cs:26`, `Services/AppConfig.cs:13-16`, `Data/AdminDatabaseService.cs:10-14`, `Models/UserSession.cs:11-14`.
- **Impact:** Every authorization, ownership filter and revenue rule is advisory: the same Windows identity can run `UPDATE Accounts SET Role=N'Admin'` from SSMS, and `CurrentUser.Account` is a settable public static.
- **Proposed fix:** Revoke direct DML from the application login and expose writes as stored procedures that re-derive the caller and role server-side; strategically, an authenticated service between client and database.
- **Behavior change:** None for legitimate flows; Invalid-path only for bypass attempts. **However** the change replaces the deployment and permission model of the whole product.
- **Effort:** XL · **Risk:** High · **Dependencies:** SQL Server login/role design; touches every data method
- **Acceptance:** A plain application principal cannot mutate `Accounts`/`Payments`/`Tickets` rows it does not own, proven from a direct SQL session; every admin operation fails server-side for a non-admin principal.
- **Verification:** Direct SQL session as a non-sysadmin application login attempting the three mutations.
- **Status:** AWAITING-APPROVAL — architectural change of this size is outside an incremental session and requires an explicit decision on the deployment model (stored-procedure surface versus an application server). Mitigations W1-05, W1-06, W1-13, W1-15 reduce, but do not remove, the exposure.

---

## Wave 2 — Stability and correctness

### AUD-W2-01 · Async-void command handlers
- **Findings:** AUD-E-001 · **Severity:** High · **Type:** Risk
- **Evidence:** 35 `new RelayCommand(async …)` sites (e.g. `ViewModels/Admin/AdminBusManagementViewModel.cs:319-322, :365`, `LoginViewModel.cs:44`, `HomeViewModel.cs:96-97`) while the correct `Common/AsyncRelayCommand.cs` is used once (`BookingConfirmationViewModel.cs:89`).
- **Impact:** Buttons are re-clickable during the await (double bus registration, double sign-in), and uncaught faults reach the dispatcher.
- **Fix:** Replace each with `AsyncRelayCommand`. **Behavior change:** None · **Effort:** M · **Risk:** Low
- **Acceptance:** No `RelayCommand(async` remains outside the doc comment; a rapid double-click produces one effect.
- **Verification:** `dotnet test`; manual double-click checklist rows (Wave 2 manual checklist).
- **Status:** TODO

### AUD-W2-02 · Singleton event subscriptions leak ViewModels
- **Findings:** AUD-E-002, AUD-E-003, AUD-A-014, AUD-E-005 · **Severity:** High · **Type:** Risk
- **Evidence:** lambda subscriptions with no `-=` at `HomeViewModel.cs:83-90`, `BookingViewModel.cs:42-46`, `ProfileViewModel.cs:58-62`, `MainViewModel.cs:76-87`, `AdminMainViewModel.cs:62-71`, `NotificationHostViewModel.cs:23`; `NavigationService.cs:78-86` never disposes the outgoing ViewModel; `SeatSelectionViewModel`'s 20 s timer stops only via the View's `Unloaded`.
- **Impact:** One leaked ViewModel per navigation, each still running DB queries on location changes; toast dispatch cost grows per login cycle; the seat poll can outlive its screen.
- **Fix:** Named handlers + `IDisposable` on the affected ViewModels; `NavigationService.Activate` disposes the outgoing non-singleton ViewModel; drop the View `Unloaded` workaround. `PaymentWindowViewModel` is the reference implementation.
- **Behavior change:** None · **Effort:** M · **Risk:** Medium · **Dependencies:** AUD-W2-01
- **Acceptance:** After 20 Home↔Booking navigations exactly one live instance of each remains; no `GetSeatsAsync` activity after leaving seat selection.
- **Verification:** Instance counter logged on dispose + a 60 s observation after navigating away.
- **Status:** TODO

### AUD-W2-03 · Dispatcher handler marks every exception handled
- **Findings:** AUD-E-011, AUD-G-005 · **Severity:** Medium · **Type:** Risk
- **Evidence:** `App.xaml.cs:50-55` logs, shows a generic `MessageBox` and sets `e.Handled = true` unconditionally.
- **Impact:** The app continues after arbitrary state corruption; the user gets a message with no correlation id.
- **Fix:** Handle the recoverable types (`SqlException`, `IOException`, `ExternalServiceException`), add a correlation GUID shown in the dialog and written to the log, let the rest terminate after flushing.
- **Behavior change:** Invalid-path only · **Effort:** S · **Risk:** Medium · **Dependencies:** AUD-W2-01
- **Acceptance:** Dialog text contains an id present verbatim in `Logs/app-*.log`; a deliberate `NullReferenceException` terminates, a transient SQL failure continues.
- **Verification:** Throwaway build with a button throwing each type.
- **Status:** TODO

### AUD-W2-04 · Background scheduler on the UI dispatcher
- **Findings:** AUD-E-006 · **Severity:** Medium · **Type:** Risk
- **Evidence:** `Services/BackgroundJobScheduler.cs:19-25` `DispatcherTimer`; `:35` `async void OnTick`; `:51-58` catches only `SqlException`/`InvalidOperationException`.
- **Impact:** Housekeeping competes with rendering every 60 s; any other exception type escapes an `async void` handler.
- **Fix:** `PeriodicTimer` on the thread pool, keep the `Interlocked` overlap guard, catch at the loop boundary and log.
- **Behavior change:** None · **Effort:** S · **Risk:** Low
- **Acceptance:** A forced non-SQL failure is logged and the next pass still runs.
- **Verification:** Point a pass at an unreachable server; observe log and continued operation.
- **Status:** TODO

### AUD-W2-05 · Expiry sweep is not atomic
- **Findings:** AUD-C-010, AUD-B-016 · **Severity:** Medium · **Type:** Risk
- **Evidence:** `Services/PaymentService.cs:334-341` — two dependent `UPDATE`s with no transaction; the returned row count conflates both.
- **Impact:** A drop between statements leaves payments `Failed/EXPIRED` while their tickets still hold seats. The sweep also runs unbounded on the booking hot path (`DatabaseHelper.cs:575`).
- **Fix:** `SET XACT_ABORT ON; BEGIN TRANSACTION … COMMIT` around both statements; report the two counts separately.
- **Behavior change:** None · **Effort:** S · **Risk:** Low
- **Acceptance:** Rolling the batch back leaves both tables unchanged; the log line reports a usable count.
- **Verification:** Integration test wrapping the sweep in a rolled-back transaction.
- **Status:** TODO

### AUD-W2-06 · Booking→payment compensation result discarded
- **Findings:** AUD-C-007 · **Severity:** Medium · **Type:** Risk
- **Evidence:** `ViewModels/BookingConfirmationViewModel.cs:118-135` — `CancelTicketAsync` result is ignored.
- **Impact:** A failed compensation leaves an unsellable hold with no trace.
- **Fix:** Inspect the result and log an error naming the ticket code when compensation fails.
- **Behavior change:** None (crash/failure path only) · **Effort:** S · **Risk:** Low
- **Acceptance:** A forced payment-creation failure either cancels the hold or logs the ticket code.
- **Verification:** Integration test with an unsupported payment method string.
- **Status:** TODO

### AUD-W2-07 · Refund approval ignores the ticket state it locks
- **Findings:** AUD-C-006 · **Severity:** Medium · **Type:** Gap
- **Evidence:** `Services/PaymentService.cs:460-466` reads `@TicketState` under `UPDLOCK,HOLDLOCK` and never uses it.
- **Impact:** A travelled (`Đã sử dụng`) ticket can be refunded.
- **Fix:** Use the value already read; return a distinct outcome code and message.
- **Behavior change:** Invalid-path only · **Effort:** S · **Risk:** Low
- **Acceptance:** `ApproveRefundAsync` refuses a payment whose ticket is `Đã sử dụng`.
- **Verification:** Integration test.
- **Status:** TODO

### AUD-W2-08 · Read paths that throw instead of degrading
- **Findings:** AUD-C-014 · **Severity:** Low · **Type:** Risk
- **Evidence:** `DatabaseHelper.cs:137-148`, `:213-232`, `:535-547` have no `try/catch`, unlike `:295` and `:838`.
- **Impact:** Inconsistent behaviour on connection loss; raw `SqlException` reaches the dispatcher handler.
- **Fix:** Apply the surrounding convention (catch `SqlException`, log, return a neutral value).
- **Behavior change:** Invalid-path only · **Effort:** S · **Risk:** Low
- **Acceptance:** With SQL Server stopped, the home/booking/seat screens degrade with a logged error.
- **Verification:** Manual checklist row with the SQL service stopped.
- **Status:** TODO

### AUD-W2-09 · Unreachable duplicate cancellation path
- **Findings:** AUD-C-015 · **Severity:** Low · **Type:** Risk
- **Evidence:** `DatabaseHelper.cs:707-709` paid branch versus `MyTicketsViewModel.cs:284` `CanCancel` requiring `Đã đặt`; the reachable paid path is `RequestRefundAsync` (`:745`).
- **Impact:** Two precondition sets for one rule; they will drift.
- **Fix:** Reduce `CancelTicketAsync` to the unpaid hold case; delegate paid tickets to `RequestRefundAsync`.
- **Behavior change:** None (dead path removal; requires reference evidence per R3 — both call sites are unpaid holds)
- **Effort:** S · **Risk:** Low
- **Acceptance:** Exactly one code path releases a paid ticket.
- **Verification:** `lsp references` on `CancelTicketAsync` + cancel/refund integration tests.
- **Status:** TODO

### AUD-W2-10 · Transient-fault resilience and command timeouts
- **Findings:** AUD-B-006 · **Severity:** High · **Type:** Technical limitation
- **Evidence:** no `CommandTimeout` anywhere in `Data/`; connection strings carry no `ConnectRetryCount`/`Connect Timeout`; `DatabaseHelper.cs:663` turns a deadlock into a generic failure message.
- **Impact:** Serializable + per-seat range locks is precisely the workload that produces 1205 deadlocks; a lost race is a user-visible failure rather than a retry.
- **Fix:** A retry helper (3 attempts, exponential backoff) keyed on `SqlException.Number ∈ {1205, -2, 4060, 40197, 40501, 40613, 49918-49920}` wrapping the fully-rolled-back Serializable units, plus explicit `CommandTimeout` on long admin queries.
- **Behavior change:** Invalid-path only · **Effort:** M · **Risk:** Low · **Dependencies:** AUD-W1-01
- **Acceptance:** A forced deadlock results in a successful booking after retry or the friendly seat-taken message, never the generic connection error.
- **Verification:** Concurrency test taking seats in inverse order from two sessions.
- **Status:** TODO

### AUD-W2-11 · Indexes for the seat-availability hot path
- **Findings:** AUD-B-003 · **Severity:** High · **Type:** Gap
- **Evidence:** `Migration_003_PerformanceIndexes.sql:3-10` creates none on `Tickets.TripId`; `Migration_006:49` removed the last such index; `TicketSeats` has no index leading on `SeatId`. Hot queries: `DatabaseHelper.cs:252-258`, `:479-485`, `:538-545`.
- **Impact:** Every search and seat-map read scans proportionally to total ticket history.
- **Fix:** New migration: `IX_Tickets_Trip_Status(TripId, Status) INCLUDE (PaymentStatus, PaymentExpiresAt)` and `IX_TicketSeats_Seat(SeatId) INCLUDE (TicketId)`.
- **Behavior change:** None · **Effort:** S · **Risk:** Low
- **Acceptance:** Plans for `SearchTripsAsync` and `GetSeatsAsync` seek instead of scan; results identical.
- **Verification:** `SET STATISTICS IO ON` before/after on the seeded test database.
- **Status:** TODO

### AUD-W2-12 · Untracked DDL and migration-history integrity
- **Findings:** AUD-B-008, AUD-B-009, AUD-B-010, AUD-B-015 · **Severity:** Medium · **Type:** Gap/Technical limitation
- **Evidence:** `DatabaseMigrator.cs:230-241` runs `ALTER TABLE` outside `ApplyMigrationAsync` and is never recorded; `:31-40` hardcodes ordering with no checksums and no rollback; `:136-145` records `001_InitialSchema` as applied without executing it; `:243-244` splits batches with a regex unaware of literals.
- **Impact:** Two production columns unknown to the history; edited applied scripts silently ignored; objects declared in `Schema1.sql` silently absent on legacy databases.
- **Fix:** Move the `StagePrice` ALTERs into a numbered migration; add a `Checksum` column verified at startup with a loud failure on drift; document the GO-splitting constraint.
- **Behavior change:** Invalid-path only (drift detection) / None (the rest) · **Effort:** M · **Risk:** Medium
- **Acceptance:** History covers every DDL statement; modifying an applied script fails startup with an explicit message; a second run is a no-op.
- **Verification:** Migrate a scratch database twice; edit one script; restart.
- **Status:** TODO

### AUD-W2-13 · Admin query efficiency and unbounded reads
- **Findings:** AUD-B-011, AUD-B-013, AUD-B-014, AUD-E-012 · **Severity:** Medium · **Type:** Risk
- **Evidence:** `AdminDatabaseService.cs:392-402` join fan-out grouped by 23 columns; `DatabaseHelper.cs:306-335` write-on-read company sync with non-sargable `TRIM()`; `AdminDatabaseService.cs:863-880` and `DatabaseHelper.cs:628-639` N+1 inserts inside open transactions; `AdminDashboardViewModel.cs:94-112` four sequential round trips.
- **Impact:** Admin screens degrade with total ticket history rather than with the entities shown; the dashboard costs the sum of four round trips.
- **Fix:** Scalar subquery/`OUTER APPLY` instead of the fan-out; move the company sync off the read path into startup/migration inside a transaction; set-based inserts via TVP or `VALUES`; `Task.WhenAll` on the dashboard (the pattern already used at `AdminBusManagementViewModel.cs:431-435`).
- **Behavior change:** None · **Effort:** M · **Risk:** Medium · **Dependencies:** AUD-W2-11
- **Acceptance:** Identical result sets; plan cost independent of ticket history; listing companies issues no `INSERT`; one statement per bus-registration batch.
- **Verification:** Result-set diff plus `SET STATISTICS IO`/statement counts.
- **Status:** TODO
- **Note:** paging for the four unbounded admin lists (AUD-B-012) changes a valid workflow → AUD-WX-07.

### AUD-W2-14 · Cancellation tokens through UI-facing data methods
- **Findings:** AUD-E-007, AUD-E-013 · **Severity:** Medium · **Type:** Gap
- **Evidence:** no `CancellationToken` in any `DatabaseHelper`/`AdminDatabaseService` UI-facing method; the only staleness guard is the hand-rolled version counter at `HomeViewModel.cs:25, :160-164`; `NavigationService.cs:80-86` fires refreshes unawaited.
- **Impact:** Navigating away mid-query leaves it running and applies results to a discarded ViewModel; rapid tab switching stacks connections.
- **Fix:** Thread `CancellationToken` through the data methods and `IRefreshable.RefreshAsync`; cancel the previous activation's token in `Activate`.
- **Behavior change:** None · **Effort:** L · **Risk:** Medium · **Dependencies:** AUD-W2-02
- **Acceptance:** Navigating away during a slow query cancels it and mutates no departed collection.
- **Verification:** Artificial delay in a loader + rapid navigation, log confirms cancellation.
- **Status:** TODO

### AUD-W2-15 · Collection refresh and command re-query cost
- **Findings:** AUD-E-008, AUD-E-014, AUD-E-015 · **Severity:** Low/Medium · **Type:** Technical limitation
- **Evidence:** `Common/RelayCommand.cs:16-20` routes `CanExecuteChanged` through `CommandManager.RequerySuggested`; Clear+per-item-Add at `BookingViewModel.cs:67-68`, `AdminBusManagementViewModel.cs:437-450`, `HomeViewModel.cs:166-167`, `SeatSelectionViewModel.cs:136-144`.
- **Impact:** O(rows × commands) predicate evaluations per keystroke; one `Reset` + N `Add` per refresh, rebuilding every row on the 20 s seat poll.
- **Fix:** Backing event + `RaiseCanExecuteChanged()` on `RelayCommand`; a `ReplaceAll` collection helper raising a single `Reset`.
- **Behavior change:** None · **Effort:** M · **Risk:** Medium
- **Acceptance:** Guarded buttons still enable/disable correctly; one `CollectionChanged` per refresh.
- **Verification:** Temporary counters before/after.
- **Status:** TODO

### AUD-W2-16 · Undisposed resources and thread-affinity contract
- **Findings:** AUD-E-009, AUD-E-010, CA2000/CA1001 sites · **Severity:** Medium/Low · **Type:** Risk
- **Evidence:** `TicketPrintService.cs:284, :300` undisposed `PngByteQRCode`; `VnPayCallbackListener.cs:35` undisposed `HttpListener` on all paths; `BusCompanyDetailViewModel.cs:11` and `PaymentWindow.xaml.cs:8` own disposable fields without being disposable; `VnPayCallbackListener.cs:20` raises a static event from a background thread with marshalling left to the subscriber; `LocationService.cs:78-79` fires detection from a constructor.
- **Impact:** Leaked native/unmanaged handles; a future subscriber touching bound state throws invisibly inside the listener loop.
- **Fix:** `using` declarations; implement `IDisposable` on the two types; marshal at the raise site or document the contract; start location detection from an explicit initializer.
- **Behavior change:** None · **Effort:** S · **Risk:** Low
- **Acceptance:** CA2000/CA1001 report zero occurrences at the named sites; a subscriber setting a bound property does not throw.
- **Verification:** Analyzer run (after AUD-W4-01) + throwaway harness resolving `LocationService.Instance` from a thread-pool thread.
- **Status:** TODO

---

## Wave 3 — Architecture, MVVM, DI

Every item here is behaviour-preserving and must be preceded by characterization tests for the flow it touches.

### AUD-W3-01 · Payment dialog behind an interface
- **Findings:** AUD-A-001 (Critical) · **Fix:** `IPaymentDialogService` implemented in the WPF layer; inject into `BookingConfirmationViewModel`; remove `new PaymentWindow(...)`/`ShowDialog()` from the ViewModel.
- **Behavior change:** None · **Effort:** M · **Risk:** Medium · **Dependencies:** AUD-W3-03
- **Acceptance:** `BookingConfirmationViewModel` has no `System.Windows`/`Views` reference; a fake dialog service drives both payment branches with no STA thread.
- **Verification:** New unit test + manual booking checklist row. **Status:** TODO

### AUD-W3-02 · Explicit actor instead of ambient `CurrentUser`
- **Findings:** AUD-A-002 (Critical), supports AUD-D-014 · **Fix:** pass `UserSession actor`/actor id into admin and payment APIs; `RequireAdmin(actor)` validates it; `CurrentUser` becomes a WPF-only convenience the shell supplies.
- **Behavior change:** None · **Effort:** L · **Risk:** Medium · **Dependencies:** characterization tests for admin flows
- **Acceptance:** No `CurrentUser` reference under `Data/` or `Services/`; an admin API rejects a Customer actor regardless of `CurrentUser`.
- **Verification:** `grep`/`lsp references` + unit test. **Status:** TODO

### AUD-W3-03 · Composition root
- **Findings:** AUD-A-003, AUD-A-005, AUD-A-009, AUD-A-010, AUD-A-011 · **Fix:** `Microsoft.Extensions.DependencyInjection` (R10 record: new dependency, justification = removes two hand-wired ternary factories and enables every other seam); one provider in `App.OnStartup`; `INavigationService : INotifyPropertyChanged`; interfaces for notification/localization/location; delete the `Activator` fallback and the parameterless `BookingViewModel` constructor.
- **Behavior change:** None, except the `Activator` fallback removal which is Invalid-path only · **Effort:** M · **Risk:** Medium
- **Acceptance:** No `new SomeViewModel(...)` in window code-behind; no `Service.Instance` under `ViewModels/`; navigating to an unregistered ViewModel throws a diagnosable exception naming the type.
- **Verification:** existing navigation tests updated + full navigation manual checklist. **Status:** TODO

### AUD-W3-04 · Headless ticket rendering
- **Findings:** AUD-A-004 · **Fix:** split `TicketPrintService` into a headless renderer (`byte[] RenderPdf/RenderPng`) and a WPF shell owning `PrintDialog`; `IFileSaveService` for `TicketViewModel`.
- **Behavior change:** None · **Effort:** L · **Risk:** Medium · **Dependencies:** AUD-W3-03; `DomainModelTests.cs:66` must keep passing
- **Acceptance:** PDF/PNG bytes produced without `System.Windows`; `TicketViewModel` has no `Microsoft.Win32` reference. **Status:** TODO

### AUD-W3-05 · Services free of the WPF dispatcher
- **Findings:** AUD-A-012 · **Fix:** `PeriodicTimer` + an injected UI-synchronisation abstraction; move `LocalizationService`'s resource-dictionary work into a WPF adapter.
- **Behavior change:** None · **Effort:** M · **Risk:** Medium · **Dependencies:** AUD-W2-04, AUD-W3-03
- **Acceptance:** `Services/` compiles with no `System.Windows*` using outside a named adapter file. **Status:** TODO

### AUD-W3-06 · Split `AdminBusManagementViewModel`
- **Findings:** AUD-A-006 · **Fix:** extract `BusFormModel`, `BusDraftListViewModel`, `BusFleetFilter`.
- **Behavior change:** None · **Effort:** L · **Risk:** Medium · **Dependencies:** existing admin bus tests as characterization
- **Acceptance:** no admin ViewModel exceeds ~350 lines; the four existing tests pass with at most binding-path updates. **Status:** TODO

### AUD-W3-07 · Booking workflow out of the ViewModel
- **Findings:** AUD-A-007, AUD-A-013 · **Fix:** `BookingWorkflow.PlaceBookingAsync(...)` returning a result record; batch bus registration as one transactional call reporting failed plates.
- **Behavior change:** None / Invalid-path only (batch atomicity) · **Effort:** M · **Risk:** Medium · **Dependencies:** AUD-W3-01, AUD-W3-02
- **Acceptance:** compensation covered by a test that constructs no ViewModel; a batch containing one invalid bus commits nothing and names the plate. **Status:** TODO

### AUD-W3-08 · Single session-teardown policy
- **Findings:** AUD-A-008 · **Fix:** one `ISessionService.SignOut()` used by both shells.
- **Behavior change:** None · **Effort:** S · **Risk:** Low · **Dependencies:** AUD-W3-02
- **Acceptance:** `CurrentUser.Account = null` appears exactly once. **Status:** TODO

### AUD-W3-09 · Data-access seams
- **Findings:** AUD-A-015, AUD-G-007 · **Fix:** extract `IBookingRepository`/`ITicketRepository`/`IAdminRepository` over the existing statics; split `DatabaseHelper` along its existing seams.
- **Behavior change:** None · **Effort:** XL · **Risk:** Medium · **Dependencies:** AUD-W3-02, AUD-W3-03, and Wave 1 integration tests as the safety net
- **Acceptance:** booking-confirmation and my-tickets flows unit-testable against fakes; generated SQL textually unchanged. **Status:** TODO

### AUD-W3-10 · Shared admin ViewModel base
- **Findings:** AUD-G-008 · **Fix:** hoist `Message`/`HasMessage`/`IsLoading` into `AdminViewModelBase`; extract the pending-delete pair.
- **Behavior change:** None · **Effort:** M · **Risk:** Low
- **Acceptance:** no admin VM declares its own `_message`; admin VM tests pass unchanged. **Status:** TODO

---

## Wave 4 — Testing and CI/CD

### AUD-W4-01 · Analyzers and `.editorconfig`
- **Findings:** AUD-G-001, AUD-G-002, AUD-H-006 · **Severity:** High · **Type:** Gap
- **Fix:** `Directory.Build.props` with `EnableNETAnalyzers`/`AnalysisLevel`/`AnalysisMode`, `.editorconfig` capturing the conventions already in use, then triage. Per R8 no blanket `NoWarn`; per the DoD exception, record the warning baseline first (00-inventory §6 already does), fix warnings in touched code, record the remainder.
- **Behavior change:** None · **Effort:** M · **Risk:** Low
- **Acceptance:** both projects inherit the properties; the remaining diagnostic set is documented with justification per rule.
- **Verification:** `dotnet build` diagnostic count before/after. **Status:** TODO

### AUD-W4-02 · Lifecycle integration tests
- **Findings:** AUD-H-002 · **Severity:** High · **Fix:** happy-path tests for hold→expire→release, hold→pay→settle, paid→refund-requested, cancel, on the Wave 1 fixture.
- **Behavior change:** None · **Effort:** L · **Risk:** Low · **Dependencies:** AUD-W1-01
- **Acceptance:** each transition asserted end to end against the test database. **Status:** TODO

### AUD-W4-03 · Test isolation for global state
- **Findings:** AUD-H-007, AUD-H-009 · **Fix:** an xunit collection + fixture snapshotting/restoring `CurrentUser.Account`, `LocationService.Instance`, culture; unsubscribe `LanguageChanged`; per-test temp directories deleted afterwards.
- **Behavior change:** None · **Effort:** M · **Risk:** Low
- **Acceptance:** five consecutive full runs green; no residual files. **Status:** TODO

### AUD-W4-04 · Test categorisation and a meaningful password theory
- **Findings:** AUD-H-008, AUD-H-011 · **Fix:** `[Trait("Category","Ui"/"Io"/"Integration")]`; add at least one valid-password row and assert the boolean outcome rather than the message text (the current theory cannot fail if the validator rejects everything).
- **Behavior change:** None · **Effort:** S · **Risk:** Low
- **Acceptance:** `--filter Category!=Ui` runs the pure tests; making the validator reject everything fails the theory. **Status:** TODO

### AUD-W4-05 · CI pipeline completion
- **Findings:** AUD-H-003, AUD-H-004, AUD-H-005, AUD-H-010, AUD-H-012, AUD-D-015 · **Fix:** add format verification (after W4-01), coverage collection + artifact, trx artifact with `if: always()`, `dotnet list package --vulnerable`, `workflow_dispatch` + nightly schedule, a versioned `dotnet publish` artifact, and a SQL Server service container so the concurrency tests actually run.
- **Behavior change:** None · **Effort:** M · **Risk:** Low · **Dependencies:** AUD-W1-01, AUD-W4-01
- **Acceptance:** CI reports 0 skipped tests, publishes coverage and a version-stamped build, and fails on a deliberately misformatted file or an introduced warning. **Status:** TODO

---

## Wave 5 — UI/UX and accessibility

All Wave 5 items are verified through the manual checklist in `04-upgrade-log.md`; no visual verification is claimed automatically.

### AUD-W5-01 · Accessible names across the booking funnel
- **Findings:** AUD-F-001 (High), AUD-F-013, AUD-F-014 · **Evidence:** `AutomationProperties` 9 occurrences in 6 of 22 views; the entire funnel has none.
- **Fix:** `AutomationProperties.Name`/`HelpText`/`LabeledBy` on every interactive control in Login → Home → Booking → SeatSelection → BookingConfirmation → Payment → Ticket, names sourced from resource keys.
- **Behavior change:** None · **Effort:** L · **Risk:** Low · **Dependencies:** AUD-W5-02
- **Acceptance:** every Tab-reachable control in the funnel exposes a non-default accessible name. **Status:** TODO

### AUD-W5-02 · Localization coverage
- **Findings:** AUD-F-002 (High) · **Evidence:** chrome uses `{DynamicResource}`; content views hardcode Vietnamese, while `SeatSelectionView.xaml:294,301` and `AdminPaymentsView.xaml:153,228,234,271` hardcode English.
- **Fix:** move user-visible literals into both dictionaries; add a key-parity test.
- **Behavior change:** None · **Effort:** XL · **Risk:** Low
- **Acceptance:** toggling language changes every visible string; the dictionaries expose identical key sets. **Status:** TODO

### AUD-W5-03 · Per-field validation
- **Findings:** AUD-F-003 (High) · **Evidence:** zero occurrences of any validation infrastructure project-wide.
- **Fix:** `INotifyDataErrorInfo` on `ViewModelBase`, one `Validation.ErrorTemplate` in `Theme.xaml`, focus the first invalid field on submit.
- **Behavior change:** None · **Effort:** L · **Risk:** Medium · **Dependencies:** AUD-W3-10
- **Acceptance:** submitting an invalid form highlights and focuses the offending field. **Status:** TODO

### AUD-W5-04 · Keyboard contract
- **Findings:** AUD-F-004, AUD-F-010 · **Fix:** `FocusManager.FocusedElement` per window/view, explicit `TabIndex` where declaration order differs from visual order, `IsDefault`/`IsCancel` on every form's primary/cancel action.
- **Behavior change:** None · **Effort:** M · **Risk:** Low
- **Acceptance:** each window opens focused on its primary input; Enter submits and Escape cancels on every form. **Status:** TODO

### AUD-W5-05 · Virtualization and the shell scroll host
- **Findings:** AUD-E-004 (High), AUD-F-005 · **Evidence:** `MainWindow.xaml:398-404` and `AdminWindow.xaml:318-320` wrap page content in a `ScrollViewer`, measuring inner `VirtualizingStackPanel`s at infinite height; `AdminReportsView.xaml:144` is an unbounded non-virtualized list.
- **Fix:** give each list its own scroll region; re-point `ContentScroller.ScrollToTop()` calls; virtualize the reports list.
- **Behavior change:** Valid-path (page scrolling behaviour changes) → see AUD-WX-08 for the shell change; the `AdminReportsView` virtualization alone is None and is in scope here.
- **Effort:** L · **Risk:** High (shell) / S · Low (reports list)
- **Acceptance:** with 2,000 payment rows realized containers stay bounded by the viewport. **Status:** TODO (reports list) / AWAITING-APPROVAL (shell scroll host)

### AUD-W5-06 · Confirmation, disabled-state and live-region announcements
- **Findings:** AUD-F-006, AUD-F-008, AUD-F-009 · **Fix:** focus trap + `IsCancel` + `AutomationProperties.LiveSetting="Assertive"` on inline confirm panels; mirror ToolTip-only explanations into `HelpText` and visible text; live-region annotation on status banners.
- **Behavior change:** None · **Effort:** M · **Risk:** Low · **Dependencies:** AUD-W5-01
- **Acceptance:** triggering a delete moves focus into the panel, Escape cancels, the prompt is announced; a failed login is announced without moving focus. **Status:** TODO

### AUD-W5-07 · Admin empty states and theme consistency
- **Findings:** AUD-F-011, AUD-F-012 · **Fix:** apply the customer empty-state pattern to the six admin lists; move ~60 inline hex literals into named `Theme.xaml` brushes and check contrast.
- **Behavior change:** None · **Effort:** M · **Risk:** Low
- **Acceptance:** every list renders a guided empty state; no raw hex remains in `Views/*.xaml`. **Status:** TODO

### AUD-W5-08 · DPI and scaling
- **Findings:** AUD-F-007 · **Fix:** named font-size resources, lower `MinWidth`/`MinHeight` to fit 1280×720 @150%, `SizeToContent` + work-area `MaxHeight` on `PaymentWindow`.
- **Behavior change:** None · **Effort:** L · **Risk:** Medium
- **Acceptance:** the booking funnel is fully usable at 1280×720 with 150% scaling, no clipping. **Status:** TODO

---

## Wave 6 — Documentation and maintainability

### AUD-W6-01 · Root README
- **Findings:** AUD-I-001 (High) · **Fix:** description, prerequisites, clone→configure→migrate→run, tests, links.
- **Behavior change:** None · **Effort:** S · **Acceptance:** a developer with only the README can build, migrate and launch. **Status:** TODO

### AUD-W6-02 · Setup, environment and migration documentation
- **Findings:** AUD-I-002, AUD-I-004, AUD-I-005 · **Fix:** numbered developer setup; a variable table (required/default/purpose/consequence) matching `AppConfig.cs:13-53`; a migrations section stating forward-only-by-design, naming, idempotency-guard pattern, GO splitting, manual rollback — and reconcile `AGENTS.md` R6's `Down()` wording with the actual mechanism.
- **Behavior change:** None · **Effort:** M · **Acceptance:** every variable read in `AppConfig.cs` has a row with a matching default; a contributor can author a correct migration from the doc alone. **Status:** TODO

### AUD-W6-03 · ERD and corrected project documentation
- **Findings:** AUD-I-003, AUD-I-006, AUD-I-007, AUD-I-008, AUD-I-009, AUD-I-010 · **Fix:** a `mermaid erDiagram` of the nine tables naming the enforced invariants; deployment + troubleshooting runbook; correct the stale "no CI/CD" claim and the stale anti-double-booking claim at `Markdown/PROJECT_DOCUMENTATION.md:119`; de-duplicate overlapping docs; resolve the stray `Models/README.md`.
- **Behavior change:** None · **Effort:** M · **Acceptance:** no statement in the docs is contradicted by the repository; the ERD matches the migrated schema. **Status:** TODO

### AUD-W6-04 · Magic strings, magic numbers and PII in logs
- **Findings:** AUD-G-003, AUD-G-009, AUD-G-010, AUD-G-011, AUD-G-013, AUD-D-012 · **Fix:** single-source the active-status SQL fragment and the 15-minute hold; replace the `AdminModels.cs:253` literal with `TicketStatuses.Paid`; name the home-screen magic numbers and move marketing tags out of the data layer; delete the dead initialiser at `DatabaseHelper.cs:92`; log account ids instead of usernames/e-mail addresses and move `Logs/` under `%LOCALAPPDATA%`.
- **Behavior change:** None · **Effort:** M · **Risk:** Medium · **Dependencies:** AUD-W4-01
- **Acceptance:** each Vietnamese status literal appears once in C#; generated SQL is byte-identical; a full smoke run leaves no raw e-mail address in the log. **Status:** TODO

### AUD-W6-05 · Exception-handling hygiene and config fallback warning
- **Findings:** AUD-G-006, AUD-G-014 · **Fix:** narrow the unfiltered catch at `TicketPrintService.cs:326-327` and log at the catch site; warn when the hardcoded connection-string fallback is used.
- **Behavior change:** None / Invalid-path only · **Effort:** S · **Risk:** Low
- **Acceptance:** no unfiltered `catch (Exception)` in `TicketPrintService`; launching without `BUS_TICKET_CONNECTION_STRING` and without `appsettings.json` logs a warning. **Status:** TODO

### AUD-W6-06 · Dedicated formatting pass
- **Findings:** AUD-G-012, AUD-H-003, and the 9 baseline whitespace violations · **Fix:** restricted to `Data/DatabaseHelper.cs` and the four `Views/Admin*View.xaml.cs` files, committed separately per R9, after `.editorconfig` lands.
- **Behavior change:** None · **Effort:** S · **Risk:** Low · **Dependencies:** AUD-W4-01
- **Acceptance:** `dotnet format <project> --verify-no-changes` exits 0; no statement-packed lines remain in the booking/seat path. **Status:** TODO

---

## AWAITING-APPROVAL — Valid-path behaviour changes

Not implemented in this session. Each records old behaviour, proposed behaviour, reason, affected workflows, risks and required tests.

| ID | Finding | Old behaviour | Proposed behaviour | Reason | Affected workflows | Risks | Tests required |
|---|---|---|---|---|---|---|---|
| **AUD-WX-01** | AUD-D-001 | Client connects directly to SQL Server with the operator's Windows identity; authorization is in-process | Application login restricted to stored procedures that re-derive caller and role server-side, or an authenticated application server | The entire security model is advisory (Blocker) | Every workflow | Deployment/permission model change; regression risk across all data access | Full integration suite + direct-SQL bypass attempts |
| **AUD-WX-02** | AUD-C-003 | No trip cancellation; `DeleteTripAsync` refuses once a ticket exists; paid tickets on a cancelled service are auto-marked used at departure | `Trips.Status` + `CancelTripAsync` cancelling live tickets and flagging settled payments for refund; cancelled trips excluded from search and from the auto-use sweep | A breakdown or cancelled service has no operational path today | Trip admin, search, refund, ticket lifecycle | New state in the ticket/payment state machine | Cancel-with-sold-tickets integration test |
| **AUD-WX-03** | AUD-C-005, AUD-B-103 | Departures stored in local wall-clock; all predicates use `SYSUTCDATETIME()` — a 7-hour skew in Vietnam | Store UTC, render local; align `DevSeedTrips.sql` | Refunds remain requestable on departed coaches; auto-use fires 7 hours late | Search, refund, cash holds, auto-use, seeding | Existing rows need a one-off shift; every departure display changes | Departed-trip behaviour tests before/after the shift |
| **AUD-WX-04** | AUD-C-009 | A cash-on-board ticket not confirmed before departure is auto-cancelled and can never become used | Exclude cash from the expiry sweep or give it a post-departure grace window plus an explicit collection outcome | The passenger's completed journey is recorded as a cancellation and the revenue is lost | Cash payment, settlement, reporting | Extends seat holds beyond departure | Cash-after-departure confirmation test |
| **AUD-WX-05** | AUD-D-013, AUD-D-014 | Operator types the new password and learns it; no forced change; no session expiry; a demoted admin keeps rights until restart | Server-generated temporary secret + `MustChangePassword`; idle timeout; re-read role before privileged operations | Shared credentials and stale privilege | Login, profile, admin accounts | Users must change password after a reset; idle sessions end | Reset-then-login and demote-then-act tests |
| **AUD-WX-06** | AUD-C-011 | VNPay is fully implemented but commented out of the offered methods while the loopback listener still starts | **SKIPPED — owner decision (2026-09-20): there is no VNPay merchant subscription, so the channel stays disabled.** Revisit only if a subscription is obtained. | — | Payment, startup | The disabled code path and its loopback listener remain in the build; they are hardened (AUD-W1-07/08/09/16) but unreachable from the product | — |
| **AUD-WX-07** | AUD-B-012 | Admin route/trip/account/bus lists load every row | `OFFSET/FETCH` paging, default page size 50 | Trips grow unbounded and stream into the UI thread | All admin lists | Admin UI gains paging controls | Constant-time open against a 100k-trip database |
| **AUD-WX-08** | AUD-E-004 | Shell `ScrollViewer` scrolls the whole page and defeats list virtualization | Per-list scroll regions | Every declared virtualization is inert | Every page's scrolling behaviour | High — layout regression across 22 views | Visual-tree element counts + full manual checklist |
| **AUD-WX-09** | AUD-B-007 | `SeedData.sql` and the startup schedule top-up run against any database | Gate both behind an explicit development flag | A production deployment cannot avoid demo data | First run, scheduling | Demo flows depend on the top-up | Fresh-database test with the flag off |
| **AUD-WX-10** | AUD-D-010 | Shipped connection strings use `Encrypt=False;TrustServerCertificate=True` | `Encrypt=True;TrustServerCertificate=False` by default | All traffic including PII is unprotected on the wire | Every database call | Breaks environments without a trusted certificate | `sys.dm_exec_connections.encrypt_option` check |
| **AUD-WX-11** | AUD-C-008, AUD-C-012, AUD-C-013 | Bank-transfer QR is a static image encoding neither amount nor reference; no automatic ticket delivery after settlement; the printed QR has no consumer | Runtime VietQR payload; automatic e-mail on settlement; ticket-code lookup/scan check-in | Manual reconciliation and an unusable QR capability | Payment, settlement, boarding | New outbound e-mail on a transactional path | Decoded-payload, settlement-e-mail and check-in-by-code tests |
| **AUD-WX-12** | AUD-B-101, AUD-B-102, AUD-B-104, AUD-B-105, AUD-B-106, AUD-B-107, AUD-B-108, AUD-B-109, AUD-B-110, AUD-B-111 | Fabricated ratings/driver/registration data, a 120,000 VND placeholder price for tripless routes, placeholder passenger/driver values on printed tickets, no admin bootstrap, no commission model, scale mismatch between ticket and ledger money | Show absence instead of fabricating; bootstrap an admin from the app; add a commission model; align money scale | Customer-visible and legally-relevant data is invented | Home, company browse, printing, reporting, first run | Display and reporting changes | Per-item tests listed in the source findings |
