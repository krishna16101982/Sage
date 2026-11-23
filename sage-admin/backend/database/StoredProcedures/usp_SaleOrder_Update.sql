-- User Panel: edits an order saved with the "New order form" (dbo.usp_SaleOrder_Create). Everything on the form can
-- change: customer, plan, seats, sale price / discount, duration, start date, free months, GST, the five list values,
-- contacts, bill-to address, wording and the included item quantity. The sale number, seller, manager and order date stay.
--   Who: the seller or the seller's manager (ManagerId saved with the sale), and their account must be active.
--   Which: active orders saved with the order form, with no cancellation request waiting.
--   Price: worked out again with the same rules as usp_SaleOrder_Create; seat prices are those of the financial year of
--   the ORDER DATE (CreatedAtUtc, India time), so an order edited after 1 April keeps its year's prices.
--   A changed customer must be active and a changed plan available for sale; the order's own customer / plan may have
--   been switched off since without blocking the edit.
--   Renewal date = start + paid duration + free months (unless @RenewalDate is given), as when the order was saved.
-- Raises 50404 (sale / customer / plan not found) or 50400 (anything not allowed). Returns the sale's PublicId.
CREATE OR ALTER PROCEDURE dbo.usp_SaleOrder_Update
    @SalePublicId               UNIQUEIDENTIFIER,
    @EditorSalesUserId          BIGINT,
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
    @OrderWording               NVARCHAR(MAX)  = NULL,
    @IncludedItemQuantity       INT            = NULL,
    @TrackingStatusId           INT            = NULL   -- Draft / Sent to DocuSign / Signed; NULL keeps it (Cancelled is done by cancelling the sale)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    -- The order, as the editor may see it (their own, or their team's for the manager).
    DECLARE @saleId BIGINT, @status VARCHAR(20), @hasOrderForm BIT, @createdAtUtc DATETIME2(3),
            @currentCustomerId BIGINT, @currentPlanId BIGINT;
    SELECT @saleId = Id, @status = Status, @createdAtUtc = CreatedAtUtc,
           @hasOrderForm = CASE WHEN BillingFrequencyId IS NOT NULL THEN 1 ELSE 0 END,
           @currentCustomerId = CustomerId, @currentPlanId = PlanId
    FROM dbo.Sales
    WHERE PublicId = @SalePublicId AND (SalesUserId = @EditorSalesUserId OR ManagerId = @EditorSalesUserId);
    IF @saleId IS NULL
        THROW 50404, 'Sale not found', 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.SalesUsers WHERE Id = @EditorSalesUserId AND IsDeleted = 0 AND Status = 'Active')
        THROW 50400, 'Your account is not active', 1;
    IF @hasOrderForm = 0
        THROW 50400, 'Only orders saved with the New order form can be edited', 1;
    IF @status <> 'Active'
        THROW 50400, 'A cancelled order cannot be edited', 1;
    IF EXISTS (SELECT 1 FROM dbo.SaleCancelRequests WHERE SaleId = @saleId AND Status = 'Pending')
        THROW 50400, 'This order has a cancellation request waiting for the manager; it cannot be edited until it is decided', 1;

    -- Order-form details (same checks as usp_SaleOrder_Create).
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
    IF @TrackingStatusId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.OrderTrackingStatuses WHERE Id = @TrackingStatusId AND Code <> 'CANCELLED')
        THROW 50400, 'Choose Draft, Sent to DocuSign or Signed (Cancelled cancels the sale)', 1;

    -- Customer: a different one must be active.
    DECLARE @customerId BIGINT, @customerActive BIT;
    SELECT @customerId = Id, @customerActive = IsActive FROM dbo.Customers WHERE PublicId = @CustomerPublicId;
    IF @customerId IS NULL
        THROW 50404, 'Company not found', 1;
    IF @customerActive = 0 AND @customerId <> @currentCustomerId
        THROW 50400, 'This company is inactive', 1;

    -- Plan: a different one must be available for sale (plan, product and category active).
    DECLARE @productId BIGINT, @categoryId BIGINT, @planPrice DECIMAL(18,2), @planCost DECIMAL(18,2),
            @planUnit VARCHAR(10), @planCount INT, @available BIT;
    SELECT @productId = p.Id, @categoryId = c.Id, @planPrice = pl.Price, @planCost = pl.Cost,
           @planUnit = pl.DurationUnit, @planCount = pl.DurationCount,
           @available = CASE WHEN pl.IsActive = 1 AND p.IsActive = 1 AND c.IsActive = 1 THEN 1 ELSE 0 END
    FROM dbo.ProductPlans pl
    JOIN dbo.Products p ON p.Id = pl.ProductId
    JOIN dbo.ProductCategories c ON c.Id = p.CategoryId
    WHERE pl.Id = @PlanId;
    IF @productId IS NULL
        THROW 50404, 'Plan not found', 1;
    IF @available = 0 AND @PlanId <> @currentPlanId
        THROW 50400, 'This plan is not available for sale', 1;

    IF @Quantity IS NULL OR @Quantity < 1 OR @Quantity > 100000
        THROW 50400, 'Quantity must be between 1 and 100000', 1;

    -- Duration and start date.
    DECLARE @unit VARCHAR(10) = ISNULL(@DurationUnit, @planUnit);
    DECLARE @count INT = ISNULL(@DurationCount, @planCount);
    IF @unit NOT IN ('Month', 'Quarter', 'Year')
        THROW 50400, 'Duration unit must be Month, Quarter or Year', 1;
    IF @count < 1 OR @count > 120
        THROW 50400, 'Duration must be between 1 and 120', 1;
    IF @StartDate IS NULL
        THROW 50400, 'Start date is required', 1;

    -- Price, as usp_SaleOrder_Create, with the financial year of the order date (India time).
    DECLARE @paidMonths INT = CASE @unit WHEN 'Year' THEN 12 WHEN 'Quarter' THEN 3 ELSE 1 END * @count;
    DECLARE @planMonths INT = CASE @planUnit WHEN 'Year' THEN 12 WHEN 'Quarter' THEN 3 ELSE 1 END * @planCount;
    DECLARE @terms DECIMAL(18,6) = CASE WHEN @paidMonths >= 1 AND @planMonths >= 1 THEN CAST(@paidMonths AS DECIMAL(18,6)) / @planMonths ELSE 1 END;

    DECLARE @orderDate DATE = CAST(SWITCHOFFSET(CAST(@createdAtUtc AS DATETIMEOFFSET), '+05:30') AS DATE);
    DECLARE @fiscalYear INT = CASE WHEN MONTH(@orderDate) >= 4 THEN YEAR(@orderDate) + 1 ELSE YEAR(@orderDate) END;
    DECLARE @seatPriced BIT = CASE WHEN EXISTS (SELECT 1 FROM dbo.ProductPlanSeatPrices WHERE PlanId = @PlanId) THEN 1 ELSE 0 END;
    DECLARE @unitPrice DECIMAL(18,2) = @planPrice, @unitCost DECIMAL(18,2) = @planCost, @seatRange NVARCHAR(30),
            @list DECIMAL(18,2), @costAmount DECIMAL(18,2), @sale DECIMAL(18,2);

    IF @seatPriced = 1
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM dbo.ProductPlanSeatPrices WHERE PlanId = @PlanId AND FiscalYear = @fiscalYear)
        BEGIN
            DECLARE @yearMessage NVARCHAR(200) = CONCAT(N'This plan has no seat prices for FY', RIGHT(CAST(@fiscalYear AS VARCHAR(4)), 2), N' (the order''s year) - ask the admin to add them');
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

    -- Renewal date: start + paid duration + free months, as when the order was saved.
    DECLARE @renewal DATE = ISNULL(@RenewalDate,
        DATEADD(MONTH, @free, CASE @unit WHEN 'Month' THEN DATEADD(MONTH, @count, @StartDate)
                                         WHEN 'Quarter' THEN DATEADD(MONTH, 3 * @count, @StartDate)
                                         ELSE DATEADD(YEAR, @count, @StartDate) END));
    IF @renewal <= @StartDate
        THROW 50400, 'Renewal date must be after the start date', 1;

    UPDATE dbo.Sales
    SET CustomerId = @customerId, CategoryId = @categoryId, ProductId = @productId, PlanId = @PlanId, Quantity = @Quantity,
        UnitPrice = @unitPrice, UnitCost = @unitCost, ListAmount = @list, SaleAmount = @sale,
        DiscountAmount = @list - @sale, CostAmount = @costAmount,
        PriceFiscalYear = CASE WHEN @seatPriced = 1 THEN @fiscalYear END, PriceSeatRange = @seatRange,
        DurationUnit = @unit, DurationCount = @count, StartDate = @StartDate, RenewalDate = @renewal, Notes = @Notes,
        GstPercent = @GstPercent, TaxAmount = ROUND(@sale * @GstPercent / 100, 2), FreeMonths = @free,
        BillingFrequencyId = @BillingFrequencyId, BillingMethodId = @BillingMethodId,
        PaymentMethodId = @PaymentMethodId, PaymentTermId = @PaymentTermId, CurrencyId = @CurrencyId,
        PrimaryContactName = @PrimaryContactName, PrimaryContactDesignation = @PrimaryContactDesignation,
        PrimaryContactEmail = @PrimaryContactEmail, PrimaryContactPhone = @PrimaryContactPhone,
        BillingContactName = @BillingContactName, BillingContactDesignation = @BillingContactDesignation,
        BillingContactEmail = @BillingContactEmail, BillingContactPhone = @BillingContactPhone,
        BillToAddress = @BillToAddress,
        OrderWording = @OrderWording,
        IncludedItemQuantity = @IncludedItemQuantity,
        TrackingStatusId = ISNULL(@TrackingStatusId, TrackingStatusId)
    WHERE Id = @saleId;

    SELECT @SalePublicId;
END
GO
