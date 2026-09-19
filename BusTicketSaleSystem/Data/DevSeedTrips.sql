/* Schedule top-up for the development data set.
   Not a migration: DatabaseMigrator.EnsureUpcomingTripsAsync runs this on
   startup whenever the database has less than two days of future departures,
   so the demo schedule never runs dry. It is idempotent - every slot is
   guarded, no row is ever deleted or updated, and routes or buses that do not
   exist are simply skipped. */
USE BusTicketSaleSystem;
GO

DECLARE @Slots TABLE
(
    Origin NVARCHAR(100) NOT NULL,
    Destination NVARCHAR(100) NOT NULL,
    LicensePlate NVARCHAR(20) NOT NULL,
    DepartMinute INT NOT NULL,
    DurationMinute INT NOT NULL,
    Price DECIMAL(10,0) NOT NULL
);

INSERT INTO @Slots (Origin, Destination, LicensePlate, DepartMinute, DurationMinute, Price)
VALUES
    (N'Hà Nội',     N'Hải Phòng',    N'29B-123.45',  7 * 60 + 30, 120, 140000),
    (N'Hà Nội',     N'Nam Định',     N'29B-123.45', 10 * 60 + 30, 120, 110000),
    (N'Hà Nội',     N'Ninh Bình',    N'29B-123.45', 15 * 60 + 30, 120, 120000),
    (N'Hà Nội',     N'Thanh Hóa',    N'29B-678.90',  8 * 60,      210, 180000),
    (N'Hà Nội',     N'Hải Phòng',    N'29B-678.90', 14 * 60,      120, 150000),
    (N'Hà Nội',     N'Thái Nguyên',  N'29B-999.88',  9 * 60,      120, 100000),
    (N'Hải Phòng',  N'Hà Nội',       N'29B-999.88', 17 * 60,      120, 140000),
    (N'Nam Định',   N'Hà Nội',       N'29B-999.88',  6 * 60 + 30, 120, 110000);

DECLARE @Day INT = 0;
DECLARE @HorizonDays INT = 14;

WHILE @Day <= @HorizonDays
BEGIN
    DECLARE @TargetDate DATETIME2 = CAST(DATEADD(DAY, @Day, CAST(GETDATE() AS DATE)) AS DATETIME2);

    INSERT INTO Trips (RouteId, BusId, DepartureTime, ArrivalTime, Price)
    SELECT r.RouteId,
           b.BusId,
           DATEADD(MINUTE, sl.DepartMinute, @TargetDate),
           DATEADD(MINUTE, sl.DepartMinute + sl.DurationMinute, @TargetDate),
           sl.Price
    FROM @Slots sl
    JOIN Routes r ON r.Origin = sl.Origin AND r.Destination = sl.Destination
    JOIN Buses b ON b.LicensePlate = sl.LicensePlate
    /* Never create a departure in the past. */
    WHERE DATEADD(MINUTE, sl.DepartMinute, @TargetDate) > SYSUTCDATETIME()
      AND NOT EXISTS
          (
              SELECT 1
              FROM Trips existing
              WHERE existing.RouteId = r.RouteId
                AND CAST(existing.DepartureTime AS DATE) = CAST(@TargetDate AS DATE)
                AND DATEPART(HOUR, existing.DepartureTime) = sl.DepartMinute / 60
                AND DATEPART(MINUTE, existing.DepartureTime) = sl.DepartMinute % 60
          )
      AND NOT EXISTS
          (
              /* Never put the same bus on two overlapping trips. */
              SELECT 1
              FROM Trips busy
              WHERE busy.BusId = b.BusId
                AND busy.DepartureTime < DATEADD(MINUTE, sl.DepartMinute + sl.DurationMinute, @TargetDate)
                AND busy.ArrivalTime > DATEADD(MINUTE, sl.DepartMinute, @TargetDate)
          );

    SET @Day = @Day + 1;
END;
GO
