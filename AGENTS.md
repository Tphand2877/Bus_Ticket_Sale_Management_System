# AGENTS.md — BusTicketSaleSystem

## Upgrade guardrails

These rules are binding for any AI agent or automated contributor working in this repository.

### R1 — Evidence first

Every finding must have evidence.

Acceptable evidence includes:

- file path + line number
- compiler output
- test output
- analyzer output
- package information
- migration/schema evidence
- SQL output
- Git evidence
- reproducible application behavior
- reproducible test behavior
- measurable performance evidence

If evidence is insufficient:

```text
HYPOTHESIS — <reason>
```

Never fabricate evidence. Never turn a personal architectural preference into a defect.

For every Blocker/Critical/High finding, provide a reproducible verification method where practical: command, test, SQL query, static-analysis result, or a concrete application operation sequence.

### R2 — Business behavior is frozen

Business behavior includes, but is not limited to: ticket pricing, seat allocation, booking rules, route rules, refund rules, cancellation rules, payment rules, authorization policy, workflow states, workflow transitions, customer classification, revenue calculations, commission calculations, operator/station rules, persisted data semantics, public APIs, user-visible workflows.

Every backlog item MUST declare one:

```text
Behavior change:
- None
- Invalid-path only
- Valid-path
```

- **None** — no observable business behavior changes (refactoring, performance, code organization, logging, DI, behavior-preserving memory-leak fixes).
- **Invalid-path only** — only previously invalid, unsafe, impossible, or corrupted behavior is rejected or made safe; valid workflows unchanged (e.g. preventing two users from purchasing the same seat). Valid only when the intended rule is already evidenced by existing code, DB constraints/schema, tests, documentation, existing workflow, or another authoritative repository source. Do NOT invent a new business rule to obtain this classification.
- **Valid-path** — changes how a previously valid workflow behaves (pricing, refund policy, valid seat-selection rules, commission logic, customer classification, a valid authorization policy).

`Valid-path` items MUST NOT be implemented automatically. They are marked `AWAITING-APPROVAL` and must document old behavior, proposed behavior, reason, affected workflows, risks, and tests required. Only the repository owner may approve them.

### R3 — Preserve unrelated work

Before every commit run `git status` and `git diff`. Stage explicit paths only. Never `git add .` or `git add -A`. Do not include unrelated user work, do not overwrite unrelated changes, and do not delete user files merely because they look unused.

Deleting an apparently unused source/config/test file requires: (1) reference/search evidence, (2) an explicit AUD item, (3) verification that removal is safe.

If the working tree contains a probable secret or unrelated work: do not stage it, do not overwrite it, do not delete it; ask for approval only if proceeding actually requires modifying or committing it.

Never rewrite Git history. If a secret is found in history: do not rewrite history automatically, do not reproduce the secret, record its location, and recommend rotation/remediation.

### R4 — Incremental and simple

Do NOT rewrite the project from scratch, replace the framework, change the database engine, replace WPF without evidence, or introduce microservices, CQRS, event sourcing, Kubernetes, distributed messaging, or speculative abstractions. Such changes are allowed only when the audit demonstrates a concrete requirement.

For every non-trivial architectural change document: why it is needed, what problem it solves, alternatives considered, added cost, added complexity, operational impact, why it fits the current scale, and how it can be reversed or extended later.

Prefer the smallest change that satisfies the acceptance criteria.

### R5 — Database integrity is authoritative

Critical business invariants must be enforced by the database, not only by the UI. The seat-sale system MUST prevent double-selling the same seat under concurrent requests.

Acceptable mechanisms: unique constraints/indexes, transactions, appropriate isolation, `rowversion`, optimistic concurrency, carefully justified pessimistic locking.

Not sufficient by themselves: UI validation, "check then insert", non-atomic application logic.

The database must remain correct even when the UI is bypassed.

### R6 — Migration safety

For every migration: inspect `Up()`, inspect `Down()`, verify reversibility, inspect generated SQL where practical, verify there are no unexpected destructive operations, verify indexes/foreign keys/unique constraints/nullability changes, verify existing data remains valid, verify rollback considerations, and apply only to the declared Dev/Test databases.

Never run on the Dev database: `EnsureDeleted`, database drop/reset, truncation, destructive seed logic, or destructive data migrations without explicit approval.

Password migrations must be safe: use rehash-at-next-login where appropriate, preserve user access, and never store plaintext passwords.

### R7 — Secrets

Never copy secrets into source code, reports, documentation, commits, logs, or test output. Mask them as `***`. If a secret is discovered, report its presence and location without reproducing the value. Use environment variables, .NET User Secrets, secure configuration, or deployment secret stores.

### R8 — Never fake green

Never delete tests to make them pass, skip failing tests, weaken assertions, narrow test cases to hide bugs, disable analyzers just to reduce warnings, or suppress errors without documenting the reason. A green build must represent actual correctness.

### R9 — Formatting

Baseline: `dotnet format --verify-no-changes`.

Do not mass-format the repository during unrelated work. If formatting fixes are necessary, create a dedicated backlog item, restrict the operation to relevant files, and commit it separately. Formatting failures must not block security, data-integrity, correctness, or stability work unless formatting prevents reliable compilation or analysis.

### R10 — Package management

Never upgrade a package merely because a newer version exists. For every package added/upgraded record: package name, previous version, new version, reason, security/bug/compatibility/architecture justification, breaking-change risk, and verification result. Avoid broad package upgrades.

## Declared databases

```text
Dev server:    localhost
Dev database:  BusTicketSaleSystem
Test server:   localhost
Test database: BusTicketSaleSystem_Tests
```

The Dev database may be migrated but never dropped, reset, or emptied. Only the Test database may be created, migrated, reset, or dropped. Never run database commands against an undeclared database.

## Environment configuration

Secrets are read from environment variables (see `BusTicketSaleSystem/Services/AppConfig.cs`), never from committed files:

`BUS_TICKET_CONNECTION_STRING`, `BUS_TICKET_MASTER_CONNECTION_STRING`, `VNPAY_TMN_CODE`, `VNPAY_HASH_SECRET`, `VNPAY_CALLBACK_PORT`, `VNPAY_RETURN_URL`, `BREVO_API_KEY`, `EMAIL_SENDER_NAME`, `EMAIL_SENDER_ADDRESS`.
