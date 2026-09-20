# BusTicketSaleSystem

Desktop application for an intercity bus station in Vietnam: customers search trips,
pick seats and pay; station staff register buses and routes, schedule departures,
settle payments and approve refunds.

WPF on .NET 10, hand-rolled MVVM, raw ADO.NET against SQL Server. There is no ORM —
schema evolution is a forward-only migration runner over the scripts in
`BusTicketSaleSystem/Data/*.sql`.

## Prerequisites

| Requirement | Notes |
|---|---|
| Windows | WPF; the Windows Desktop runtime is required |
| .NET SDK 10.0 | `dotnet --info` must list a `10.0.*` SDK and `Microsoft.WindowsDesktop.App` |
| SQL Server | Any edition reachable from the workstation, including LocalDB and Express. The application creates its own database on first run |

## Quick start

```bash
git clone <repository-url>
cd BusTicketSaleSystem

dotnet restore BusTicketSaleSystem.slnx
dotnet build   BusTicketSaleSystem.slnx
dotnet run --project BusTicketSaleSystem/BusTicketSaleSystem.csproj
```

On first launch the application connects to the server in `BusTicketSaleSystem/appsettings.json`,
creates the `BusTicketSaleSystem` database if it does not exist, applies every migration,
seeds development routes/buses/seats, and tops the departure schedule up to two weeks ahead.
If that fails you get a dialog naming the reason, and the detail is in `Logs/app-*.log`
beside the executable.

### First administrator

The seed data contains no accounts. Register through the application, then promote
yourself once:

```sql
UPDATE Accounts SET Role = N'Admin' WHERE Username = N'<your-username>';
```

Admin screens are then reachable by signing in with that account.

## Configuration

`BusTicketSaleSystem/appsettings.json` holds the connection strings and logging
settings. Everything secret is an environment variable and is never read from a
tracked file.

| Variable | Required | Default | Purpose | If unset |
|---|---|---|---|---|
| `BUS_TICKET_CONNECTION_STRING` | no | `appsettings.json` → `Server=localhost;Database=BusTicketSaleSystem;…` | Application database | Falls back to the file, then to the built-in local default (a warning is logged) |
| `BUS_TICKET_MASTER_CONNECTION_STRING` | no | same server, `master` | Used only to `CREATE DATABASE` on first run | First-run database creation fails |
| `STATION_LOCATION` | recommended for staff workstations | IP geolocation hint | Pins which station this workstation may administer | The station is guessed and the guess is treated as non-authoritative |
| `VNPAY_TMN_CODE` | for VNPay | empty | Merchant code | VNPay is unavailable (it is disabled in the product — see below) |
| `VNPAY_HASH_SECRET` | for VNPay | empty | HMAC-SHA512 signing secret | as above |
| `VNPAY_CALLBACK_PORT` | no | `5058` | Loopback port for the gateway return | Default port must be free |
| `VNPAY_RETURN_URL` | no | the loopback prefix | Gateway return URL | — |
| `BREVO_API_KEY` | for e-mail | empty | Brevo transactional e-mail | "E-mail this ticket" reports that delivery is not configured |
| `EMAIL_SENDER_NAME` | no | `OmniBus Việt Nam` | Sender display name | — |
| `EMAIL_SENDER_ADDRESS` | for e-mail | empty | Sender address | as `BREVO_API_KEY` |

Local overrides belong in `appsettings.Development.json` or `secrets.json`, both of
which are git-ignored. **VNPay is currently disabled in the product** because no
merchant subscription exists; cash on board and bank transfer are the offered methods.

## Tests

```bash
dotnet test BusTicketSaleSystem.slnx
```

The suite includes integration tests that create, migrate and drop a dedicated
database, `BusTicketSaleSystem_Tests`, on the same server. They never touch the
development database — the fixture refuses to run against any other catalog. Point
them elsewhere with `BUS_TICKET_TEST_SERVER` (CI uses `(localdb)\MSSQLLocalDB`).
When SQL Server is unreachable those tests skip with the reason rather than failing.

## Migrations

Forward-only by design; there are no `Down()` scripts. To add one:

1. Create `BusTicketSaleSystem/Data/Migration_0NN_<Name>.sql`.
2. Make every statement idempotent (`IF NOT EXISTS`, `IF COL_LENGTH(...) IS NULL`, …)
   and separate batches with a bare `GO` on its own line.
3. Document the reversal procedure in a header comment — that is what "rollback" means
   here, alongside a restore point.
4. Register it in `DatabaseMigrator.MigrateAsync` with the next `0NN_Name` history key.

Applied scripts are immutable: each is recorded with a SHA-256 checksum and the
application refuses to start if one has been edited since it ran. Add a new migration
instead.

## Troubleshooting

| Symptom | Where to look | Remedy |
|---|---|---|
| "The database could not be created or updated" at startup | `Logs/app-*.log` | The server is unreachable or the master connection lacks `CREATE DATABASE` rights |
| "Migration … has changed since it was applied" | startup dialog | An applied `.sql` file was edited; restore it and add a new migration instead |
| Sign-in reports the account is locked | `Logs/app-*.log` (`Sign-in failed for account …`) | Five consecutive failures lock an account for fifteen minutes |
| "E-mail delivery is not configured" | — | Set `BREVO_API_KEY` and `EMAIL_SENDER_ADDRESS` |
| The admin console refuses a route or trip | on-screen message names the station | The workstation's `STATION_LOCATION` does not serve that route |

## Further reading

- `Markdown/PROJECT_DOCUMENTATION.md` — feature and architecture documentation (Vietnamese)
- `BusTicketSaleSystem/PROJECT_LOGIC.md` — screen and navigation flows (Vietnamese)
- `docs/audit/` — the audit, upgrade backlog, plan and log
- `docs/architecture/adr/` — architecture decision records
- `AGENTS.md` — engineering rules binding on automated contributors
