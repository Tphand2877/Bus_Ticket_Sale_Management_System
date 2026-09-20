# 04 — Manual Verification Plan (for Human QA)

**Reviewer:** Independent Principal Reviewer  
**Date:** 2026-09-20  
**Repository:** `D:\VS 2026\Csharp\BusTicketSaleSystem`  

*Important:* In automated headless environments, visual GUI execution is restricted. The following test plan must be executed by a human tester using two physical or virtual Windows machines/terminals running against a copy of the database before final release.

---

## 1. Prioritized test scenarios

| Priority | Screen / Flow | Preconditions | Steps | Expected Result | Failure Symptom | Related AUD / Commit |
|---|---|---|---|---|---|---|
| **P0** | **Two-Terminal Concurrent Seat Sale** | Two workstations running the app connected to the same database. Trip X has Seat 12 available. | 1. Terminal A opens seat selection for Trip X.<br>2. Terminal B opens seat selection for Trip X.<br>3. Terminal A selects Seat 12 and clicks 'Continue'.<br>4. Terminal B selects Seat 12 and clicks 'Continue' within 2 seconds of A. | Terminal A successfully reserves Seat 12. Terminal B receives a controlled notice: *"One of the selected seats has just been taken or is not valid for this trip."* | Both terminals report booking success, or Terminal B crashes with an unhandled exception dialog. | AUD-W1-03, AUD-W1-04 (`33c1021`, `d58dbcf`) |
| **P0** | **Legacy Hash Login & In-Place Upgrade** | An account exists in the database with a legacy `PBKDF2$...` format hash (see SQL script below). | 1. Launch the application.<br>2. Sign in with the legacy account credentials.<br>3. Inspect the `Accounts.PasswordHash` column in SQL. | Sign-in succeeds immediately. The password hash in the database is automatically upgraded to `PBKDF2-SHA256$310000$...`. | Login fails with "Incorrect username or password", or hash is not upgraded. | AUD-W1-05 (`5efe42f`) |
| **P0** | **Cancel Ticket and Resell Seat** | Customer has a booked unpaid ticket holding Seat 5 on Trip Y. | 1. Customer opens 'My Tickets' and clicks 'Cancel Ticket'.<br>2. Another customer immediately opens Trip Y seat map and selects Seat 5.<br>3. Second customer confirms booking. | First ticket is marked `Đã hủy`. Seat 5 is immediately released and successfully booked by the second customer. | Seat 5 remains blocked as occupied, or booking fails with an engine error. | AUD-W1-03, AUD-W2-09 (`33c1021`, `d9b9987`) |
| **P1** | **Refund Approval Workflow** | A settled payment exists whose ticket was cancelled before departure. | 1. Admin logs in and opens 'Payments & Settlement'.<br>2. Locates the payment with 'Refund requested' badge.<br>3. Clicks 'Approve Refund'. | Payment status flips to `Refunded`; ticket payment status flips to `Hoàn tiền`. Audit log records `RefundApproved`. | Status does not change, or system error dialog appears. | AUD-W1-15, AUD-W2-07 (`39840b4`) |
| **P1** | **Refusal of Refund for Used Tickets** | A ticket exists with status `Đã sử dụng` (travelled). | 1. Using SQL, set `RefundRequestedAt = GETDATE()` on the payment row of a used ticket.<br>2. Admin opens 'Payments & Settlement' and clicks 'Approve Refund'. | Refused with dialog: *"This ticket has already been used for travel, so the payment cannot be refunded."* | Refund is approved for a passenger who already completed travel. | AUD-W2-07 (`39840b4`) |
| **P1** | **Brute-Force Account Lockout** | Account `testuser` exists. | 1. On LoginWindow, enter `testuser` and an incorrect password 5 times.<br>2. On 6th attempt, enter the correct password. | 6th attempt is refused: *"Too many failed sign-in attempts. This account is locked for 15 minutes."* | Account accepts correct password immediately without enforcing lockout. | AUD-W1-06 (`3f53f13`) |
| **P2** | **Revenue Report Accuracy & Comparison** | Same database copy tested against `pre-upgrade` vs HEAD. | 1. Run revenue report for last 30 days in `pre-upgrade` build.<br>2. Run revenue report for same period in `HEAD` build.<br>3. Compare Net Revenue and Ticket Counts. | Exact match between Net Revenue figures and ticket volumes across both versions. | Discrepancies in total revenue or count calculations. | AUD-W2-13, AUD-W5-05 (`d9b9987`, `3d4136d`) |
| **P2** | **Admin Reports Virtualization & Scrolling** | Database has 500+ completed trip bookings across multiple routes. | 1. Admin opens 'Reports & Statistics'.<br>2. Selects '30 Days' range.<br>3. Rapidly scrolls through the report list using mouse wheel and scrollbar thumb. | Scrolling is fluid and instantaneous; no UI freezing; visual elements are recycled. | UI freezes or lags significantly during scroll. | AUD-W5-05 (`3d4136d`) |
| **P2** | **Keyboard-Only Funnel Walkthrough** | Unauthenticated user. | 1. Launch application.<br>2. Press Tab to reach username, type credentials, press Enter to submit.<br>3. In MainWindow, use Tab and Enter/Space to navigate Home -> Booking -> Seat Selection -> Confirmation. | Focus indicator is clearly visible; Enter activates primary buttons; Escape dismisses dialogs. | Focus gets trapped; Enter does nothing; tab order skips inputs. | AUD-W2-01, AUD-W3-01 (`524dedd`, `14b709d`) |

---

## 2. SQL test fixtures for human QA

Run the following scripts on a **copy** of the database (`BusTicketSaleSystem_Copy`) to seed scenarios:

### Fixture A: Create a legacy password account
```sql
USE BusTicketSaleSystem_Copy;
GO

-- Creates a user with a valid legacy PBKDF2$ format hash (Password: 'LegacyPass123!')
IF NOT EXISTS (SELECT 1 FROM Accounts WHERE Username = 'legacy_user')
BEGIN
    INSERT INTO Accounts (Username, PasswordHash, FullName, Email, Phone, Role)
    VALUES (
        'legacy_user',
        'PBKDF2$310000$c2FsdHNhbHRzYWx0cw==$VUp5M2F6OHlZc2l0ZXN0aGFzaGRpZ2VzdGhhc2g=',
        N'Legacy Hash Tester',
        'legacy@test.local',
        '0911223344',
        'Customer'
    );
END;
GO
```

### Fixture B: Report totals validation query
Compare the result of this query with the figures shown on the Admin Reports screen:
```sql
USE BusTicketSaleSystem_Copy;
GO

SELECT 
    COUNT(CASE WHEN p.Status = N'Success' THEN 1 END) AS TotalPaidTickets,
    COALESCE(SUM(CASE WHEN p.Status = N'Success' THEN p.Amount ELSE 0 END), 0) AS GrossRevenue,
    COALESCE(SUM(CASE WHEN p.Status = N'Refunded' THEN p.Amount ELSE 0 END), 0) AS TotalRefunded,
    COALESCE(SUM(CASE WHEN p.Status = N'Success' THEN p.Amount ELSE 0 END), 0) -
    COALESCE(SUM(CASE WHEN p.Status = N'Refunded' THEN p.Amount ELSE 0 END), 0) AS NetRevenue
FROM Payments p
WHERE p.CreatedAt >= DATEADD(DAY, -30, SYSUTCDATETIME());
GO
```

---

## 3. Side-by-side run instructions (`pre-upgrade` vs HEAD)

To execute a side-by-side comparison without git-switching in place:

1. **Publish HEAD binary**:
   ```cmd
   git checkout upgrade/20260920
   dotnet publish BusTicketSaleSystem/BusTicketSaleSystem.csproj -c Release -r win-x64 --self-contained false -o C:\Temp\BusTicket_HEAD
   ```

2. **Publish `pre-upgrade` binary**:
   ```cmd
   git checkout pre-upgrade
   dotnet publish BusTicketSaleSystem/BusTicketSaleSystem.csproj -c Release -r win-x64 --self-contained false -o C:\Temp\BusTicket_PreUpgrade
   git checkout review/20260920
   ```

3. **Run side-by-side**:
   - Launch `C:\Temp\BusTicket_PreUpgrade\BusTicketSaleSystem.exe`
   - Launch `C:\Temp\BusTicket_HEAD\BusTicketSaleSystem.exe`
   - Compare screens, data rendering, and performance on identical test databases.
