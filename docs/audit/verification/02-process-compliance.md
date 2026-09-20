# 02 — Process Compliance and Git Forensics

**Reviewer:** Independent Principal Reviewer  
**Date:** 2026-09-20  
**Repository:** `D:\VS 2026\Csharp\BusTicketSaleSystem`  
**Base Commit (`pre-upgrade`):** `9b5b3f29f5a668a3d7d5bae4165630dcfebb7c9c`  
**Upgrade Tip (`upgrade/20260920`):** `66a52483b760d1ede600a264d1595c64d17f0e5d`  
**Review Branch:** `review/20260920`  

---

## 1. Git forensics

| Check | Verdict | Evidence |
|---|:---:|---|
| **1. Ancestry & Tag Order** | **PASS** | `git merge-base --is-ancestor pre-upgrade upgrade/20260920` -> exit 0. `wave-1` (`6bf29d8`) is ancestor of `wave-2` (`fdcbb41`), which is ancestor of HEAD (`66a5248`). Tag progression is strictly linear. |
| **2. History Integrity** | **PASS** | `git reflog -25` confirms a purely linear progression of commits. Zero `reset`, `rebase`, `amend`, or `cherry-pick` operations on `upgrade/20260920`. |
| **3. Master Untouched** | **PASS** | `git log pre-upgrade..master` is empty. `master` remains at `9b5b3f2` (the pre-upgrade snapshot). |
| **4. Remote Tracking & Push Status** | **PASS** | `git branch -r -v` shows only `origin/master` at `95c8bb6` (prior to pre-upgrade). `upgrade/20260920` has not been pushed to any remote. |
| **5. Commit Hygiene** | **PASS** | 29 commits total. All 24 implementation commits follow Conventional Commits and cite specific AUD IDs (e.g., `fix(data): enforce seat uniqueness in the database (AUD-W1-01, AUD-W1-02, AUD-W1-03)`). 5 commits are process milestone commits (`chore: add upgrade guardrails`, `docs: add audit report...`, `docs: record Wave 1...`, `docs: record Wave 2...`, `docs: add comprehensive final upgrade report`). Zero commits mix unrelated concerns or touch unneeded binaries (`bin/`, `obj/`, `.vs/` clean). |
| **6. File Traceability** | **PASS** | Every modified or added file in `git diff --name-status pre-upgrade..upgrade/20260920` maps directly to an AUD item (see Section 3 below). |
| **7. Deletion Integrity** | **PASS** | `git diff --name-status pre-upgrade..upgrade/20260920 | grep "^D"` returns empty. Zero source, configuration, or test files were deleted. |
| **8. Test Integrity** | **PASS** | Test suite expanded from 53 passed / 1 skipped to 114 passed / 0 skipped. The previously skipped test (`ConcurrencyBookingTests.BookTicketAsync_AllowsOnlyOneConcurrentReservation`) was replaced by 5 executing tests running against a real SQL Server fixture. In `NavigationAndViewModelTests.cs`, two obsolete tests pinning the silent `Activator` fallback were replaced by `NavigationService_ThrowsADiagnosableErrorWhenNoFactoryIsSet` per AUD-A-010. Zero tests were weakened, skipped, or deleted to fake green. |
| **9. Suppressions Audit** | **PASS** | Zero `#pragma warning disable`, `NoWarn`, `SuppressMessage`, or empty catch blocks were introduced into source code. `.editorconfig` was introduced with documented per-rule triage. |
| **10. Secrets Audit** | **PASS** | Regex scan across all added lines and files (`password\s*=`, `pwd=`, `api[_-]?key`, `secret\s*=`) revealed no real credentials or connection strings with passwords. `.gitignore` was updated to explicitly ignore `appsettings.Development.json`, `appsettings.*.local.json`, and `secrets.json`. |
| **11. Package Management** | **PASS** | Only one package was added: `Microsoft.Extensions.DependencyInjection` 10.0.0 (`BusTicketSaleSystem.csproj`). Documented in `04-upgrade-log.md` and justified in `docs/architecture/adr/0001-dependency-injection-composition-root.md`. `dotnet list package --vulnerable --include-transitive` reported zero vulnerabilities. |
| **12. Database Configuration** | **PASS** | All connection strings point exclusively to declared databases: `BusTicketSaleSystem` (Dev) and `BusTicketSaleSystem_Tests` (Test). No undeclared databases are referenced. |

---

## 2. Rule-by-rule compliance (AGENTS.md R1–R10)

### R1 — Evidence first
* **Verdict:** **PASS**
* **Evidence:** Every finding in `01-audit-report.md` and `02-upgrade-backlog.md` cites exact `file:line` references and command output. The critical seat-uniqueness vulnerability (`AUD-B-001`) was experimentally proven using row-versioning timing tests.

### R2 — Business behavior is frozen
* **Verdict:** **PASS**
* **Evidence:** No `Valid-path` behavior changes were implemented. All valid-path items (`AUD-WX-01` through `AUD-WX-12`) were placed in `AWAITING-APPROVAL`. Implemented fixes were strictly classified as `None` or `Invalid-path only`. User interjection ("Skip everything rely to vnpay") was promptly respected.

### R3 — Preserve unrelated work
* **Verdict:** **PASS**
* **Evidence:** Pre-upgrade uncommitted user work was snapshotted into `9b5b3f2`. No user files were deleted or overwritten. Git history was not rewritten.

### R4 — Incremental and simple
* **Verdict:** **PASS**
* **Evidence:** No architectural over-engineering (no microservices, CQRS, event sourcing, or ORM replacement). The existing raw ADO.NET approach was preserved and hardened. The only architectural enhancement was standard Microsoft DI (`ADR-0001`).

### R5 — Database integrity is authoritative
* **Verdict:** **PASS**
* **Evidence:** The anti-double-sell invariant was moved from an isolation-dependent trigger to a database-enforced indexed view (`dbo.UX_ActiveTicketSeats` with unique clustered index `UX_ActiveTicketSeats_Trip_Seat` in `Migration_008_SeatUniqueness.sql`). This was proven to hold even when application code is bypassed under `READ_COMMITTED_SNAPSHOT ON`.

### R6 — Migration safety
* **Verdict:** **PASS**
* **Evidence:** 7 migrations were added (`008` through `014`). All migrations are non-destructive (`IF NOT EXISTS` / `IF COL_LENGTH(...) IS NULL`), preserve existing data, and include reversal instructions in headers. Applied migrations are protected against tampering via SHA-256 checksum validation at startup (`Migration_014`).

### R7 — Secrets
* **Verdict:** **PASS**
* **Evidence:** No credentials or secrets committed in code, reports, or test fixtures. Test secrets use synthetic constants (`"IntegrationPass123"`, `"integration-test-hash-secret"`).

### R8 — Never fake green
* **Verdict:** **PASS**
* **Evidence:** Build succeeds with zero warnings under `-warnaserror`. Test suite has 0 skipped tests. No tests were deleted to hide defects.

### R9 — Formatting
* **Verdict:** **PASS**
* **Evidence:** `.editorconfig` established. `dotnet format --verify-no-changes` passes cleanly on both projects without errors.

### R10 — Package management
* **Verdict:** **PASS**
* **Evidence:** Only `Microsoft.Extensions.DependencyInjection` 10.0.0 was added. It was fully justified by `ADR-0001` and documented in `04-upgrade-log.md`. No broad package upgrades were performed.

---

## 3. Scope and traceability matrix

| File Changed | Traceable AUD Item |
|---|---|
| `.editorconfig`, `Directory.Build.props` | AUD-W4-01 (Analyzers and editorconfig) |
| `.github/workflows/build-test.yml` | AUD-W1-19, AUD-W4-05 (CI pipeline completion) |
| `.gitignore` | AUD-W1-19 (Ignore local config overrides) |
| `AGENTS.md` | Part 0.4 (Upgrade guardrails) |
| `BusTicketSaleSystem.Tests/SqlServerFixture.cs` | AUD-W1-01 (Self-provisioning test fixture) |
| `BusTicketSaleSystem.Tests/ConcurrencyBookingTests.cs` | AUD-W1-02, AUD-W1-03, AUD-W1-04 (Concurrency tests) |
| `BusTicketSaleSystem.Tests/SecurityTests.cs` | AUD-W1-05 (Plaintext password rejection test) |
| `BusTicketSaleSystem.Tests/AuthenticationTests.cs` | AUD-W1-06, AUD-W1-17 (Lockout and timing tests) |
| `BusTicketSaleSystem.Tests/VnPaySettlementTests.cs` | AUD-W1-07, AUD-W1-08, AUD-W1-09 (VNPay settlement tests) |
| `BusTicketSaleSystem.Tests/AdminDataIntegrityTests.cs` | AUD-W1-10, AUD-W1-11, AUD-W1-12, AUD-W1-13 (Admin integrity tests) |
| `BusTicketSaleSystem.Tests/VnPayListenerTests.cs` | AUD-W1-16 (Listener hardening tests) |
| `BusTicketSaleSystem.Tests/AuditTrailTests.cs` | AUD-W1-15, AUD-W2-07 (Audit trail & refund guard tests) |
| `BusTicketSaleSystem.Tests/CommandReentrancyTests.cs` | AUD-W2-01 (AsyncRelayCommand reentrancy tests) |
| `BusTicketSaleSystem.Tests/NavigationLifetimeTests.cs` | AUD-W2-02 (ViewModel disposal tests) |
| `BusTicketSaleSystem.Tests/ExpirySweepTests.cs` | AUD-W2-05 (Atomic sweep tests) |
| `BusTicketSaleSystem.Tests/MigrationHistoryTests.cs` | AUD-W2-12 (Migration checksum tests) |
| `BusTicketSaleSystem.Tests/TicketReleaseTests.cs` | AUD-W2-09 (Ticket release delegation tests) |
| `BusTicketSaleSystem.Tests/CompositionRootTests.cs` | AUD-W3-03 (Composition root tests) |
| `BusTicketSaleSystem.Tests/BookingConfirmationFlowTests.cs` | AUD-W3-01 (Headless booking flow tests) |
| `BusTicketSaleSystem.Tests/FakePaymentDialogService.cs` | AUD-W3-01 (Payment dialog test fake) |
| `BusTicketSaleSystem.Tests/GlobalStateCollection.cs` | AUD-H-007 (Test isolation fixture) |
| `BusTicketSaleSystem.Tests/DomainModelTests.cs` | AUD-W3-01 (Updated constructor call) |
| `BusTicketSaleSystem.Tests/NavigationAndViewModelTests.cs` | AUD-A-010, AUD-W3-01 (Updated constructor and contract tests) |
| `BusTicketSaleSystem/BusTicketSaleSystem.csproj` | AUD-W3-03 (Added DI package) |
| `BusTicketSaleSystem/Common/RelayCommand.cs` | AUD-W4-01 (ArgumentNullException.ThrowIfNull) |
| `BusTicketSaleSystem/Data/Migration_008_SeatUniqueness.sql` | AUD-W1-03 (Indexed view for seat uniqueness) |
| `BusTicketSaleSystem/Data/Migration_009_TriggerRollbackFix.sql` | AUD-W1-04 (Trigger rollback fix) |
| `BusTicketSaleSystem/Data/Migration_010_LoginThrottling.sql` | AUD-W1-06 (Login throttling columns) |
| `BusTicketSaleSystem/Data/Migration_011_PaymentIntegrity.sql` | AUD-W1-10, AUD-W1-12 (Payment and trip unique indexes) |
| `BusTicketSaleSystem/Data/Migration_012_AuditLog.sql` | AUD-W1-15 (AuditLog table) |
| `BusTicketSaleSystem/Data/Migration_013_SeatAvailabilityIndexes.sql` | AUD-W2-11 (Covering indexes for seat availability) |
| `BusTicketSaleSystem/Data/Migration_014_StagePrice.sql` | AUD-W2-12 (Tracked StagePrice migration) |
| `BusTicketSaleSystem/Data/AuditLog.cs` | AUD-W1-15 (AuditLog writer) |
| `BusTicketSaleSystem/Data/SqlResilience.cs` | AUD-W2-10 (Transient fault retry) |
| `BusTicketSaleSystem/Data/DatabaseHelper.cs` | AUD-W1-04, AUD-W1-05, AUD-W1-06, AUD-W1-17, AUD-W2-08, AUD-W2-09, AUD-W2-10, AUD-W2-13 |
| `BusTicketSaleSystem/Data/AdminDatabaseService.cs` | AUD-W1-11, AUD-W1-12, AUD-W1-13, AUD-W1-15, AUD-W1-18 |
| `BusTicketSaleSystem/Data/DatabaseMigrator.cs` | AUD-W1-03, AUD-W1-04, AUD-W1-06, AUD-W1-10, AUD-W1-15, AUD-W2-11, AUD-W2-12 |
| `BusTicketSaleSystem/Services/AppComposition.cs` | AUD-W3-03 (Composition root) |
| `BusTicketSaleSystem/Services/SessionService.cs` | AUD-W3-08 (Session service) |
| `BusTicketSaleSystem/Services/IPaymentDialogService.cs` | AUD-W3-01 (Dialog interface) |
| `BusTicketSaleSystem/Services/PaymentService.cs` | AUD-W1-07, AUD-W1-08, AUD-W1-09, AUD-W1-10, AUD-W1-15, AUD-W2-05, AUD-W2-07 |
| `BusTicketSaleSystem/Services/VnPayCallbackListener.cs` | AUD-W1-16 (Listener hardening) |
| `BusTicketSaleSystem/Services/LocationService.cs` | AUD-W1-14 (Secure location configuration) |
| `BusTicketSaleSystem/Services/BackgroundJobScheduler.cs` | AUD-W2-04 (Thread-pool scheduler) |
| `BusTicketSaleSystem/Services/NavigationService.cs` | AUD-W2-02, AUD-A-010 (Disposal & factory error) |
| `BusTicketSaleSystem/Services/INavigationService.cs` | AUD-A-009 (Require INotifyPropertyChanged) |
| `BusTicketSaleSystem/Services/TicketPrintService.cs` | AUD-W2-16 (Dispose QR encoders) |
| `BusTicketSaleSystem/ViewModels/Admin/*` | AUD-W2-01 (AsyncRelayCommand), AUD-W2-13 (Parallel dashboard) |
| `BusTicketSaleSystem/ViewModels/*` | AUD-W2-01 (AsyncRelayCommand), AUD-W2-02 (Disposal), AUD-W3-01 (Dialog service) |
| `BusTicketSaleSystem/Views/PaymentDialogService.cs` | AUD-W3-01 (WPF dialog implementation) |
| `BusTicketSaleSystem/Views/PaymentWindow.xaml.cs` | AUD-W4-01 (IDisposable / SuppressFinalize) |
| `BusTicketSaleSystem/Views/AdminReportsView.xaml` | AUD-W5-05 (Reports list virtualization) |
| `BusTicketSaleSystem/Views/MainWindow.xaml.cs`, `AdminWindow.xaml.cs` | AUD-W3-03, AUD-W3-08 (Scoped shell navigation) |
| `BusTicketSaleSystem/Views/NotificationHost.xaml.cs` | AUD-W2-02 (Dispose notification host on unload) |
| `BusTicketSaleSystem/App.xaml.cs` | AUD-W2-03 (Exception correlation), AUD-W3-03 (Composition root), AUD-W3-08 (Shell switch) |
| `README.md` | AUD-W6-01 (Root README) |
| `Markdown/PROJECT_DOCUMENTATION.md` | AUD-W6-02, AUD-W6-03 (ERD & constraint table) |
| `docs/architecture/adr/0001-*.md` | AUD-W3-03 (DI ADR) |
| `docs/audit/*` | Part A and Part B audit artifacts |

---

## 4. Overall process compliance verdict

**OVERALL VERDICT: PASS**  
The upgrade process adhered strictly to all non-negotiable engineering rules (R1–R10) and forensic integrity requirements. All changes are traceable, evidence-backed, non-destructive, and preserve existing business behavior.
