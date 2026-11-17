-- 032: seat-based prices for a plan, per financial year (e.g. LinkedIn Sales Navigator Advanced / Advanced Plus).
--   Master: dbo.ProductPlans (one row per plan: Advanced, Advanced Plus under the LinkedIn product).
--   Details: this table (financial year, seat range, price, cost per seat) - entered in Admin Panel > Products > plan.
--   FiscalYear: the Indian financial year by the year it ends in: 2027 = FY27 = 1 Apr 2026 to 31 Mar 2027.
--   One row per seat range: MinSeats..MaxSeats (MaxSeats NULL = "and above"). Ranges of a plan and year start at 1 and
--   follow on without gaps or overlaps (checked by dbo.usp_ProductPlanSeatPrice_SaveYear).
--   Price (and optional Cost) are per seat for one plan term. ALL seats of an order are charged at the price of the range
--   the order's seat count falls in, for the financial year of the order date. Cost NULL = the plan's own cost.
-- A plan without rows here keeps its single Price / Cost (dbo.ProductPlans).
IF OBJECT_ID(N'dbo.ProductPlanSeatPrices', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ProductPlanSeatPrices
    (
        Id            BIGINT IDENTITY(1,1) NOT NULL,
        PlanId        BIGINT          NOT NULL,
        FiscalYear    INT             NOT NULL,
        MinSeats      INT             NOT NULL,
        MaxSeats      INT             NULL,
        Price         DECIMAL(18,2)   NOT NULL,
        Cost          DECIMAL(18,2)   NULL,
        CreatedAtUtc  DATETIME2(3)    NOT NULL CONSTRAINT DF_ProductPlanSeatPrices_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        CreatedBy     BIGINT          NULL,
        CONSTRAINT PK_ProductPlanSeatPrices PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_ProductPlanSeatPrices_Plan FOREIGN KEY (PlanId) REFERENCES dbo.ProductPlans (Id),
        CONSTRAINT FK_ProductPlanSeatPrices_CreatedBy FOREIGN KEY (CreatedBy) REFERENCES dbo.AdminUsers (Id),
        CONSTRAINT UQ_ProductPlanSeatPrices_Range UNIQUE (PlanId, FiscalYear, MinSeats),
        CONSTRAINT CK_ProductPlanSeatPrices_Year CHECK (FiscalYear BETWEEN 2000 AND 2100),
        CONSTRAINT CK_ProductPlanSeatPrices_Seats CHECK (MinSeats >= 1 AND (MaxSeats IS NULL OR MaxSeats >= MinSeats)),
        CONSTRAINT CK_ProductPlanSeatPrices_Price CHECK (Price >= 0 AND (Cost IS NULL OR Cost >= 0))
    );
    -- The order form looks up the range for a plan, year and seat count.
    CREATE NONCLUSTERED INDEX IX_ProductPlanSeatPrices_Lookup ON dbo.ProductPlanSeatPrices (PlanId, FiscalYear, MinSeats) INCLUDE (MaxSeats, Price, Cost);
END
GO

-- Which price an order used (shown on the sale): the financial year and the seat range. NULL = the plan's single price.
IF COL_LENGTH(N'dbo.Sales', N'PriceFiscalYear') IS NULL
    ALTER TABLE dbo.Sales ADD PriceFiscalYear INT NULL, PriceSeatRange NVARCHAR(30) NULL;
GO
