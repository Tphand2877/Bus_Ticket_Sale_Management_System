/* Development seed only. This script never deletes application data. */
USE BusTicketSaleSystem;
GO

/* 1. Insert Routes */
IF NOT EXISTS (SELECT 1 FROM Routes WHERE Origin=N'Hà Nội' AND Destination=N'Hải Phòng')
    INSERT INTO Routes (Origin, Destination, DistanceKm) VALUES (N'Hà Nội', N'Hải Phòng', 120);
IF NOT EXISTS (SELECT 1 FROM Routes WHERE Origin=N'Hà Nội' AND Destination=N'Nam Định')
    INSERT INTO Routes (Origin, Destination, DistanceKm) VALUES (N'Hà Nội', N'Nam Định', 90);
IF NOT EXISTS (SELECT 1 FROM Routes WHERE Origin=N'Hà Nội' AND Destination=N'Thái Nguyên')
    INSERT INTO Routes (Origin, Destination, DistanceKm) VALUES (N'Hà Nội', N'Thái Nguyên', 80);
IF NOT EXISTS (SELECT 1 FROM Routes WHERE Origin=N'Hà Nội' AND Destination=N'Thanh Hóa')
    INSERT INTO Routes (Origin, Destination, DistanceKm) VALUES (N'Hà Nội', N'Thanh Hóa', 150);
IF NOT EXISTS (SELECT 1 FROM Routes WHERE Origin=N'Hà Nội' AND Destination=N'Ninh Bình')
    INSERT INTO Routes (Origin, Destination, DistanceKm) VALUES (N'Hà Nội', N'Ninh Bình', 100);
IF NOT EXISTS (SELECT 1 FROM Routes WHERE Origin=N'Hải Phòng' AND Destination=N'Hà Nội')
    INSERT INTO Routes (Origin, Destination, DistanceKm) VALUES (N'Hải Phòng', N'Hà Nội', 120);
IF NOT EXISTS (SELECT 1 FROM Routes WHERE Origin=N'Nam Định' AND Destination=N'Hà Nội')
    INSERT INTO Routes (Origin, Destination, DistanceKm) VALUES (N'Nam Định', N'Hà Nội', 90);
GO

/* 2. Insert Buses & Seats */
IF NOT EXISTS (SELECT 1 FROM Buses WHERE LicensePlate=N'29B-123.45')
    INSERT INTO Buses (LicensePlate, SeatCount, BusType) VALUES (N'29B-123.45', 40, N'Ghế ngồi');

IF NOT EXISTS (SELECT 1 FROM Buses WHERE LicensePlate=N'29B-678.90')
    INSERT INTO Buses (LicensePlate, SeatCount, BusType) VALUES (N'29B-678.90', 40, N'Ghế ngồi cao cấp');

IF NOT EXISTS (SELECT 1 FROM Buses WHERE LicensePlate=N'29B-999.88')
    INSERT INTO Buses (LicensePlate, SeatCount, BusType) VALUES (N'29B-999.88', 34, N'Limousine VIP');

INSERT INTO Seats (BusId, SeatNumber)
SELECT b.BusId, CONVERT(NVARCHAR(10), n.Number)
FROM Buses b CROSS JOIN (SELECT TOP (40) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) Number FROM sys.objects) n
WHERE n.Number <= b.SeatCount
  AND NOT EXISTS (SELECT 1 FROM Seats s WHERE s.BusId=b.BusId AND s.SeatNumber=CONVERT(NVARCHAR(10), n.Number));
GO

/* 3. Insert Trips for next 7 days */
DECLARE @day INT = 0;
WHILE @day <= 7
BEGIN
    DECLARE @targetDate DATE = DATEADD(DAY, @day, CAST(GETDATE() AS DATE));

    IF NOT EXISTS (SELECT 1 FROM Trips t JOIN Routes r ON r.RouteId=t.RouteId WHERE r.Origin=N'Hà Nội' AND r.Destination=N'Hải Phòng' AND CAST(t.DepartureTime AS DATE)=@targetDate AND DATEPART(HOUR, t.DepartureTime)=7)
    BEGIN
        INSERT INTO Trips (RouteId, BusId, DepartureTime, ArrivalTime, Price)
        SELECT r.RouteId, 1, DATEADD(MINUTE, 30, DATEADD(HOUR, 7, CAST(@targetDate AS DATETIME2))),
               DATEADD(MINUTE, 30, DATEADD(HOUR, 9, CAST(@targetDate AS DATETIME2))), 140000
        FROM Routes r WHERE r.Origin=N'Hà Nội' AND r.Destination=N'Hải Phòng';
    END;

    IF NOT EXISTS (SELECT 1 FROM Trips t JOIN Routes r ON r.RouteId=t.RouteId WHERE r.Origin=N'Hà Nội' AND r.Destination=N'Hải Phòng' AND CAST(t.DepartureTime AS DATE)=@targetDate AND DATEPART(HOUR, t.DepartureTime)=14)
    BEGIN
        INSERT INTO Trips (RouteId, BusId, DepartureTime, ArrivalTime, Price)
        SELECT r.RouteId, 2, DATEADD(HOUR, 14, CAST(@targetDate AS DATETIME2)),
               DATEADD(HOUR, 16, CAST(@targetDate AS DATETIME2)), 150000
        FROM Routes r WHERE r.Origin=N'Hà Nội' AND r.Destination=N'Hải Phòng';
    END;

    IF NOT EXISTS (SELECT 1 FROM Trips t JOIN Routes r ON r.RouteId=t.RouteId WHERE r.Origin=N'Hà Nội' AND r.Destination=N'Nam Định' AND CAST(t.DepartureTime AS DATE)=@targetDate AND DATEPART(HOUR, t.DepartureTime)=10)
    BEGIN
        INSERT INTO Trips (RouteId, BusId, DepartureTime, ArrivalTime, Price)
        SELECT r.RouteId, 1, DATEADD(MINUTE, 30, DATEADD(HOUR, 10, CAST(@targetDate AS DATETIME2))),
               DATEADD(MINUTE, 30, DATEADD(HOUR, 12, CAST(@targetDate AS DATETIME2))), 110000
        FROM Routes r WHERE r.Origin=N'Hà Nội' AND r.Destination=N'Nam Định';
    END;

    IF NOT EXISTS (SELECT 1 FROM Trips t JOIN Routes r ON r.RouteId=t.RouteId WHERE r.Origin=N'Hà Nội' AND r.Destination=N'Thanh Hóa' AND CAST(t.DepartureTime AS DATE)=@targetDate AND DATEPART(HOUR, t.DepartureTime)=8)
    BEGIN
        INSERT INTO Trips (RouteId, BusId, DepartureTime, ArrivalTime, Price)
        SELECT r.RouteId, 2, DATEADD(HOUR, 8, CAST(@targetDate AS DATETIME2)),
               DATEADD(MINUTE, 30, DATEADD(HOUR, 11, CAST(@targetDate AS DATETIME2))), 180000
        FROM Routes r WHERE r.Origin=N'Hà Nội' AND r.Destination=N'Thanh Hóa';
    END;

    IF NOT EXISTS (SELECT 1 FROM Trips t JOIN Routes r ON r.RouteId=t.RouteId WHERE r.Origin=N'Hà Nội' AND r.Destination=N'Ninh Bình' AND CAST(t.DepartureTime AS DATE)=@targetDate AND DATEPART(HOUR, t.DepartureTime)=15)
    BEGIN
        INSERT INTO Trips (RouteId, BusId, DepartureTime, ArrivalTime, Price)
        SELECT r.RouteId, 1, DATEADD(MINUTE, 30, DATEADD(HOUR, 15, CAST(@targetDate AS DATETIME2))),
               DATEADD(MINUTE, 30, DATEADD(HOUR, 17, CAST(@targetDate AS DATETIME2))), 120000
        FROM Routes r WHERE r.Origin=N'Hà Nội' AND r.Destination=N'Ninh Bình';
    END;

    SET @day = @day + 1;
END;
GO

