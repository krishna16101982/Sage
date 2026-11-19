-- User Panel "New order form": records the sale through dbo.usp_Sale_Create (same price-list rules, same Sales row) and,
-- in the same transaction, adds the order-form details (DDL 029):
--   @GstPercent 0-28 -> TaxAmount = SaleAmount x GST.   @FreeMonths 0 to Settings Sales.MaxFreeMonths: added to the
--   renewal date at the same price (unless @RenewalDate is given).   Billing frequency / method and payment method / term:
--   (and currency) active values of the right list in dbo.MasterValues.   Contacts and bill-to address are stored as given (the API
--   checks their format).
-- Price: one plan term per unit = the seat range's price for seat-priced plans (DDL 032: every seat at the price of the
--   range the quantity falls in, financial year of the order date) or the plan's own price; times the number of plan
--   terms paid for (24 paid months of a 1-year plan = x2). The sale/discount and the company discount limit are checked
--   against that list amount. UnitPrice / UnitCost are stored for the whole paid term.
-- Raises 50404 / 50400 like usp_Sale_Create. Returns the new sale's PublicId.
CREATE OR ALTER PROCEDURE dbo.usp_SaleOrder_Create
    @SalesUserId                BIGINT,
    @CustomerPublicId           UNIQUEIDENTIFIER,
    @PlanId                     BIGINT,
    @Quantity                   INT,
    @SaleAmount                 DECIMAL(18,2)  = NULL,
    @DiscountAmount             DECIMAL(18,2)  = NULL,
    @DurationUnit               VARCHAR(10)    = NULL,
    @DurationCount              INT            = NULL,
    @StartDate                  DATE,
    @RenewalDate                DATE           = NULL,
    @Notes                      NVARCHAR(1000) = NULL,
    @GstPercent                 DECIMAL(5,2),
    @FreeMonths                 INT            = 0,
    @BillingFrequencyId         INT,
    @BillingMethodId            INT,
    @PaymentMethodId            INT,
    @PaymentTermId              INT,
    @CurrencyId                 INT,
    @PrimaryContactName         NVARCHAR(150),
    @PrimaryContactDesignation  NVARCHAR(100)  = NULL,
    @PrimaryContactEmail        NVARCHAR(256)  = NULL,
    @PrimaryContactPhone        NVARCHAR(20)   = NULL,
    @BillingContactName         NVARCHAR(150)  = NULL,
    @BillingContactDesignation  NVARCHAR(100)  = NULL,
    @BillingContactEmail        NVARCHAR(256)  = NULL,
    @BillingContactPhone        NVARCHAR(20)   = NULL,
    @BillToAddress              NVARCHAR(500)  = NULL,
    @OrderWording               NVARCHAR(MAX)  = NULL,  -- JSON: the order form's wording sections (DDL 031)
    @IncludedItemQuantity       INT            = NULL   -- quantity of the plan's included item, e.g. Team Link Extend seats (DDL 034)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    -- Order-form details are checked first, so nothing is saved when one is wrong.
    IF @GstPercent IS NULL OR @GstPercent < 0 OR @GstPercent > 28
        THROW 50400, 'GST must be between 0 and 28 percent', 1;

    DECLARE @free INT = ISNULL(@FreeMonths, 0);
    DECLARE @maxFree INT = ISNULL(TRY_CAST((SELECT SettingValue FROM dbo.Settings WHERE SettingKey = 'Sales.MaxFreeMonths') AS INT), 12);
    IF @free < 0 OR @free > @maxFree
    BEGIN
        DECLARE @freeMessage NVARCHAR(200) = CONCAT(N'Free months must be between 0 and ', @maxFree, N' (company setting)');
        THROW 50400, @freeMessage, 1;
    END

    IF NOT EXISTS (SELECT 1 FROM dbo.MasterValues WHERE Id = @BillingFrequencyId AND MasterType = 'BillingFrequency' AND IsActive = 1)
        THROW 50400, 'Choose a billing frequency from the list', 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.MasterValues WHERE Id = @BillingMethodId AND MasterType = 'BillingMethod' AND IsActive = 1)
        THROW 50400, 'Choose a billing method from the list', 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.MasterValues WHERE Id = @PaymentMethodId AND MasterType = 'PaymentMethod' AND IsActive = 1)
        THROW 50400, 'Choose a payment method from the list', 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.MasterValues WHERE Id = @PaymentTermId AND MasterType = 'PaymentTerm' AND IsActive = 1)
        THROW 50400, 'Choose a payment term from the list', 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.MasterValues WHERE Id = @CurrencyId AND MasterType = 'Currency' AND IsActive = 1)
        THROW 50400, 'Choose a currency from the list', 1;
    IF @OrderWording IS NOT NULL AND ISJSON(@OrderWording) = 0
        THROW 50400, 'The order wording is not valid', 1;
    IF @IncludedItemQuantity IS NOT NULL AND (@IncludedItemQuantity < 1 OR @IncludedItemQuantity > 10000000)
        THROW 50400, 'The Team Link Extend seat quantity must be between 1 and 10000000', 1;
    IF NULLIF(LTRIM(RTRIM(@PrimaryContactName)), N'') IS NULL
        THROW 50400, 'Enter the primary contact''s name', 1;

    -- Price of one plan term (per unit): the seat range's for seat-priced plans (financial year of the order date, India
    -- time; FY27 = Apr 2026 - Mar 2027), otherwise the plan's own. The paid term may be several plan terms: 24 paid months
    -- of a 1-year plan = 2 terms, so the unit price and cost are multiplied by paid months / plan months.
    DECLARE @planPrice DECIMAL(18,2), @planCost DECIMAL(18,2), @planUnit VARCHAR(10), @planCount INT;
    SELECT @planPrice = Price, @planCost = Cost, @planUnit = DurationUnit, @planCount = DurationCount FROM dbo.ProductPlans WHERE Id = @PlanId;
    IF @planPrice IS NULL
        THROW 50404, 'Plan not found', 1;
    IF @Quantity IS NULL OR @Quantity < 1 OR @Quantity > 100000
        THROW 50400, 'Quantity must be between 1 and 100000', 1;

    DECLARE @paidMonths INT = CASE ISNULL(@DurationUnit, @planUnit) WHEN 'Year' THEN 12 WHEN 'Quarter' THEN 3 ELSE 1 END * ISNULL(@DurationCount, @planCount);
    DECLARE @planMonths INT = CASE @planUnit WHEN 'Year' THEN 12 WHEN 'Quarter' THEN 3 ELSE 1 END * @planCount;
    -- (A wrong duration is refused by usp_Sale_Create below.)
    DECLARE @terms DECIMAL(18,6) = CASE WHEN @paidMonths >= 1 AND @planMonths >= 1 THEN CAST(@paidMonths AS DECIMAL(18,6)) / @planMonths ELSE 1 END;

    DECLARE @today DATE = CAST(SWITCHOFFSET(SYSDATETIMEOFFSET(), '+05:30') AS DATE);
    DECLARE @fiscalYear INT = CASE WHEN MONTH(@today) >= 4 THEN YEAR(@today) + 1 ELSE YEAR(@today) END;
    DECLARE @seatPriced BIT = CASE WHEN EXISTS (SELECT 1 FROM dbo.ProductPlanSeatPrices WHERE PlanId = @PlanId) THEN 1 ELSE 0 END;
    DECLARE @unitPrice DECIMAL(18,2) = @planPrice, @unitCost DECIMAL(18,2) = @planCost, @seatRange NVARCHAR(30),
            @list DECIMAL(18,2), @costAmount DECIMAL(18,2), @sale DECIMAL(18,2);

    IF @seatPriced = 1
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM dbo.ProductPlanSeatPrices WHERE PlanId = @PlanId AND FiscalYear = @fiscalYear)
        BEGIN
            DECLARE @yearMessage NVARCHAR(200) = CONCAT(N'This plan has no seat prices for FY', RIGHT(CAST(@fiscalYear AS VARCHAR(4)), 2), N' - ask the admin to add them');
            THROW 50400, @yearMessage, 1;
        END
        SET @unitPrice = NULL;
        SELECT TOP 1 @unitPrice = sp.Price, @unitCost = ISNULL(sp.Cost, @planCost),
               @seatRange = CASE WHEN sp.MaxSeats IS NULL THEN CONCAT(sp.MinSeats, '+') ELSE CONCAT(sp.MinSeats, '-', sp.MaxSeats) END
        FROM dbo.ProductPlanSeatPrices sp
        WHERE sp.PlanId = @PlanId AND sp.FiscalYear = @fiscalYear
          AND sp.MinSeats <= @Quantity AND (sp.MaxSeats IS NULL OR sp.MaxSeats >= @Quantity)
        ORDER BY sp.MinSeats DESC;
        IF @unitPrice IS NULL
            THROW 50400, 'No seat price covers this number of seats', 1;
    END

    SET @unitPrice = ROUND(@unitPrice * @terms, 2);
    SET @unitCost = ROUND(@unitCost * @terms, 2);
    SET @list = @unitPrice * @Quantity;
    SET @costAmount = @unitCost * @Quantity;

    -- The same amount rules as usp_Sale_Create, against this list amount.
    IF (@SaleAmount IS NULL AND @DiscountAmount IS NULL) OR (@SaleAmount IS NOT NULL AND @DiscountAmount IS NOT NULL)
        THROW 50400, 'Enter either the sale price or the discount', 1;
    IF @DiscountAmount < 0
        THROW 50400, 'Discount cannot be negative', 1;
    IF @DiscountAmount > @list
        THROW 50400, 'Discount cannot be more than the list amount', 1;
    IF @SaleAmount < 0
        THROW 50400, 'Sale price cannot be negative', 1;
    IF @SaleAmount > @list
        THROW 50400, 'Sale price cannot be more than the list amount', 1;
    SET @sale = ISNULL(@SaleAmount, @list - @DiscountAmount);

    DECLARE @maxDiscount DECIMAL(9,2) = ISNULL(TRY_CAST((SELECT SettingValue FROM dbo.Settings WHERE SettingKey = 'Sales.MaxDiscountPercent') AS DECIMAL(9,2)), 100);
    IF @list > 0 AND @maxDiscount < 100 AND (@list - @sale) * 100 > @maxDiscount * @list
    BEGIN
        DECLARE @limitMessage NVARCHAR(200) = CONCAT(N'The discount cannot be more than ', FORMAT(@maxDiscount, '0.##'), N' percent of the list amount (company limit)');
        THROW 50400, @limitMessage, 1;
    END

    BEGIN TRAN;

    -- The sale itself: seller, customer, plan, duration, sale number, exactly as the "New sale" page. It is created at the
    -- plan's plain price with no discount and gets this order's amounts right after.
    DECLARE @created TABLE (PublicId UNIQUEIDENTIFIER);
    INSERT @created
    EXEC dbo.usp_Sale_Create
        @SalesUserId = @SalesUserId, @CustomerPublicId = @CustomerPublicId, @PlanId = @PlanId, @Quantity = @Quantity,
        @SaleAmount = NULL, @DiscountAmount = 0, @DurationUnit = @DurationUnit, @DurationCount = @DurationCount,
        @StartDate = @StartDate, @RenewalDate = @RenewalDate, @Notes = @Notes;
    DECLARE @publicId UNIQUEIDENTIFIER = (SELECT TOP 1 PublicId FROM @created);

    UPDATE dbo.Sales
    SET UnitPrice = @unitPrice, UnitCost = @unitCost, ListAmount = @list, SaleAmount = @sale,
        DiscountAmount = @list - @sale, CostAmount = @costAmount,
        PriceFiscalYear = CASE WHEN @seatPriced = 1 THEN @fiscalYear END, PriceSeatRange = @seatRange
    WHERE PublicId = @publicId;

    UPDATE dbo.Sales
    SET GstPercent = @GstPercent,
        TaxAmount = ROUND(SaleAmount * @GstPercent / 100, 2),
        FreeMonths = @free,
        RenewalDate = CASE WHEN @RenewalDate IS NULL THEN DATEADD(MONTH, @free, RenewalDate) ELSE RenewalDate END,
        BillingFrequencyId = @BillingFrequencyId, BillingMethodId = @BillingMethodId,
        PaymentMethodId = @PaymentMethodId, PaymentTermId = @PaymentTermId, CurrencyId = @CurrencyId,
        PrimaryContactName = @PrimaryContactName, PrimaryContactDesignation = @PrimaryContactDesignation,
        PrimaryContactEmail = @PrimaryContactEmail, PrimaryContactPhone = @PrimaryContactPhone,
        BillingContactName = @BillingContactName, BillingContactDesignation = @BillingContactDesignation,
        BillingContactEmail = @BillingContactEmail, BillingContactPhone = @BillingContactPhone,
        BillToAddress = @BillToAddress,
        OrderWording = @OrderWording,
        IncludedItemQuantity = @IncludedItemQuantity
    WHERE PublicId = @publicId;

    COMMIT;

    SELECT @publicId;
END
GO
