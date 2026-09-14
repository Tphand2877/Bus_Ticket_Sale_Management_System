# Domain models

The project models customer and administration data through:

- `UserSession.cs`: authenticated account and role.
- `DomainModels.cs`: trips, seats and tickets used by customer booking.
- `AdminModels.cs`: routes, buses, trips and dashboard statistics used by the admin shell.

Roles are `Customer` and `Admin`. Admin accounts are routed to `AdminWindow`; customer accounts are routed to `MainWindow`.
