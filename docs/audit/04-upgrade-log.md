# 04 — Upgrade log

Branch `upgrade/20260920`, base `9b5b3f2` (tag `pre-upgrade`).

Declared databases in use: Dev `localhost/BusTicketSaleSystem` (migrated, never reset), Test `localhost/BusTicketSaleSystem_Tests` (created, dropped and recreated by the test fixture on every run).

---


## Scope decision — VNPay (2026-09-20, owner)

No VNPay merchant subscription exists, so **no further VNPay work is performed**. `AUD-WX-06` is `SKIPPED`; any remaining VNPay-flavoured findings (IPN reconciliation, `vnp_QueryDr`, enabling the payment option) are out of scope until a subscription exists.

The VNPay fixes already landed in Wave 1 — `AUD-W1-07` (require `vnp_TransactionStatus`), `AUD-W1-08` (late capture queued for refund), `AUD-W1-09` (decline releases the hold) and `AUD-W1-16` (loopback endpoint hardening) — are **kept**. They cost nothing to keep, their tests sign callbacks locally and need no gateway account, and `AUD-W1-16` closes a log-injection and denial-of-service hole in an endpoint that still binds on every launch. Removing the VNPay code path entirely would delete working code and needs an explicit instruction.

## Wave 1 — Test foundation, security, data integrity

### Result

| | |
|---|---|
| Build | `dotnet build BusTicketSaleSystem.slnx` → **0 errors, 0 warnings** |
| Tests | `dotnet test` → **84 passed, 0 failed, 0 skipped** (baseline was 53 passed / 1 skipped) |
| New tests | 31, of which 24 are integration tests against real SQL Server |
| Migrations added | 5 (`008`…`012`), all applied to Dev and Test; no Dev data modified or deleted |
| Packages added/upgraded | none |
| Tag | `wave-1` |

### Completed items

| Item | Commit | Change | Verification |
|---|---|---|---|
| AUD-W1-01 | `33c1021` | `SqlServerFixture` creates, drops and migrates the declared test database, seeds accounts/trips/seats, and hard-refuses to run against any catalog other than `BusTicketSaleSystem_Tests`. Replaces the three env vars the old skipped test required. | `dotnet test` reports 0 skipped; Dev row counts unchanged before/after |
| AUD-W1-02 | `33c1021` | Five executing concurrency tests: single-seat, multi-seat overlap, raw-SQL under row versioning, controlled-failure message, and a guard asserting the uniqueness object exists. | `dotnet test --filter ConcurrencyBookingTests` |
| AUD-W1-03 | `33c1021` | `Migration_008_SeatUniqueness.sql`: schema-bound view `dbo.UX_ActiveTicketSeats` with unique clustered index `UX_ActiveTicketSeats_Trip_Seat (TripId, SeatId)`. | Pre-fix the raw-SQL test committed **two** tickets on one seat under `READ_COMMITTED_SNAPSHOT ON`; post-fix exactly one, with the loser refused by the engine |
| AUD-W1-04 | `d58dbcf` | Both `TicketSeats` triggers `THROW` without `ROLLBACK TRANSACTION`; `BookTicketsAsync` tolerates an already-completed transaction and maps 2601/2627/1205/51001/51002 to the seat-taken message. | `BookTicketsAsync_SeatTakenByAnotherWriter_ReturnsControlledFailure` |
| AUD-W1-05 | `5efe42f` | `VerifyPassword` rejects any stored value that is not a derived hash. No stored hash modified. | 5 new `SecurityTests` rows |
| AUD-W1-06, AUD-W1-17 | `3f53f13` | `Accounts.FailedLoginCount` / `LockoutEndsAt`; 5 failures lock for 15 minutes; every failure logged by account id; unknown logins pay the same PBKDF2 cost. | 4 `AuthenticationTests` |
| AUD-W1-07, AUD-W1-08, AUD-W1-09 | `9570aa8` | Settlement requires `vnp_ResponseCode` **and** `vnp_TransactionStatus`; a capture after the hold lapsed is recorded and queued for refund; a decline releases the hold. | 6 `VnPaySettlementTests` using locally HMAC-signed callbacks |
| AUD-W1-10, AUD-W1-11, AUD-W1-12 | `f3b34ba` | Cash and bank-transfer creation are Serializable; `UX_Payments_Live_Ticket` enforces one live payment per ticket; `DeleteBusAsync` sets `XACT_ABORT ON` and locks its preflight; trip insert holds the overlap range and `UX_Trips_Bus_Departure` backs it. | 3 `AdminDataIntegrityTests` |
| AUD-W1-13, AUD-W1-14 | `f782517` | Station scope enforced in the same statement as every route/trip write; geolocation moved to HTTPS and can no longer override a `STATION_LOCATION`-pinned station. | 2 scope-refusal tests |
| AUD-W1-15, AUD-W2-07 | `39840b4` | `dbo.AuditLog` written inside the operation's transaction for role changes, password resets, account deletions and all three settlement outcomes; refunds refused for travelled tickets. | 3 `AuditTrailTests`, including "a refused settlement writes no audit row" |
| AUD-W1-16 | `008fc46` | Loopback endpoint: logging moved after verification and sanitised, non-GET and foreign-`Host` refused, handlers dispatched off the accept loop. | 3 `VnPayListenerTests` against a live listener |
| AUD-W1-18 | `aa05356` | Admin password reset uses the single `ValidatePassword` policy; role grants log on success. | build + suite |
| AUD-W1-19 | `d187c94` | `.gitignore` covers `appsettings.*` overrides and `secrets.json`; CI gains a vulnerability audit, manual trigger, nightly schedule and trx artifact. | `git status`; workflow file |

### Awaiting approval (not implemented)

`AUD-W1-20` (AUD-D-001, Blocker) — the client is a trusted SQL Server principal. Fixing it replaces the deployment and permission model (stored-procedure surface or an application server) and is `XL`/High-risk, so it needs an explicit decision. Wave 1 reduces but does not remove the exposure: plaintext logins are gone, guessing is rate-limited and recorded, the seat invariant is now engine-enforced, station scope is enforced below the UI, and privileged actions are audited. A user who can run the app can still connect to the database directly with the same Windows identity.

The `Valid-path` items in `02-upgrade-backlog.md` (`AUD-WX-01` … `AUD-WX-12`) remain unimplemented by design.

### Migration verification

All five migrations were inspected for `Up` content, destructive statements, indexes, foreign keys, unique constraints, nullability and data preservation; each records its reversal procedure in its header comment. This project's runner is forward-only by design, so reversal is a documented manual procedure, not a `Down()` script.

| Migration | Objects | Destructive? | Data check before applying to Dev |
|---|---|---|---|
| `008_SeatUniqueness` | view + unique clustered index | no | 0 duplicate active `(TripId, SeatId)` pairs |
| `009_TriggerRollbackFix` | recreates two triggers | drops and recreates triggers only | n/a |
| `010_LoginThrottling` | 2 nullable/defaulted columns | no | 6 accounts, all counters 0 |
| `011_PaymentIntegrity` | 2 filtered/unique indexes | no | 0 tickets with >1 live payment; 0 duplicate `(BusId, DepartureTime)` |
| `012_AuditLog` | 1 table + 1 index | no | n/a |

Dev row counts before and after Wave 1: Tickets 57 → 57, TicketSeats 63 → 63, Payments 52 → 52, Trips 619 → 619, Accounts 6 → 6.

### Risks carried forward

- The client remains a trusted database principal (`AUD-W1-20`).
- The new uniqueness object makes concurrent seat contention surface as deadlock (1205) more often than before; `AUD-W2-10` (retry with backoff) is now higher value than the audit originally assessed.
- Station scope is still sourced from a process-global singleton, exactly like `CurrentUser`; Wave 3 (`AUD-W3-02`) replaces both with an explicit actor.
- Integration tests skip, rather than fail, when SQL Server is unreachable — so a CI runner without SQL Server would report 0 skipped only once `AUD-W4-05` provisions one.

### Manual test checklist — Wave 1

The GUI was not launched. The following require a human at the application.

| Screen | Preconditions | Action | Expected result | Failure symptom |
|---|---|---|---|---|
| Login | Any account | Enter a wrong password five times | Sixth attempt reports the account is locked for 15 minutes; the correct password is also refused until the window passes | Wrong password keeps returning "Incorrect username or password" indefinitely |
| Login | Locked account, 15 minutes elapsed | Sign in with the correct password | Sign-in succeeds; counter reset | Still refused |
| Seat selection | Two clients on one trip | Both select the same seat and confirm within a second of each other | One booking succeeds; the other shows "One of the selected seats has just been taken or is not valid for this trip." | A system-error dialog, or both bookings succeed |
| Payment (VNPay) | `VNPAY_*` configured, sandbox declines the payment | Complete the gateway flow | Seat is immediately available again and the message matches | Seat stays blocked for up to 15 minutes while the UI says it was released |
| Payment (bank transfer) | Booked, unpaid ticket | Double-click the pay button | Exactly one pending payment appears in the admin ledger | Two pending rows for one ticket |
| Admin → Payments | Settled payment whose ticket is `Đã sử dụng`, refund requested | Approve refund | Refused: "This ticket has already been used for travel…" | Refund is approved |
| Admin → Accounts | Any non-self account | Promote to Admin | `dbo.AuditLog` gains one `AccountRoleChanged` row naming the acting admin | No audit row |
| Admin → Accounts | Any account | Reset password to `password` | Refused by the complexity policy | Accepted |
| Admin → Trips/Routes | Workstation pinned to station X via `STATION_LOCATION` | Create a trip on a route that touches neither endpoint of X | Refused, naming station X | Trip is created |
| Admin → Buses | Bus with seats and no trips/tickets | Delete it | Bus and its seats both disappear, or neither does | Bus remains with an empty seat map while the UI reports success |
