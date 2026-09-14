# Data layer

- `DatabaseHelper.cs`: customer authentication, booking, profile, tickets and dashboard queries.
- `AdminDatabaseService.cs`: admin-only route, bus, seat and trip management.
- `Schema1.sql`: fresh SQL Server schema.
- `Migration_001.sql`: migration for ticket fields.
- `Migration_002_Admin.sql`: adds account roles.
- `SeedData.sql`: non-destructive development seed data.
- `Schema_Payments.sql`: idempotent payment ledger schema for sandbox VNPay/MoMo/bank-transfer flows.

Admin data operations require `CurrentUser.IsAdmin` and are also guarded by SQL validation for duplicate routes, overlapping trips and records referenced by tickets.
