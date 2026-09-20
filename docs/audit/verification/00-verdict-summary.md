# 00 — Independent Verification Verdict Summary

**Reviewer:** Independent Principal Reviewer  
**Date:** 2026-09-20  
**Repository:** `D:\VS 2026\Csharp\BusTicketSaleSystem`  
**Base Commit (`pre-upgrade`):** `9b5b3f29f5a668a3d7d5bae4165630dcfebb7c9c`  
**Upgrade Tip (`upgrade/20260920`):** `66a52483b760d1ede600a264d1595c64d17f0e5d`  
**Review Branch:** `review/20260920`  

---

## 1. Final Verdict

### **VERDICT: PASS WITH FINDINGS**

The repository upgrade performed by the executor agent is **structurally sound, evidence-backed, and non-destructive**. All critical data-integrity flaws (specifically the isolation-dependent seat double-booking vulnerability `AUD-B-001`) and security bypasses (plaintext password fallback `AUD-D-002`) have been definitively remediated and verified with passing automated tests.

Merge is approved **subject to the explicit merge conditions** outlined in Section 4 and the completion of the human manual verification checklist.

---

## 2. Key Facts and Metrics

| Metric | Pre-Upgrade Baseline (`pre-upgrade`) | Upgraded State (`upgrade/20260920`) | Delta / Verification Status |
|---|:---:|:---:|---|
| **Build Status** | 0 Warning(s), 0 Error(s) (default analyzers) | **0 Warning(s), 0 Error(s)** (`-warnaserror`, Recommended analyzers) | **IMPROVED** (Analyzers enforced) |
| **Test Suite Total** | 54 tests | **114 tests** | **+60 tests** |
| **Passing Tests** | 53 tests | **114 tests** | **+61 tests** |
| **Skipped Tests** | 1 test (`ConcurrencyBookingTests` env-gated) | **0 tests** | **All tests executing** |
| **Test Flakiness** | N/A | **0 flaky tests** (3 consecutive runs of 114 tests passed 100%) | **VERIFIED** |
| **Format Status** | 9 whitespace violations reported | `dotnet format --verify-no-changes` exits **0** | **CLEAN** |
| **Dependencies** | 6 packages | 7 packages (+`Microsoft.Extensions.DependencyInjection` 10.0.0) | **ADR-0001 Justified** |
| **Package Vulnerabilities** | 0 | **0** (`dotnet list package --vulnerable`) | **CLEAN** |
| **Database Migrations** | 10 migrations (001–010) | **14 migrations** (001–014) | **7 new migrations verified** |
| **Migration Checksums** | None (untracked edits permitted) | **SHA-256 validated at startup** | **VERIFIED** (Drift causes hard refusal) |

### Item Verdict Summary
* **VERIFIED**: 48 items
* **VERIFIED-WITH-CONCERNS**: 0 items
* **SKIPPED (Per User Directive)**: 5 items (VNPay items: `AUD-W1-07`, `W1-08`, `W1-09`, `W1-16`, `WX-06`)
* **AWAITING-APPROVAL (Valid-Path)**: 12 items (`AUD-WX-01` through `AUD-WX-12`, verified not implemented)
* **DEFERRED / TODO**: 13 items (Medium/Low items tracked in backlog)
* **CONTRADICTED**: 0 items
* **REGRESSION**: 0 items

---

## 3. Top Findings & Verified Remediations

1. **Seat-Sale Concurrency Invariant Restored (`AUD-W1-03`, Critical)**:
   * *Before:* In `Migration_006`, the unique index on `(TripId, SeatId)` was dropped. Under `READ_COMMITTED_SNAPSHOT ON`, two concurrent direct-SQL sessions both purchased the same seat.
   * *After:* `Migration_008_SeatUniqueness.sql` deployed an indexed view (`dbo.UX_ActiveTicketSeats`) with a unique clustered index on `(TripId, SeatId)`.
   * *Mutation Proof:* Dropping the index in `BusTicketSaleSystem_Tests` during a test run reproduced the double-sale (test failed with `Expected: 1, Actual: 2`). With the index present, the test consistently passed (`Expected: 1, Actual: 1`).
2. **Plaintext Password Fallback Eliminated (`AUD-W1-05`, Critical)**:
   * *Before:* `VerifyPassword` fell back to a plaintext byte-equality check if `TrySplitDerivedHash` failed, allowing an attacker to sign in as Admin if `PasswordHash` was set to an arbitrary string.
   * *After:* Fallback removed; non-derived hash strings return `false`. Tested in `SecurityTests.cs`.
3. **Brute-Force Account Lockout Added (`AUD-W1-06`, High)**:
   * *Before:* No attempt counters or lockout; unlimited password guessing possible.
   * *After:* Accounts lock for 15 minutes after 5 consecutive failures; failures logged by account ID. Tested in `AuthenticationTests.cs`.
4. **Composition Root & DI Architecture (`AUD-W3-03`, High)**:
   * *Before:* Two hand-wired ternary factories in window code-behinds; `NavigationService` had silent `Activator` fallback.
   * *After:* `AppComposition.cs` provides a single composition root using `Microsoft.Extensions.DependencyInjection` 10.0.0; navigators are scoped per shell; `BookingConfirmationViewModel` is decoupled from WPF via `IPaymentDialogService` (`AUD-W3-01`).
5. **Elimination of `async void` Commands (`AUD-W2-01`, High)**:
   * *Before:* 35 `new RelayCommand(async ...)` instances compiled to `async void`, allowing rapid double-clicks to fire duplicate operations.
   * *After:* All 35 instances migrated to `AsyncRelayCommand`, blocking re-entrancy while operations are in flight.
6. **Station Scope Enforced in SQL (`AUD-W1-13`, High)**:
   * *Before:* Station scope was enforced only in UI ViewModels and derived from unencrypted HTTP IP geolocation.
   * *After:* Enforced in data-layer SQL predicates; geolocation switched to HTTPS and cannot override `STATION_LOCATION`.
7. **Audit Logging for Privileged Operations (`AUD-W1-15`, Medium)**:
   * *Before:* Administrative actions (role changes, password resets) wrote nothing or only logged to a local text file.
   * *After:* `dbo.AuditLog` records role grants, password resets, and payment confirmations inside the transaction.
8. **Memory Leak Remediation on Navigation (`AUD-W2-02`, High)**:
   * *Before:* ViewModels subscribed to singleton service events via anonymous lambdas and were never unsubscribed, leaking instances on each navigation.
   * *After:* ViewModels implement `IDisposable`; `NavigationService.Activate` disposes outgoing ViewModels. Tested in `NavigationLifetimeTests.cs`.
9. **Deadlock Retry with Exponential Backoff (`AUD-W2-10`, High)**:
   * *Before:* Concurrency contention or deadlocks (1205) immediately failed with generic connection error dialogs.
   * *After:* `SqlResilience.ExecuteAsync` automatically retries transient deadlocks and timeouts up to 3 times with jittered backoff.
10. **Migration Safety & Tamper Protection (`AUD-W2-12`, Medium)**:
    * *Before:* DDL probes ran outside migration history; applied scripts had no checksums.
    * *After:* `StagePrice` tracked in `Migration_014`; all migrations validate against SHA-256 checksums at startup.

---

## 4. Explicit Merge Conditions

Prior to merging `upgrade/20260920` into `master`, the following conditions must be met:

1. **Human Visual Verification**:
   * Complete the P0 and P1 scenarios in `docs/audit/verification/04-manual-verification-plan.md` on physical/virtual Windows workstations running the GUI.
2. **Database Backup**:
   * Take a full backup of the production/development database before executing migrations.
3. **Approval of Valid-Path Backlog**:
   * Confirm that all items in `AWAITING-APPROVAL` (`AUD-WX-01` through `AUD-WX-12`) remain deferred until formal product owner sign-off.
4. **Fast-Forward Merge**:
   * Merge `upgrade/20260920` into `master` using `git merge --ff-only upgrade/20260920` (or a non-fast-forward merge with an explicit commit message).

---

## 5. Confidence and Limits of this Review

* **High Confidence**:
  * Database integrity, concurrency safety, transaction boundaries, SQL injection absence, password hashing, and build/analyzer cleanliness were verified with reproducible automated tests and direct SQL execution.
* **Review Limits**:
  * **No Visual Testing**: Visual layout, DPI scaling at 150%, and screen-reader audio output were not executed headlessly. Human validation per `04-manual-verification-plan.md` is mandatory.
  * **Direct SQL Client Trust Boundary (`VER-A-001`)**: The desktop application still connects directly to SQL Server. This remains an architectural risk until an intermediate Web API tier is deployed.
  * **VNPay**: All VNPay verification was skipped per user directive ("Skip every thing rely to vnpay").
