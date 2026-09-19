# 00 — Inventory

Audit date: 2026-09-20. Branch `upgrade/20260920`, base commit `9b5b3f2` (tag `pre-upgrade`).

## 1. Verified technology stack

The initial assumptions in the request were checked against the repository. Result:

| Assumption | Verdict | Evidence |
|---|---|---|
| Desktop application | Confirmed | `BusTicketSaleSystem/BusTicketSaleSystem.csproj:4` `<OutputType>WinExe</OutputType>` |
| Intercity bus ticketing / station management | Confirmed | `Data/Schema1.sql:21-69` (Routes/Buses/Trips/Seats/Tickets), `Services/PaymentService.cs`, `ViewModels/Admin/*` |
| WPF | Confirmed | `BusTicketSaleSystem.csproj:8` `<UseWPF>true</UseWPF>`, 26 `.xaml` files |
| .NET 10 | Confirmed | `BusTicketSaleSystem.csproj:5` `net10.0-windows`; SDK 10.0.401, runtime `Microsoft.WindowsDesktop.App 10.0.12` |
| MVVM | Confirmed, hand-rolled | `Common/RelayCommand.cs`, `Common/ViewModelBase.cs`, `Services/NavigationService.cs`; no MVVM framework package |
| SQL Server | Confirmed | `Microsoft.Data.SqlClient` 7.0.2; `appsettings.json:3` |
| **EF Core** | **Refuted** | No `Microsoft.EntityFrameworkCore*` package; data access is raw ADO.NET. Schema evolution uses a hand-rolled runner, `Data/DatabaseMigrator.cs:31-41`, over 11 `.sql` scripts. `dotnet ef` is therefore not applicable to this repository. |

Language of the product UI: Vietnamese (primary) with a partial English resource dictionary (`Resources/Strings.en-US.xaml`, `Resources/Strings.vi-VN.xaml`).

## 2. Solution and projects

`BusTicketSaleSystem.slnx` (XML solution format), two projects:

| Project | TFM | Nullable | ImplicitUsings | Output |
|---|---|---|---|---|
| `BusTicketSaleSystem` | `net10.0-windows` | enable | enable | WinExe (WPF) |
| `BusTicketSaleSystem.Tests` | `net10.0-windows` | enable | enable | xunit test library |

No `Directory.Build.props`, no `.editorconfig`, no central package management, no analyzer properties (`EnableNETAnalyzers`, `AnalysisLevel`, `TreatWarningsAsErrors` are all absent from both `.csproj` files).

## 3. Packages

`BusTicketSaleSystem`:

| Package | Version | Latest |
|---|---|---|
| Microsoft.Data.SqlClient | 7.0.2 | 7.1.0 |
| Microsoft.Extensions.Configuration.Json | 10.0.0 | 10.0.12 |
| QRCoder | 1.6.0 | 1.8.0 |
| QuestPDF | 2025.12.3 | 2026.9.0 |
| Serilog | 4.3.0 | 4.4.0 |
| Serilog.Sinks.File | 7.0.0 | — |

`BusTicketSaleSystem.Tests`: coverlet.collector 6.0.4, Microsoft.NET.Test.Sdk 17.14.1, xunit 2.9.3, xunit.runner.visualstudio 3.1.4, Xunit.SkippableFact 1.5.23.

Vulnerability scan: `dotnet list package --vulnerable --include-transitive` → **no vulnerable packages** in either project. Per R10 no package is upgraded merely because a newer version exists.

## 4. Configuration

`BusTicketSaleSystem/appsettings.json` (tracked) holds two Integrated-Security connection strings and logging settings. **No secret is present in any tracked file.**

Secrets and overrides come from environment variables (`Services/AppConfig.cs:13-53`):

| Variable | Required | Default | Purpose |
|---|---|---|---|
| `BUS_TICKET_CONNECTION_STRING` | no | `appsettings.json` → hardcoded localhost | Application database |
| `BUS_TICKET_MASTER_CONNECTION_STRING` | no | as above, `master` | Used by the migrator to `CREATE DATABASE` |
| `VNPAY_TMN_CODE` | for VNPay | empty | Merchant code |
| `VNPAY_HASH_SECRET` | for VNPay | empty | HMAC-SHA512 signing secret |
| `VNPAY_CALLBACK_PORT` | no | `5058` | Loopback listener port |
| `VNPAY_RETURN_URL` | no | loopback prefix | Gateway return URL |
| `BREVO_API_KEY` | for e-mail | empty | Brevo transactional e-mail |
| `EMAIL_SENDER_NAME` | no | `OmniBus Việt Nam` | Sender display name |
| `EMAIL_SENDER_ADDRESS` | for e-mail | empty | Sender address |
| `STATION_LOCATION` | no | IP geolocation | Kiosk station override (`Services/LocationService.cs:70-76`) |

## 5. Database

Declared databases for this engagement (the request template left these blank; taken from `appsettings.json` and the template's own naming convention, recorded in `AGENTS.md`):

```text
Dev server:    localhost      Dev database:  BusTicketSaleSystem
Test server:   localhost      Test database: BusTicketSaleSystem_Tests
```

Observed server state (`sqlcmd -S localhost -E -C`): SQL Server with databases `master, tempdb, model, msdb, BusTicketSaleSystem`. `BusTicketSaleSystem_Tests` did not exist and was created during this audit.

Dev database contents at audit time: Routes 17, Buses 7, Seats 250, Trips 619, Tickets 57, TicketSeats 63, Payments 52, Accounts 6. `__DatabaseMigrations` holds all ten steps `001_InitialSchema` … `010_BusCompanyAndRegistration`. `is_read_committed_snapshot_on = 0`.

Tables: `Accounts`, `Routes`, `Buses`, `Trips`, `Seats`, `Tickets`, `TicketSeats`, `Payments`, `BusCompanies`, `__DatabaseMigrations`. All monetary columns are `DECIMAL` — no `float`/`real`/`money` anywhere.

### Deviation to disclose

While provisioning the test database, `sqlcmd -d BusTicketSaleSystem_Tests -i Data/Schema1.sql` executed the `USE BusTicketSaleSystem;` statement embedded at `Data/Schema1.sql:4` and `Data/SeedData.sql:2`, redirecting that first pass into the **Dev** database. The application's migrator strips those statements (`Data/DatabaseMigrator.cs:227-228`), so only the raw `sqlcmd` invocation was affected. Every statement executed is `IF NOT EXISTS`-guarded and non-destructive; verification afterwards showed no duplicated routes, no deleted rows and unchanged migration history. No Dev data was lost or overwritten. Subsequent test-database provisioning applied the same normalisation the application performs.

## 6. Build baseline

```text
dotnet build BusTicketSaleSystem.slnx --nologo
Build succeeded. 0 Warning(s) 0 Error(s). Time Elapsed 00:00:09.40
```

That figure is produced with the SDK default analyzer set and non-fatal warnings. Re-running with analyzers forced on (`-p:AnalysisMode=All -p:EnableNETAnalyzers=true`, no source change) produces the following diagnostic distribution for `BusTicketSaleSystem` (counts are raw occurrences across the analyzer passes):

| Rule | Count | Meaning |
|---|---|---|
| CA2007 | 2228 | missing `ConfigureAwait` (largely not applicable to a WPF app) |
| CA1515 | 444 | type could be internal |
| CA1849 | 236 | sync call in async method |
| CA1031 | 236 | catch of general `Exception` |
| CA1305 | 144 | missing `IFormatProvider` |
| CA1822 | 124 | member can be static |
| CA1307 | 116 | missing `StringComparison` |
| CA1062 | 88 | unvalidated public argument |
| CA2100 | 28 | SQL command text review (7 distinct sites, all reviewed — see 01-audit-report §D) |
| CA2000 | 12 | undisposed object (3 distinct sites) |
| CA1001 | 8 | type owns disposable field but is not disposable (2 sites) |

## 7. Test baseline

```text
dotnet test BusTicketSaleSystem.slnx --no-build --nologo
Passed! - Failed: 0, Passed: 53, Skipped: 1, Total: 54, Duration: 2 s
```

The single skipped test is `ConcurrencyBookingTests.BookTicketAsync_AllowsOnlyOneConcurrentReservation` — the only test of the anti-double-sell invariant. It is a `[SkippableFact]` that skips unless `BUS_TICKET_TEST_TRIP_ID`, `BUS_TICKET_TEST_SEAT_ID` and `BUS_TICKET_TEST_ACCOUNT_IDS` are set; CI sets none of them (`.github/workflows/build-test.yml:28-29`).

Test files: `NavigationAndViewModelTests.cs` (768 ln), `DomainModelTests.cs`, `SecurityTests.cs`, `ConcurrencyBookingTests.cs`.

## 8. Formatting baseline

`dotnet format --verify-no-changes` rejects the `.slnx` solution file on SDK 10.0.401 (prints help, exit 0). Per-project invocation works:

```text
dotnet format BusTicketSaleSystem/BusTicketSaleSystem.csproj --verify-no-changes   → exit 2
```

9 whitespace violations reported, in `Data/DatabaseHelper.cs` (lines 368, 546 ×3, 857) and in the four `Views/Admin*View.xaml.cs` files (line 2). There is no `.editorconfig`, so this reflects SDK defaults only. Per R9 this does not block higher-priority work.

## 9. Repository statistics

Tracked files 126 · C# 79 · XAML 26 · SQL 11 · C# LOC 12,720 · combined C#+XAML+SQL LOC 21,068.

Ten largest files:

| Lines | File |
|---|---|
| 1231 | `BusTicketSaleSystem/Data/AdminDatabaseService.cs` |
| 1046 | `BusTicketSaleSystem/Data/DatabaseHelper.cs` |
| 995 | `BusTicketSaleSystem/ViewModels/Admin/AdminBusManagementViewModel.cs` |
| 891 | `BusTicketSaleSystem/Services/TicketPrintService.cs` |
| 768 | `BusTicketSaleSystem.Tests/NavigationAndViewModelTests.cs` |
| 730 | `BusTicketSaleSystem/Theme.xaml` |
| 680 | `BusTicketSaleSystem/Views/AdminDashboardView.xaml` |
| 659 | `BusTicketSaleSystem/Views/AdminBusManagementView.xaml` |
| 593 | `BusTicketSaleSystem/Services/PaymentService.cs` |
| 411 | `BusTicketSaleSystem/Views/MainWindow.xaml` |

Complexity hotspots (by responsibility, not size): `DatabaseHelper` (auth + hashing + search + booking transaction + cancellation), `AdminDatabaseService` (all admin CRUD + reports), `AdminBusManagementViewModel` (form state + validation + pricing + draft list + persistence), `PaymentService` (three payment channels + VNPay signing + expiry sweep + settlement state machine), `TicketPrintService` (WPF visual + QR + barcode + print + PNG + PDF).

Largest code-behind: `Views/AdminBusManagementView.xaml.cs`, `Views/SeatSelectionView.xaml.cs`, `Views/MainWindow.xaml.cs` — all thin (< 60 lines); logic is in ViewModels except for dialog ownership.

## 10. CI/CD

`.github/workflows/build-test.yml`: `windows-latest`, checkout → setup-dotnet 10.0.x → restore → build (Release) → test (trx logger). No format check, no analyzer enforcement, no coverage collection, no artifact upload, no publish/packaging, no versioning, no secret scanning, no vulnerability audit, no `workflow_dispatch`, no schedule.

## 11. Git

Branch `master` at `9b5b3f2`; audit branch `upgrade/20260920`; tag `pre-upgrade` at `9b5b3f2`.

History before this engagement: `0ab5328` initial commit, `a065a22` complete system + VietQR payment, `95c8bb6` booking/VietQR fixes. The working tree contained 105 uncommitted paths of relevant project work, snapshotted as `9b5b3f2 chore: snapshot before upgrade` using explicit pathspecs (no `git add .`).

Secret scan of full history (`git log --all -S` for `Password=`, `pwd=`, `VNPAY_HASH_SECRET`, `xkeysib-`): **no secret value found**. The `VNPAY_HASH_SECRET` matches are all `Environment.GetEnvironmentVariable` call sites, never a literal.
