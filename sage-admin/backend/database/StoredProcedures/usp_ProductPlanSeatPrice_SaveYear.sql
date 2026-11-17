-- Admin Panel > Products > plan > Seat prices: replaces ALL seat ranges of one plan for one financial year.
-- @Ranges is a JSON array: [{"minSeats":1,"maxSeats":9,"price":111578,"cost":null}, ...] (maxSeats null = and above).
-- Rules: the first range starts at 1 seat; each next range starts right after the previous one ends (no gaps, no
-- overlaps); only the last range may be open-ended; prices and costs are not negative. An empty array removes that
-- year's seat prices. Raises 50404 (plan not found) or 50400 (ranges not valid). Returns the number of ranges saved.
CREATE OR ALTER PROCEDURE dbo.usp_ProductPlanSeatPrice_SaveYear
    @PlanId       BIGINT,
    @FiscalYear   INT,
    @Ranges       NVARCHAR(MAX),
    @ActorUserId  BIGINT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.ProductPlans WHERE Id = @PlanId)
        THROW 50404, 'Plan not found', 1;
    IF @FiscalYear IS NULL OR @FiscalYear < 2000 OR @FiscalYear > 2100
        THROW 50400, 'Choose a financial year', 1;
    IF @Ranges IS NULL OR ISJSON(@Ranges) = 0
        THROW 50400, 'The seat ranges are not valid', 1;

    DECLARE @rows TABLE (RowNo INT IDENTITY(1,1) PRIMARY KEY, MinSeats INT, MaxSeats INT, Price DECIMAL(18,2), Cost DECIMAL(18,2));
    INSERT @rows (MinSeats, MaxSeats, Price, Cost)
    SELECT MinSeats, MaxSeats, Price, Cost
    FROM OPENJSON(@Ranges) WITH (MinSeats INT '$.minSeats', MaxSeats INT '$.maxSeats', Price DECIMAL(18,2) '$.price', Cost DECIMAL(18,2) '$.cost')
    ORDER BY MinSeats;

    DECLARE @count INT = (SELECT COUNT(*) FROM @rows);
    IF @count > 0
    BEGIN
        IF EXISTS (SELECT 1 FROM @rows WHERE MinSeats IS NULL OR Price IS NULL)
            THROW 50400, 'Every range needs its first seat and a price', 1;
        IF (SELECT MinSeats FROM @rows WHERE RowNo = 1) <> 1
            THROW 50400, 'The first range must start at 1 seat', 1;
        IF EXISTS (SELECT 1 FROM @rows WHERE MaxSeats IS NOT NULL AND MaxSeats < MinSeats)
            THROW 50400, 'A range cannot end before it starts', 1;
        IF EXISTS (SELECT 1 FROM @rows WHERE MaxSeats IS NULL AND RowNo < @count)
            THROW 50400, 'Only the last range can be open-ended (and above)', 1;
        IF EXISTS (SELECT 1 FROM @rows a JOIN @rows b ON b.RowNo = a.RowNo + 1 WHERE b.MinSeats <> a.MaxSeats + 1)
            THROW 50400, 'Each range must start right after the previous one ends (no gaps or overlaps)', 1;
        IF EXISTS (SELECT 1 FROM @rows WHERE Price < 0 OR Cost < 0)
            THROW 50400, 'Prices and costs cannot be negative', 1;
    END

    BEGIN TRAN;

    DELETE dbo.ProductPlanSeatPrices WHERE PlanId = @PlanId AND FiscalYear = @FiscalYear;
    INSERT dbo.ProductPlanSeatPrices (PlanId, FiscalYear, MinSeats, MaxSeats, Price, Cost, CreatedBy)
    SELECT @PlanId, @FiscalYear, MinSeats, MaxSeats, Price, Cost, @ActorUserId FROM @rows;

    COMMIT;

    SELECT CAST(@count AS BIGINT);
END
GO
