# 05 — Follow-up Backlog

**Reviewer:** Independent Principal Reviewer  
**Date:** 2026-09-20  
**Repository:** `D:\VS 2026\Csharp\BusTicketSaleSystem`  

The following items are new findings and architectural recommendations identified during the independent verification of the upgrade. These items are recorded for future planning and are **NOT** implemented in this review session.

---

### VER-A-001 · Direct SQL client connection trust boundary
- **Severity:** Blocker · **Type:** Risk
- **Evidence:** `BusTicketSaleSystem/Data/DatabaseHelper.cs:26` (`GetConnection() => new(DatabaseOptions.ConnectionString)`); client connects directly to SQL Server with Integrated Security.
- **Impact:** Administrative role checks and business rules are advisory if an attacker has network access to SQL Server. A malicious user with Windows access could execute direct DML queries against the database.
- **Proposed fix:** Transition the desktop application to communicate through an authenticated, reverse-proxied ASP.NET Core Web API / gRPC service. The database should only accept connections from the API service account.
- **Behavior change:** Valid-path (architectural deployment change)
- **Effort:** XL · **Risk:** High
- **Dependencies:** None
- **Acceptance criteria:** Direct client connections to SQL Server are disabled; all ticketing operations flow through the authenticated API tier.
- **Verification method:** Attempt to query SQL Server directly from a client workstation; verify connection is refused.
- **Status:** TODO

---

### VER-B-001 · Timezone alignment between local schedules and UTC predicates
- **Severity:** High · **Type:** Risk
- **Evidence:** `AdminTripManagementViewModel.cs:20` sets departure times in local wall-clock time (`DateTime.Today.AddDays(1).AddHours(7)`), while SQL predicates compare against `SYSUTCDATETIME()` (e.g. in `DatabaseHelper.cs:586` and `PaymentService.cs:64`).
- **Impact:** In UTC+7 (Vietnam), departures are evaluated as if they were 7 hours in the future, causing hold expiries and auto-mark-used routines to execute late relative to actual local departure times.
- **Proposed fix:** Store all departure and arrival times in UTC in the database; convert to local time at the UI presentation boundary. Execute a one-time data migration to shift existing local timestamps.
- **Behavior change:** Valid-path
- **Effort:** M · **Risk:** Medium
- **Dependencies:** None
- **Acceptance criteria:** Trips scheduled in local time are stored as UTC; departures passing in local time immediately trigger auto-use and hold expiration.
- **Verification method:** Schedule a trip 5 minutes in the future local time; observe that once the local departure passes, the trip is marked used.
- **Status:** TODO

---

### VER-A-002 · Data access layer abstraction and explicit actor parameterization
- **Severity:** Medium · **Type:** Technical limitation
- **Evidence:** `DatabaseHelper` and `AdminDatabaseService` remain static classes reading the ambient `CurrentUser.Account` session (`AdminDatabaseService.cs:10-14`).
- **Impact:** Business logic and data access cannot be unit-tested without mocking global static state, preventing reuse in multi-tenant or server environments.
- **Proposed fix:** Refactor data access methods into injectable repository interfaces (`ITicketRepository`, `ITripRepository`, `IAccountRepository`) and pass an explicit `UserSession actor` parameter to administrative operations.
- **Behavior change:** None
- **Effort:** L · **Risk:** Medium
- **Dependencies:** AUD-W3-03 (DI container)
- **Acceptance criteria:** All data access classes implement injectable interfaces; `CurrentUser` is not referenced in data layer code.
- **Verification method:** Unit test data operations with fake repositories without setting `CurrentUser.Account`.
- **Status:** TODO

---

### VER-F-001 · Comprehensive screen-reader accessibility and per-field validation
- **Severity:** Medium · **Type:** Gap
- **Evidence:** 16 of 22 XAML views have zero `AutomationProperties` elements; input forms lack `INotifyDataErrorInfo` adorners.
- **Impact:** Visually impaired users cannot navigate the booking funnel using assistive technology (e.g. Windows Narrator); validation errors are displayed as generic banners rather than highlighting invalid input fields.
- **Proposed fix:** Implement `INotifyDataErrorInfo` in `ViewModelBase`; add `Validation.ErrorTemplate` in `Theme.xaml`; add `AutomationProperties.Name` and `AutomationProperties.HelpText` to all interactive controls in `Views/*.xaml`.
- **Behavior change:** None
- **Effort:** L · **Risk:** Low
- **Dependencies:** None
- **Acceptance criteria:** Running Windows Accessibility Insights over the booking flow yields 0 missing accessible name violations.
- **Verification method:** Execute an accessibility audit using Accessibility Insights for Windows.
- **Status:** TODO

---

### VER-C-001 · Production online payment gateway activation (VNPay / MoMo)
- **Severity:** Medium · **Type:** Technical limitation
- **Evidence:** VNPay payment option was disabled in `BookingConfirmationViewModel.cs:80-84` and skipped per owner instruction ("Skip every thing rely to vnpay").
- **Impact:** Customers cannot pay online using credit cards or domestic banking gateways; only Cash on Board and manual Bank Transfer are available.
- **Proposed fix:** Obtain VNPay / MoMo merchant production credentials; re-enable the payment option in `BookingConfirmationViewModel`; deploy a secure public HTTPS webhook/IPN endpoint.
- **Behavior change:** Valid-path
- **Effort:** M · **Risk:** Low
- **Dependencies:** Merchant gateway contract
- **Acceptance criteria:** Online payment option is selectable and successfully redirects to the merchant portal; IPN confirms settlement automatically.
- **Verification method:** Complete a sandbox transaction and verify automatic settlement.
- **Status:** TODO
