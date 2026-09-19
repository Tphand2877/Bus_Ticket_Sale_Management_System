# 01 — Audit report

Scope: full repository at `D:\VS 2026\Csharp\BusTicketSaleSystem`, commit `9b5b3f2`. Read-only phase; no production source was modified. Every finding carries `path:line` evidence or is explicitly marked `HYPOTHESIS`.

## 1. Executive summary

The product is a substantially complete Vietnamese intercity bus-ticketing desktop application: 28 workflows traced, 18 complete, 7 partial, 3 missing. Engineering quality is well above typical coursework in several places — PBKDF2-SHA256 with 310,000 iterations and rehash-on-login, Serializable transactions with `UPDLOCK, HOLDLOCK` on every state machine, fully parameterised SQL with no injection sites, `DECIMAL` money throughout, a migration runner that commits each script with its history row atomically, and 53 passing tests.

Three problems dominate everything else:

1. **The database is the trust boundary and it is handed to the client** (AUD-D-001). Every workstation connects directly to SQL Server with the operator's Windows identity; `RequireAdmin()` is an in-process check on a mutable public static. All authorization, revenue integrity and per-account isolation are advisory.
2. **Plaintext passwords still authenticate** (AUD-D-002). `VerifyPassword` falls through to a byte comparison of the password against the stored value when the stored value is not a recognised hash. Combined with (1), setting `PasswordHash='x'` on any account yields admin access, and the next login re-hashes it, erasing the evidence.
3. **The anti-double-sell invariant has no declarative database constraint** (AUD-B-001). `Migration_006_MultiSeatTicket.sql:48-49` drops `UX_Tickets_Active_Trip_Seat`; the replacement is an `AFTER INSERT` trigger. This audit **proved experimentally** that the trigger is isolation-dependent: with `READ_COMMITTED_SNAPSHOT ON`, two concurrent transactions both sold the same seat.

### Proven double-sale reproduction

Run against `BusTicketSaleSystem_Tests` (migrated from the repository's own scripts), application code bypassed entirely, two synchronised `sqlcmd` sessions inserting the same `(TripId=1, SeatId)` via the exact SQL shape used by `BookTicketsAsync`:

| Database setting | Session A | Session B | Resulting active tickets on the seat |
|---|---|---|---|
| `READ_COMMITTED_SNAPSHOT OFF` (current dev setting) | inserted | `Msg 51002 … Another active ticket already holds this seat for the same trip.` | 1 — correct |
| `READ_COMMITTED_SNAPSHOT ON` | inserted | inserted | **2 — seat sold twice** |

The trigger only works because lock-based `READ COMMITTED` blocks its `EXISTS` read on the other session's uncommitted row. Under row-versioning it reads a stale snapshot and passes. A single `ALTER DATABASE … SET READ_COMMITTED_SNAPSHOT ON` — a routine performance change — silently removes the system's core business invariant. R5 requires a database-enforced invariant; today there is none.

### Maturity

| Level | Verdict |
|---|---|
| **Level 1 — Demo/Coursework** | **Ready.** The application builds clean, migrates itself, seeds its own schedule, and demonstrates search → seat selection → booking → payment → e-ticket → admin settlement end to end. |
| **Level 2 — Production** | **Not ready.** Blocking: client-side trust boundary (D-001), plaintext-password fallback (D-002), no declarative seat constraint (B-001), no login lockout or failed-login logging (D-003), no audit trail for privilege grants (D-009), unencrypted SQL transport by default (D-010), a charged-but-unfulfilled VNPay path with no reconciliation (C-001), no trip-cancellation workflow (C-003), and a 7-hour local/UTC clock skew in scheduling (C-005). |

## 2. Scorecard

| Axis | Score | Reasoning and key evidence |
|---|---:|---|
| **A — Architecture / MVVM** | 5/10 | Clean view/VM split and a working navigation service, but no composition root (`Views/MainWindow.xaml.cs:15-22` hand-wires a ternary factory, duplicated at `ViewModels/Admin/AdminMainViewModel.cs:76-85`), no DI package, ViewModels construct Windows (`BookingConfirmationViewModel.cs:166-178`) and OS dialogs (`TicketViewModel.cs:117-125`), and 11 ViewModels reach into `Service.Instance` singletons. Business logic is not reusable by an API/web host: `Data/` and `Services/` depend on the static `CurrentUser` and three services require a WPF dispatcher (`AUD-A-012`). |
| **B — Data / SQL Server** | 4/10 | Strong: parameterisation everywhere, `DECIMAL` money, per-call connections with `await using`, Serializable + `UPDLOCK,HOLDLOCK` state machines, atomic script+history commits. Weak: no declarative seat-uniqueness constraint (proven exploitable, AUD-B-001), trigger `ROLLBACK TRANSACTION` zombies the ambient `SqlTransaction` (AUD-B-002), no index leading on `Tickets.TripId` or `TicketSeats.SeatId` for the hot availability path (AUD-B-003), no transient-fault retry or `CommandTimeout` anywhere (AUD-B-006), unbounded admin list queries (AUD-B-012), migration runner has no checksums and no rollback (AUD-B-009) and records `001_InitialSchema` as applied without executing it on legacy databases (AUD-B-010). |
| **C — Business logic** | 6/10 | 18/28 workflows complete and correctly transactional. Gaps are real and customer-facing: late VNPay success leaves the customer charged with no ticket and no reconciliation (AUD-C-001), declined callbacks keep the seat held while telling the user it was released (AUD-C-004), no trip-cancellation workflow at all (AUD-C-003), cash-on-board tickets are auto-cancelled at departure (AUD-C-009), and departure times are stored in local wall-clock while every predicate uses `SYSUTCDATETIME()` (AUD-C-005). |
| **D — Security** | 3/10 | Password hashing, SQL parameterisation, VNPay HMAC verification, `Process.Start` handling and deserialisation are all correct; no secret is in the tree or in git history. But the trust boundary is the client (D-001), plaintext still authenticates (D-002), there is no lockout and no failed-login record (D-003), station scope is enforced only in ViewModels and is derived from a cleartext third-party IP lookup (D-005, D-008), privilege grants are not audited (D-009), and the shipped connection string is `Encrypt=False;TrustServerCertificate=True` (D-010). |
| **E — WPF performance / stability** | 4/10 | 35 `new RelayCommand(async …)` async-void command sites despite a correct `AsyncRelayCommand` already in the repo (AUD-E-001); singleton events subscribed with lambdas and never unsubscribed, leaking one ViewModel per navigation and re-running DB queries on dead instances (AUD-E-002, E-003); the shell `ScrollViewer` measures at infinite height, neutering every declared `VirtualizingStackPanel` (AUD-E-004); no `CancellationToken` in any UI-facing data method (AUD-E-007); `DispatcherUnhandledException` marks everything handled (AUD-E-011). `PaymentWindowViewModel` is the counter-example done right. |
| **F — UI/UX / accessibility** | 4/10 | Consistent theme, good customer-side empty/loading states, 16 views bind a busy indicator. But `AutomationProperties` appears 9 times across 6 of 22 views and the entire booking funnel has none (AUD-F-001); zero validation infrastructure project-wide — no `INotifyDataErrorInfo`, no `Validation.ErrorTemplate` (AUD-F-003); localisation covers the chrome but not the content, leaving a permanently mixed-language UI (AUD-F-002); `TabIndex`/`FocusManager`/`KeyboardNavigation` appear zero times (AUD-F-004). |
| **G — Code quality** | 5/10 | Nullable enabled, no `SELECT *`, meaningful XML docs on the hard methods, clean build. But no analyzers, no `.editorconfig`, no `TreatWarningsAsErrors` — so the clean build proves little (AUD-G-001/002); `N'Đã đặt'` is duplicated on 28 lines across 7 files while `TicketStatuses` constants sit unused by the SQL (AUD-G-003); configuration is static with no `IOptions` and no startup validation (AUD-G-004); two 1000+ line static god-classes (AUD-G-007). |
| **H — Testing / CI** | 3/10 | 53 tests pass, security tests are genuine, and CI builds and tests on every push. But the only test of the core invariant is skipped by default and in CI (AUD-H-001); no test exercises booking, settlement, expiry or refund happy paths (AUD-H-002); tests mutate process-global singletons without fixtures (AUD-H-007); coverlet is referenced but coverage is never collected (AUD-H-004); no format check, no analyzer gate, no artifact, no versioning (AUD-H-003/005/006). |
| **I — Documentation** | 4/10 | Two substantial Vietnamese documents exist and are mostly accurate. But there is no root `README.md` (AUD-I-001), no ordered developer setup (AUD-I-002), no ERD (AUD-I-003), no migration-authoring or rollback procedure (AUD-I-004), no deployment or troubleshooting runbook (AUD-I-006), and `PROJECT_DOCUMENTATION.md:371` claims there is no CI/CD while `.github/workflows/build-test.yml` exists (AUD-I-007). |
| **J — Extensibility / production readiness** | 3/10 | See §6. |
| **Overall** | **4.1/10** | Solid demo-grade product with a genuinely good data-access discipline, blocked from production by its trust model, one missing database constraint, and the absence of executable proof for its own core invariant. |

## 3. Findings by axis

Full finding records — ID, severity, type, evidence, impact, proposed fix, behaviour-change classification, effort, risk, dependencies, acceptance criteria, verification method — are carried in `02-upgrade-backlog.md`, which is the authoritative tracker. This section states the substance per axis.

### A — Architecture and MVVM

- **AUD-A-001 (Critical)** `BookingConfirmationViewModel.cs:166-178` constructs `PaymentWindow` and calls `ShowDialog()`. The revenue path cannot run headlessly.
- **AUD-A-002 (Critical)** `AdminDatabaseService.cs:10-14` and `PaymentService.cs:361` authorise against the process-global `CurrentUser` (`Models/UserSession.cs:11-14`). No seam exists to pass a caller identity; a multi-user host would authorise request B against user A.
- **AUD-A-003 (High)** No composition root. `Views/MainWindow.xaml.cs:15-22` and `AdminMainViewModel.cs:76-85` each hand-wire a ternary ViewModel factory.
- **AUD-A-004 (High)** `TicketPrintService.cs:305-309` owns `PrintDialog`; `TicketViewModel.cs:117-125` owns `SaveFileDialog`. Export is welded to WPF STA.
- **AUD-A-005 (High)** `AdminMainViewModel` depends on the concrete `NavigationService`; `MainViewModel` on `INavigationService`. Two conventions for one dependency.
- **AUD-A-006 (High)** `AdminBusManagementViewModel.cs` is 995 lines owning form state, validation, pricing, draft CRUD, filtering and persistence.
- **AUD-A-007 … A-015 (Medium/Low)** booking orchestration living in a ViewModel; duplicated logout policy across two code-behinds; `INavigationService` missing `INotifyPropertyChanged` forcing a runtime type test (`MainViewModel.cs:90-92`); `Activator` fallback that silently produces a detached `NavigationService`; singleton reach-through in 11 ViewModels; three services requiring a WPF dispatcher — the concrete blocker to API reuse; per-bus batch registration with no rollback; ViewModel disposal owned by the View instead of the navigator; no data-access seam.

**Reuse verdict:** business logic is **not** reusable by an API/web/mobile host without rework. Blockers, in order: `CurrentUser` ambient identity (A-002), WPF dispatcher dependencies in `Services/` (A-012), UI-owned dialogs in the booking and export paths (A-001, A-004), and the absence of any data abstraction (A-015).

### B — Data and SQL Server

Schema after all ten migrator steps: `Accounts`, `Routes`, `Buses`, `Trips`, `Seats`, `Tickets`, `TicketSeats`, `Payments`, `BusCompanies`, `__DatabaseMigrations`.

**Double-sale audit — UI → ViewModel → Service → Data → Database.** `SeatSelectionViewModel` → `BookingConfirmationViewModel.CompleteBookingAsync` → `DatabaseHelper.BookTicketsAsync` (`:560`) → one `SERIALIZABLE` transaction (`:604`) inserting one `Tickets` row then one `TicketSeats` row per seat, each guarded by `NOT EXISTS` (`:595-600`) → `TR_TicketSeats_AntiDoubleSeat` (`Migration_006:59-88`).

Answer to "can two concurrent transactions purchase the same seat": **through the application, no — under the current database settings.** Against the database directly, **yes**, and even through the application if `READ_COMMITTED_SNAPSHOT` is ever enabled, because the last declarative constraint was dropped at `Migration_006:48-49` and the only `UNIQUE` on the junction table is `(TicketId, SeatId)`, which cannot constrain two *different* tickets. Proven experimentally — see §1. UI validation is not counted as protection.

Other material findings: trigger `ROLLBACK TRANSACTION` versus the caller's `SqlTransaction.RollbackAsync` (B-002); no index leading on `Tickets.TripId` or `TicketSeats.SeatId` while the availability subqueries at `DatabaseHelper.cs:252` and `:538` run per row (B-003); `DeleteBusAsync` (`AdminDatabaseService.cs:928-935`) runs `BEGIN TRANSACTION` with no `SET XACT_ABORT ON`, so a failing second `DELETE` commits the seat wipe and still reports success (B-004); unlocked check-then-insert for trip overlap (B-005); no retry and no `CommandTimeout` anywhere (B-006); development seed applied unconditionally to any database (B-007); untracked DDL outside the migration history (B-008); no checksums, no rollback, C#-owned ordering (B-009); `skipWhenAccountsExist` records `001_InitialSchema` as applied without running it (B-010); admin list fan-out and unbounded reads (B-011, B-012); write-on-read company sync (B-013); N+1 inserts inside open transactions (B-014).

Clean: no `float`/`money` for currency, no `SELECT *`, correct connection lifecycle, correct parameterisation, no `DROP DATABASE`/`TRUNCATE`/unguarded `DELETE` in any script.

### C — Business logic and functionality

| Workflow | Status | Evidence |
|---|---|---|
| Login, registration, profile, change password | Complete | `DatabaseHelper.cs:28, :69, :89, :111` |
| Forgot-password / self-service reset | **Missing** | admin-only `AdminDatabaseService.cs:1146` |
| Route search, company browse/detail, seat map | Complete | `DatabaseHelper.cs:246, :338, :405, :535` |
| Booking, single and multi-seat | Complete | `DatabaseHelper.cs:549, :560` |
| Payment — cash on board | Partial | `PaymentService.cs:69`; no collection workflow (C-009) |
| Payment — bank transfer QR | Partial | static JPEG asset, no amount/reference encoded (C-008) |
| Payment — VNPay | Implemented but unreachable | `BookingConfirmationViewModel.cs:80-88` comments the option out (C-011) |
| VNPay callback verification | Signature complete, settlement partial | `PaymentService.cs:227`; C-001, C-004, D-004 |
| Payment expiry sweep | Complete, non-atomic | `PaymentService.cs:332` (C-010) |
| Cancellation (unpaid hold) / refund request / refund approval | Complete / Complete / Partial | `DatabaseHelper.cs:675, :745`; `PaymentService.cs:445` (C-006) |
| Ticket auto-use, operator check-in | Complete / Partial | `TicketLifecycleService.cs:15, :29` (C-013) |
| QR generation, printing, PDF, PNG, e-mail | Complete (e-mail manual only) | `TicketPrintService.cs:279, :303, :350, :312`; `ExternalApiServices.cs:47` (C-012) |
| **QR verification / scanning** | **Missing** | no decoder anywhere; check-in is grid selection (C-013) |
| Revenue reports, settlement console, admin CRUD | Complete | `AdminDatabaseService.cs:223`; `PaymentService.cs:367, :411` |
| **Trip cancellation** | **Missing** | no `Trips.Status`; `DeleteTripAsync` refuses once a ticket exists (C-003) |
| **Commission / operator payout** | **Missing** | zero code or schema artefacts (B-109) |
| Bus-company registration | Partial | side effect of registering a bus (B-105) |

Edge cases: payment-then-ticket writes are atomic; ticket-then-payment is a best-effort compensation whose result is discarded (C-007); crash mid-batch is safe for single-statement state machines and unsafe across booking→payment and across the two-statement expiry sweep (C-010); printer failure is handled; DB connection loss degrades on most read paths but throws on three (C-014).

### D — Security

Blocker and criticals are summarised in §1. Additional: no lockout/backoff/failed-login logging (D-003); VNPay settles from the browser return rather than an IPN and ignores `vnp_TransactionStatus` (D-004); station scope enforced only in two ViewModels and derived from cleartext `http://ip-api.com` (D-005, D-008); pre-verification log write from an unauthenticated loopback endpoint gives a CRLF log-injection primitive (D-006); the listener handles callbacks serially with no method/Host/rate checks (D-007); no audit trail for role grants (D-009); `Encrypt=False;TrustServerCertificate=True` shipped default (D-010); username-enumeration timing oracle because a miss returns before any key derivation (D-011); usernames and e-mail addresses retained 30 days in plain-text logs beside the binary (D-012); admin password reset bypasses the complexity policy and forces no change at next login (D-013); no session expiry and no re-validation of a demoted role (D-014); `.gitignore` does not exclude `appsettings*.json` overrides and CI has no secret or vulnerability scanning (D-015).

Verified clean: no SQL injection; no path traversal; no unsafe deserialisation; no command injection; `RequireAdmin()` present on all 22 admin data-layer entry points; no secret in the tree; no secret in git history (verified in this session with `git log --all -S`).

### E — WPF performance and stability

See scorecard. Highest value: E-001 (async-void commands, fix already in the repo and unused), E-002/E-003 (unsubscribed singleton events leaking a ViewModel per navigation and issuing DB queries from dead instances), E-004 (shell `ScrollViewer` defeating all declared virtualization), E-011 (`e.Handled = true` on every unhandled UI exception).

### F — UI/UX and accessibility

Measured: `AutomationProperties` 9 occurrences in 6 of 22 views; `TabIndex`/`KeyboardNavigation`/`FocusManager` 0; validation infrastructure 0; `IsDefault` 4, `IsCancel` 3, `KeyBinding` 1; 16 `ItemsControl` + 1 `ListBox`, 9 virtualized; 0 `DataGrid`. Findings F-001 … F-014 cover screen-reader reachability of the booking funnel, mixed-language UI, absent per-field validation, tab order and initial focus, the unbounded reports list, confirmation dialogs without focus traps, fixed pixel sizing that breaks 1280×720 @150%, disabled-state reasons only in ToolTips, and status banners with no live-region annotation.

Because the GUI must not be launched in this environment, every F item carries a manual checklist row rather than a claim of visual verification.

### G — Code quality

See scorecard. G-001/G-002 (no analyzers, no `.editorconfig`) are the enabling gap: they are why the 0-warning baseline is not evidence. G-003 (28 duplicated status literals) is the highest-risk maintainability defect because the SQL predicate that defines seat occupancy is retyped in eight places and has **already drifted** — `Schema1.sql:72` omits `N'Đã sử dụng'` from the index filter that `Migration_005_Settlement.sql:76` includes.

### H — Testing and CI/CD

Is there a test proving two concurrent attempts to sell the same seat cannot both succeed? **No.** The only candidate is skipped by default and in CI, and it exercises only the single-seat overload. See H-001/H-002.

### I — Documentation and maintainability

No root README; no ordered setup; no ERD; no migration-authoring or rollback guidance (and `AGENTS.md:R6` mandates inspecting a `Down()` that this migration system does not have); no env-var reference table with required/default/consequence; no deployment or troubleshooting runbook; stale CI/CD claim; three overlapping architecture descriptions across two languages and two directories.

### J — Extensibility and production readiness

| Capability | State |
|---|---|
| Business core separated from UI | No — `CurrentUser` ambient identity, WPF dispatcher in `Services/`, dialogs in ViewModels |
| API / web reuse | Not possible without A-002, A-012, A-001, A-004, A-015 |
| Multi-user operation | Technically works; **not safe** — every client is a trusted DB principal (D-001) |
| Concurrency | Application-level correct today; database-level unprotected (B-001) |
| Printing / PDF / QR generation | Working, WPF-bound |
| QR verification | Missing |
| Payment integration | VNPay implemented, disabled in the UI, browser-return settlement only |
| Reports | Working, gross revenue only; no commission/payout model |
| Monitoring | None — local rolling text file, no metrics, no health check |
| Logging | Serilog file sink; no audit table; log is injectable from an unauthenticated local endpoint |
| Centralised configuration | None — static accessors, no validation, no `IOptions` |

## 4. Gaps / technical limitations / risks

- **Gaps** (something required is absent): B-001, B-003, B-005, B-007-010, C-003, C-011, C-012, C-013, D-002, D-003, D-005, D-009, D-011, D-013, D-015, E-007, F-001-004, F-006, F-008-011, F-013, G-001-004, G-013, H-001-006, H-009-012, I-001-006, I-009, B-102, B-108, B-109.
- **Technical limitations** (the design cannot do this without rework): A-002, A-004, A-012, A-015, B-006, B-009, C-008, E-008, E-012, E-014, E-015, F-007, G-007, G-008, G-012, B-107, B-111.
- **Risks** (works today, fails under a plausible change): B-002, B-004, B-011, B-013, B-014, C-002, C-005, C-007, C-010, C-014, C-015, D-001, D-004, D-006-008, D-010, D-012, D-014, E-001-006, E-009-011, E-013, F-005, F-012, F-014, G-005, G-006, G-009-011, G-014, H-007, H-008, I-007, I-008, B-101, B-103, B-104, B-105, B-106, B-110.

## 5. Do NOT do

| Change | Why unnecessary now | Risk of doing it | What would justify reconsidering |
|---|---|---|---|
| Full rewrite | 12.7k LOC of working, largely correct code; every real defect is a bounded, local fix | Loses the correct Serializable/`UPDLOCK` discipline and PBKDF2 implementation already in place | Nothing observed |
| Replace SQL Server / change engine | SQL Server features (filtered indexes, indexed views, `HOLDLOCK`, `rowversion`) are exactly what the fix for B-001 needs | Would delete the only asset that can enforce the core invariant | Nothing observed |
| Replace WPF | No evidence of a WPF-caused defect; the stability findings are code-level, not framework-level | Discards 26 views and a coherent theme | A genuine cross-platform or web requirement |
| Introduce EF Core | Raw ADO.NET here is parameterised, explicit and fast; the booking transaction depends on exact SQL semantics | Rewriting the seat-hold SQL under an ORM is the single most likely way to reintroduce double-selling | A large increase in schema surface or a team unfamiliar with ADO.NET |
| Microservices / distributed messaging / Kubernetes | One desktop app, one database, single-site operation | Operational cost with no benefit at this scale | Multi-site deployment with independent failure domains |
| CQRS / event sourcing | Reads and writes hit the same small schema; no read-model pressure exists | Doubles the model count for no measured gain | Reporting load that measurably degrades transactional work |
| Mass package upgrade | Zero vulnerable packages; every package is at most one minor behind | Breaking-change risk with no security or bug justification (R10) | A published CVE, or a bug fix the project actually needs |
| Speculative abstraction layers | The needed seams are specific and named (A-001, A-002, A-012, A-015) | Generic "clean architecture" scaffolding hides the three real trust problems | Nothing observed |
| Rewriting git history to purge secrets | No secret exists in the tree or in history (verified) | Destroys the only shared reference points | An actual secret discovered later — then rotate first, rewrite only on explicit approval |
