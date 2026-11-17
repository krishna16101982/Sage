/*
  LinkedIn Sales Navigator: plans Advanced and Advanced Plus with their seat prices for FY26 and FY27 (INR, per seat,
  one year). Run by hand in SSMS against the Sage database (not part of Deploy-Database.ps1). Safe to run again:
    - the product must already exist (Admin Panel > Products); set @ProductName to its exact name;
    - each plan is created if missing (price = the FY27 1-9 seat price, cost = @AdvancedCost / @AdvancedPlusCost);
      an existing plan keeps its price, cost and description and only gets the Team Link Extend text when it has none;
    - each year's seat prices are REPLACED through dbo.usp_ProductPlanSeatPrice_SaveYear, which checks the ranges.
  FY26 = Apr 2025 - Mar 2026 (FiscalYear 2026); FY27 = Apr 2026 - Mar 2027 (FiscalYear 2027).
  On an order every seat is charged the per-seat price of the range the seat count falls in (8 seats x the 1-9 price).
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @ProductName       NVARCHAR(100) = N'LinkedIn';   -- the product as named in Admin Panel > Products
DECLARE @AdvancedCost      DECIMAL(18,2) = 0;             -- cost per seat for a NEW Advanced plan (edit later in Admin)
DECLARE @AdvancedPlusCost  DECIMAL(18,2) = 0;             -- cost per seat for a NEW Advanced Plus plan

DECLARE @AdvancedDescription NVARCHAR(1000) = N'Find leads and forge deeper relationships by leveraging real-time sales intelligence and team collaboration. Provides access to relevant lead and account insights, powered by LinkedIn''s professional network data. Includes 50 InMail messages per month, Buyer Interest, BI Integrations, List Sharing, Smart Links, Team Link, and other features. Get even more out of Sales Navigator with access to qualifying Sales Navigator Success Plans, which include onboarding, education, adoption, and support resources. Learn more here: https://lnkd.in/SuccessPlans';
DECLARE @TeamLinkTitle       NVARCHAR(200)  = N'Sales Navigator Team Link Extend Seat';
-- {totalMonths} is filled in on the order form (paid + free months, e.g. 14).
DECLARE @TeamLinkDescription NVARCHAR(1000) = N'{totalMonths} months 1000 Team Link Extend seats which are included with Customer''s Sales Navigator Advanced or Advanced Plus purchase.';

-- Seat ranges and prices per seat (MaxSeats NULL = 50,000 and above).
DECLARE @Prices TABLE (MinSeats INT, MaxSeats INT NULL, AdvancedFY26 DECIMAL(18,2), AdvancedFY27 DECIMAL(18,2), PlusFY26 DECIMAL(18,2), PlusFY27 DECIMAL(18,2));
INSERT @Prices (MinSeats, MaxSeats, AdvancedFY26, AdvancedFY27, PlusFY26, PlusFY27) VALUES
    (    1,     9, 106265, 111578, 118697, 124632),
    (   10,    24, 100207, 105217, 112749, 118386),
    (   25,    49,  97456, 102329, 109772, 115261),
    (   50,    99,  95503, 100278, 106801, 112141),
    (  100,   199,  94616,  99347, 105840, 111132),
    (  200,   399,  91460,  96033, 102811, 107952),
    (  400,   599,  88442,  92864,  99776, 104765),
    (  600,   799,  86389,  90708,  96800, 101640),
    (  800,   999,  83648,  87830,  93713,  98399),
    ( 1000,  1499,  80624,  84655,  90683,  95217),
    ( 1500,  1999,  80624,  84655,  90683,  95217),
    ( 2000,  2999,  80624,  84655,  90683,  95217),
    ( 3000,  4999,  62244,  65356,  69032,  72484),
    ( 5000,  9999,  62244,  65356,  69032,  72484),
    (10000, 14999,  44972,  47221,  50400,  52920),
    (15000, 19999,  44972,  47221,  50400,  52920),
    (20000, 24999,  44972,  47221,  50400,  52920),
    (25000, 34999,  44972,  47221,  50400,  52920),
    (35000, 49999,  44972,  47221,  50400,  52920),
    (50000,  NULL,  44972,  47221,  50400,  52920);

DECLARE @productId BIGINT = (SELECT Id FROM dbo.Products WHERE Name = @ProductName);
IF @productId IS NULL
    THROW 50404, 'Product not found: create it in Admin Panel > Products first, or set @ProductName to its exact name.', 1;

BEGIN TRAN;

-- 1. Plans (master).
DECLARE @advancedId BIGINT = (SELECT Id FROM dbo.ProductPlans WHERE ProductId = @productId AND Name = N'Advanced');
IF @advancedId IS NULL
BEGIN
    DECLARE @advancedPrice DECIMAL(18,2) = (SELECT AdvancedFY27 FROM @Prices WHERE MinSeats = 1);
    DECLARE @created TABLE (Id BIGINT);
    INSERT @created EXEC dbo.usp_ProductPlan_Create
        @ProductId = @productId, @Name = N'Advanced', @Description = @AdvancedDescription,
        @IncludedItemTitle = @TeamLinkTitle, @IncludedItemDescription = @TeamLinkDescription,
        @DurationUnit = 'Year', @DurationCount = 1, @Price = @advancedPrice, @Cost = @AdvancedCost, @SortOrder = 1;
    SET @advancedId = (SELECT TOP 1 Id FROM @created);
    DELETE @created;
END

DECLARE @plusId BIGINT = (SELECT Id FROM dbo.ProductPlans WHERE ProductId = @productId AND Name = N'Advanced Plus');
IF @plusId IS NULL
BEGIN
    DECLARE @plusPrice DECIMAL(18,2) = (SELECT PlusFY27 FROM @Prices WHERE MinSeats = 1);
    DECLARE @createdPlus TABLE (Id BIGINT);
    -- Advanced Plus description: add it in Admin Panel > Products > Edit plan (not in the price sheet).
    INSERT @createdPlus EXEC dbo.usp_ProductPlan_Create
        @ProductId = @productId, @Name = N'Advanced Plus', @Description = NULL,
        @IncludedItemTitle = @TeamLinkTitle, @IncludedItemDescription = @TeamLinkDescription,
        @DurationUnit = 'Year', @DurationCount = 1, @Price = @plusPrice, @Cost = @AdvancedPlusCost, @SortOrder = 2;
    SET @plusId = (SELECT TOP 1 Id FROM @createdPlus);
END

-- Existing plans: add the Team Link Extend text only where it is missing.
UPDATE dbo.ProductPlans
SET IncludedItemTitle = @TeamLinkTitle, IncludedItemDescription = @TeamLinkDescription, UpdatedAtUtc = SYSUTCDATETIME()
WHERE Id IN (@advancedId, @plusId) AND IncludedItemTitle IS NULL;

-- 2. Seat prices (details): each plan and year replaced as a whole.
DECLARE @json NVARCHAR(MAX);
DECLARE @saved TABLE (Ranges BIGINT);

SET @json = (SELECT MinSeats AS minSeats, MaxSeats AS maxSeats, AdvancedFY26 AS price FROM @Prices ORDER BY MinSeats FOR JSON PATH, INCLUDE_NULL_VALUES);
INSERT @saved EXEC dbo.usp_ProductPlanSeatPrice_SaveYear @PlanId = @advancedId, @FiscalYear = 2026, @Ranges = @json;
SET @json = (SELECT MinSeats AS minSeats, MaxSeats AS maxSeats, AdvancedFY27 AS price FROM @Prices ORDER BY MinSeats FOR JSON PATH, INCLUDE_NULL_VALUES);
INSERT @saved EXEC dbo.usp_ProductPlanSeatPrice_SaveYear @PlanId = @advancedId, @FiscalYear = 2027, @Ranges = @json;
SET @json = (SELECT MinSeats AS minSeats, MaxSeats AS maxSeats, PlusFY26 AS price FROM @Prices ORDER BY MinSeats FOR JSON PATH, INCLUDE_NULL_VALUES);
INSERT @saved EXEC dbo.usp_ProductPlanSeatPrice_SaveYear @PlanId = @plusId, @FiscalYear = 2026, @Ranges = @json;
SET @json = (SELECT MinSeats AS minSeats, MaxSeats AS maxSeats, PlusFY27 AS price FROM @Prices ORDER BY MinSeats FOR JSON PATH, INCLUDE_NULL_VALUES);
INSERT @saved EXEC dbo.usp_ProductPlanSeatPrice_SaveYear @PlanId = @plusId, @FiscalYear = 2027, @Ranges = @json;

COMMIT;

-- 3. Check: 20 ranges per plan and year.
SELECT pl.Name AS PlanName, sp.FiscalYear, COUNT(*) AS Ranges, MIN(sp.Price) AS LowestPrice, MAX(sp.Price) AS HighestPrice
FROM dbo.ProductPlanSeatPrices sp
JOIN dbo.ProductPlans pl ON pl.Id = sp.PlanId
WHERE pl.Id IN (@advancedId, @plusId)
GROUP BY pl.Name, sp.FiscalYear
ORDER BY pl.Name, sp.FiscalYear;
