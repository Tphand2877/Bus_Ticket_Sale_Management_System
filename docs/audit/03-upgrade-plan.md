# 03 — Upgrade plan

Execution order for `02-upgrade-backlog.md`. Dependencies override numeric order. Every item follows: re-verify evidence → set `IN-PROGRESS` → characterization test where behaviour is at risk → minimum change → `dotnet build` + `dotnet test` → update backlog and `04-upgrade-log.md` → Conventional Commit referencing the AUD ID.

Global stop conditions: three consecutive `BLOCKED` items, or every Blocker/Critical/High item resolved, blocked with evidence, or awaiting approval.

Database rules for every wave: only `BusTicketSaleSystem_Tests` may be created, reset or dropped. `BusTicketSaleSystem` (Dev) may be migrated but never emptied. Migrations are validated for `Up`, reversibility considerations, generated SQL, indexes, foreign keys, unique constraints, nullability and data preservation; this project's runner is forward-only by design, so "rollback" means a documented manual procedure plus a restore point, not a `Down()` script (see AUD-W6-02).

---

## Wave 1 — Test foundation, security, data integrity

**Items, in order:** AUD-W1-01 → W1-02 → W1-03 → W1-04 → W1-05 → W1-06 → W1-07 → W1-08 → W1-09 → W1-10 → W1-11 → W1-12 → W1-13 → W1-14 → W1-15 → W1-16 → W1-17 → W1-18 → W1-19. AUD-W1-20 is `AWAITING-APPROVAL` and is not executed.

**Dependencies:** W1-02 needs W1-01. W1-03 needs W1-02 (the test must fail before the fix and pass after). W1-04 follows W1-03 because the trigger may be replaced by the constraint. W1-13 needs W1-14's hardening so the scope value is not attacker-controlled before it becomes authoritative. Everything else is independent.

**Expected outcome:** the anti-double-sell invariant is enforced by the database under any isolation level and proven by an executing test; plaintext passwords no longer authenticate; online guessing is rate-limited and recorded; the VNPay paths no longer mis-settle, strand money, or keep seats held after a decline; the duplicate-payment, bus-delete and trip-overlap races are closed; privileged operations are audited; the loopback endpoint cannot forge log entries or wedge settlement.

**Stop conditions:** SQL Server on `localhost` unreachable (blocks W1-01 and everything depending on it — record and continue with W1-05, W1-07, W1-09, W1-11, W1-16, W1-18, W1-19, which need no database); existing Dev data violating a new uniqueness object (record, do not force; the constraint lands on the Test database and the Dev migration is deferred with evidence); any Valid-path requirement surfacing mid-item → reclassify and move to `AWAITING-APPROVAL`.

**Verification commands:**
```bash
dotnet build BusTicketSaleSystem.slnx --nologo
dotnet test  BusTicketSaleSystem.slnx --nologo
sqlcmd -S localhost -E -C -I -d BusTicketSaleSystem_Tests -Q "SELECT name, type_desc FROM sys.indexes WHERE object_id IN (OBJECT_ID('dbo.TicketSeats'), OBJECT_ID('dbo.Tickets'));"
```
Concurrency proof (must hold with `READ_COMMITTED_SNAPSHOT` both off and on):
```bash
sqlcmd -S localhost -E -C -I -Q "ALTER DATABASE BusTicketSaleSystem_Tests SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;"
dotnet test BusTicketSaleSystem.slnx --nologo --filter "FullyQualifiedName~Concurrency"
sqlcmd -S localhost -E -C -I -Q "ALTER DATABASE BusTicketSaleSystem_Tests SET READ_COMMITTED_SNAPSHOT OFF WITH ROLLBACK IMMEDIATE;"
```

**Manual test requirements:** none — Wave 1 is provable without the GUI, except AUD-W1-09 and AUD-W1-16, whose user-visible messages are recorded as checklist rows (payment decline message matches the released seat; a crafted loopback request produces one sanitised log line).

---

## Wave 2 — Stability and performance

**Items, in order:** AUD-W2-01 → W2-02 → W2-03 → W2-04 → W2-05 → W2-06 → W2-07 → W2-08 → W2-09 → W2-10 → W2-11 → W2-12 → W2-13 → W2-14 → W2-15 → W2-16.

**Dependencies:** W2-02 after W2-01 (both touch command/ViewModel lifetime). W2-03 after W2-01 so routine command faults stop reaching the dispatcher handler before its behaviour is narrowed. W2-10 and W2-13 need the Wave 1 fixture for their concurrency and plan evidence. W2-13 needs W2-11's indexes to show the intended plans. W2-14 after W2-02 (disposal must exist before cancellation is tied to it).

**Expected outcome:** no async-void command handlers; ViewModels and their timers/subscriptions die with their navigation; unrecoverable exceptions stop pretending to be handled; the expiry sweep is atomic; the seat-availability path is index-backed; deadlocks retry instead of surfacing as generic failures; the migration history describes the schema truthfully.

**Stop conditions:** a leak-verification measurement that cannot be obtained without the GUI (record as a manual checklist row rather than blocking); any index creation that would require touching Dev data (defer with evidence).

**Verification commands:**
```bash
dotnet build BusTicketSaleSystem.slnx --nologo
dotnet test  BusTicketSaleSystem.slnx --nologo
sqlcmd -S localhost -E -C -I -d BusTicketSaleSystem_Tests -Q "SET STATISTICS IO ON; <SearchTripsAsync statement>;"
```

**Manual test requirements:** double-click each primary command (bus registration, sign-in, booking) → exactly one effect; navigate Home↔Booking 20× → one live instance each; open seat selection, navigate away, observe no further seat queries for 60 s; stop SQL Server and open home/booking/seat screens → degraded content, no system-error dialog.

---

## Wave 3 — Architecture, MVVM, DI

Only areas already covered by Wave 1/Wave 2 integration tests or by a characterization test written immediately before the change may be refactored.

**Items, in order:** AUD-W3-03 (composition root first — everything else depends on it) → W3-01 → W3-02 → W3-08 → W3-05 → W3-04 → W3-07 → W3-10 → W3-06 → W3-09.

**Dependencies:** W3-01/W3-04/W3-05/W3-11 all need the composition root. W3-02 must precede W3-07 and W3-09 (the actor parameter shapes both signatures). W3-09 is the largest item and runs last, guarded by the full Wave 1/2/4 test set.

**Expected outcome:** one composition root; no ViewModel constructs a Window or an OS dialog; no `CurrentUser` reference under `Data/` or `Services/`; `Services/` free of WPF dispatcher dependencies; booking orchestration testable without a ViewModel; no admin ViewModel over ~350 lines; booking and my-tickets flows unit-testable against fakes. At that point the business core is genuinely reusable by an API/web/mobile host — the J-axis blocker.

**Stop conditions:** W3-09 is `XL`; if it cannot be completed without behaviour drift it is split or marked `BLOCKED` with evidence rather than half-applied. Any characterization test that cannot be written for a flow about to change → the refactor for that flow does not proceed.

**Verification commands:**
```bash
dotnet build BusTicketSaleSystem.slnx --nologo
dotnet test  BusTicketSaleSystem.slnx --nologo
```
plus, per item, the greps named in its acceptance criteria (`CurrentUser.` under `Data/`+`Services/`, `Service.Instance` under `ViewModels/`, `new SomeViewModel(` in code-behind, `System.Windows` in `Services/`).

**Manual test requirements:** navigate every customer and admin tab after the composition root lands; complete one booking through the payment dialog; export a PDF and a PNG; print preview; log out from both shells.

**ADRs required:** `docs/architecture/adr/0001-dependency-injection-composition-root.md` (W3-03, introduces a package), `0002-explicit-actor-instead-of-ambient-session.md` (W3-02), `0003-headless-ticket-rendering.md` (W3-04), `0004-data-access-seams.md` (W3-09). Each records context, problem, decision, alternatives, consequences, evidence and reversal considerations.

---

## Wave 4 — Testing and CI/CD

**Items, in order:** AUD-W4-01 → W4-03 → W4-04 → W4-02 → W4-05.

**Dependencies:** W4-01 precedes W4-05's format and warning gates. W4-02 needs the Wave 1 fixture. W4-03 precedes W4-05 so CI is not made stricter while the suite still carries ordering dependencies.

**Expected outcome:** analyzers on with a documented, justified residual diagnostic set; deterministic, isolated, categorised tests; the revenue-bearing lifecycle transitions covered; CI that runs the concurrency test for real, publishes coverage and a versioned artifact, verifies formatting, and fails on new warnings or vulnerable packages.

**Stop conditions:** enabling analyzers surfaces a diagnostic volume that cannot be triaged inside the wave — then the analyzer set is narrowed by documented per-rule severity in `.editorconfig` (never blanket `NoWarn`, never mass `#pragma`, per R8), and the remainder is recorded as a follow-up item.

**Verification commands:**
```bash
dotnet build BusTicketSaleSystem.slnx --nologo
dotnet test  BusTicketSaleSystem.slnx --nologo --collect:"XPlat Code Coverage"
dotnet format BusTicketSaleSystem/BusTicketSaleSystem.csproj --verify-no-changes
dotnet format BusTicketSaleSystem.Tests/BusTicketSaleSystem.Tests.csproj --verify-no-changes
dotnet list BusTicketSaleSystem.slnx package --vulnerable --include-transitive
```
(`dotnet format` on SDK 10.0.401 does not accept the `.slnx` solution; per-project invocation is the supported form and is what CI must use.)

**Manual test requirements:** none.

---

## Wave 5 — UI/UX and accessibility

**Items, in order:** AUD-W5-02 (localization first — accessible names reuse its keys) → W5-01 → W5-04 → W5-03 → W5-06 → W5-07 → W5-05 (reports-list virtualization only) → W5-08.

**Dependencies:** W5-01 and W5-06 depend on W5-02's resource keys. W5-03 depends on the shared ViewModel base from W3-10. The shell scroll-host change inside W5-05 is `AWAITING-APPROVAL` (AUD-WX-08) and is not executed.

**Expected outcome:** the booking funnel is completable with a screen reader and with the keyboard alone; language toggling changes every visible string; invalid fields are identified and focused; destructive confirmations trap focus and announce; the reports list is virtualized; no raw hex in views; the app is usable at 1280×720 @150%.

**Stop conditions:** the GUI must not be launched in this environment, so every Wave 5 item ends in a manual checklist row rather than a verification claim. An item whose only possible verification is visual is completed in code, marked with its checklist row, and explicitly reported as not visually verified.

**Verification commands:**
```bash
dotnet build BusTicketSaleSystem.slnx --nologo
dotnet test  BusTicketSaleSystem.slnx --nologo
```
plus the measured greps from the findings (`AutomationProperties` count, `TabIndex`/`FocusManager` presence, raw-hex count in `Views/*.xaml`, dictionary key parity).

**Manual test requirements:** full screen-by-screen checklist in `04-upgrade-log.md` using the columns Screen | Preconditions | Action | Expected result | Failure symptom, covering: Narrator walkthrough of Login → Home → Booking → SeatSelection → BookingConfirmation → Payment → Ticket; keyboard-only completion of the same funnel; language toggle on all 22 views; invalid-form submission on SeatSelection and admin bus registration; delete confirmation focus/Escape/announcement on admin accounts; 1280×720 @150% walkthrough; 12-month revenue report scroll.

---

## Wave 6 — Documentation and maintainability

**Items, in order:** AUD-W6-01 → W6-02 → W6-03 → W6-04 → W6-05 → W6-06.

**Dependencies:** W6-04 and W6-06 need the analyzers and `.editorconfig` from W4-01. W6-06 is committed separately per R9 and touches only the files named in its scope.

**Expected outcome:** a newcomer can build, configure, migrate, run and test from the README alone; the documented schema matches the migrated schema; the docs no longer contradict the repository; status literals and the payment-hold window have one definition each; logs carry no raw PII; the formatting baseline is clean.

**Stop conditions:** none — documentation work cannot block on the environment.

**Verification commands:**
```bash
dotnet build BusTicketSaleSystem.slnx --nologo
dotnet test  BusTicketSaleSystem.slnx --nologo
dotnet format BusTicketSaleSystem/BusTicketSaleSystem.csproj --verify-no-changes
```

**Manual test requirements:** follow the README verbatim against a scratch database name and confirm the application reaches the login window (recorded as a checklist row; the GUI is not launched in this session).

---

## Wave completion protocol

At the end of every wave: run build and tests; run format verification where applicable; update `04-upgrade-log.md` with completed, incomplete, blocked, skipped and awaiting-approval items, package changes, migration changes, architecture changes, risks, test results and the manual checklist; create the tag `wave-N`. Never push, never merge.
