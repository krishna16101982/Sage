-- Records a sale for @SalesUserId. Price and cost come from the price list (dbo.ProductPlans), never from the caller:
--   ListAmount = Price x Quantity, CostAmount = Cost x Quantity.
-- The caller gives EITHER @SaleAmount OR @DiscountAmount (amounts in INR); the other is calculated.
-- Duration defaults to the plan's; RenewalDate defaults to StartDate + duration and may be overridden (must be after StartDate).
-- Raises 50404 (customer / plan not found) or 50400 (anything not allowed). Returns the new sale's PublicId.
CREATE OR ALTER PROCEDURE dbo.usp_Sale_Create
    @SalesUserId       BIGINT,
    @CustomerPublicId  UNIQUEIDENTIFIER,
    @PlanId            BIGINT,
    @Quantity          INT,
    @SaleAmount        DECIMAL(18,2)  = NULL,
    @DiscountAmount    DECIMAL(18,2)  = NULL,
    @DurationUnit      VARCHAR(10)    = NULL,
    @DurationCount     INT            = NULL,
    @StartDate         DATE,
    @RenewalDate       DATE           = NULL,
    @Notes             NVARCHAR(1000) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    -- Seller (must be an active sales user). ManagerId is snapshotted for "team sales".
    DECLARE @managerId BIGINT, @sellerOk BIT = 0;
    SELECT @sellerOk = 1, @managerId = ManagerId
    FROM dbo.SalesUsers WHERE Id = @SalesUserId AND IsDeleted = 0 AND Status = 'Active';
    IF @sellerOk = 0
        THROW 50400, 'Your account is not active', 1;

    -- Customer
    DECLARE @customerId BIGINT, @customerActive BIT;
    SELECT @customerId = Id, @customerActive = IsActive FROM dbo.Customers WHERE PublicId = @CustomerPublicId;
    IF @customerId IS NULL
        THROW 50404, 'Customer not found', 1;
    IF @customerActive = 0
        THROW 50400, 'This customer is inactive', 1;

    -- Plan, product and category must all be active.
    DECLARE @productId BIGINT, @categoryId BIGINT, @price DECIMAL(18,2), @cost DECIMAL(18,2),
            @planUnit VARCHAR(10), @planCount INT, @available BIT;
    SELECT @productId = p.Id, @categoryId = c.Id, @price = pl.Price, @cost = pl.Cost, @planUnit = pl.DurationUnit, @planCount = pl.DurationCount,
           @available = CASE WHEN pl.IsActive = 1 AND p.IsActive = 1 AND c.IsActive = 1 THEN 1 ELSE 0 END
    FROM dbo.ProductPlans pl
    JOIN dbo.Products p ON p.Id = pl.ProductId
    JOIN dbo.ProductCategories c ON c.Id = p.CategoryId
    WHERE pl.Id = @PlanId;
    IF @productId IS NULL
        THROW 50404, 'Plan not found', 1;
    IF @available = 0
        THROW 50400, 'This plan is not available for sale', 1;

    IF @Quantity IS NULL OR @Quantity < 1 OR @Quantity > 100000
        THROW 50400, 'Quantity must be between 1 and 100000', 1;

    -- Amounts: exactly one of sale price / discount.
    IF (@SaleAmount IS NULL AND @DiscountAmount IS NULL) OR (@SaleAmount IS NOT NULL AND @DiscountAmount IS NOT NULL)
        THROW 50400, 'Enter either the sale price or the discount', 1;

    DECLARE @list DECIMAL(18,2) = @price * @Quantity;
    DECLARE @costAmount DECIMAL(18,2) = @cost * @Quantity;
    DECLARE @sale DECIMAL(18,2);

    IF @DiscountAmount IS NOT NULL
    BEGIN
        IF @DiscountAmount < 0
            THROW 50400, 'Discount cannot be negative', 1;
        IF @DiscountAmount > @list
            THROW 50400, 'Discount cannot be more than the list amount', 1;
        SET @sale = @list - @DiscountAmount;
    END
    ELSE
    BEGIN
        IF @SaleAmount < 0
            THROW 50400, 'Sale price cannot be negative', 1;
        IF @SaleAmount > @list
            THROW 50400, 'Sale price cannot be more than the list amount', 1;
        SET @sale = @SaleAmount;
    END

    -- Duration and renewal date.
    DECLARE @unit VARCHAR(10) = ISNULL(@DurationUnit, @planUnit);
    DECLARE @count INT = ISNULL(@DurationCount, @planCount);
    IF @unit NOT IN ('Month', 'Quarter', 'Year')
        THROW 50400, 'Duration unit must be Month, Quarter or Year', 1;
    IF @count < 1 OR @count > 120
        THROW 50400, 'Duration must be between 1 and 120', 1;
    IF @StartDate IS NULL
        THROW 50400, 'Start date is required', 1;

    DECLARE @renewal DATE = ISNULL(@RenewalDate,
        CASE @unit WHEN 'Month' THEN DATEADD(MONTH, @count, @StartDate)
                   WHEN 'Quarter' THEN DATEADD(MONTH, 3 * @count, @StartDate)
                   ELSE DATEADD(YEAR, @count, @StartDate) END);
    IF @renewal <= @StartDate
        THROW 50400, 'Renewal date must be after the start date', 1;

    DECLARE @n BIGINT = NEXT VALUE FOR dbo.SaleNumberSequence;
    DECLARE @digits VARCHAR(20) = CAST(@n AS VARCHAR(20));
    DECLARE @saleNumber VARCHAR(20) = CONCAT('S-', YEAR(SYSUTCDATETIME()), '-', CASE WHEN LEN(@digits) < 6 THEN RIGHT('000000' + @digits, 6) ELSE @digits END);
    DECLARE @publicId UNIQUEIDENTIFIER = NEWID();

    INSERT dbo.Sales (PublicId, SaleNumber, CustomerId, CategoryId, ProductId, PlanId, SalesUserId, ManagerId, Quantity,
                      UnitPrice, UnitCost, ListAmount, SaleAmount, DiscountAmount, CostAmount,
                      DurationUnit, DurationCount, StartDate, RenewalDate, Notes)
    VALUES (@publicId, @saleNumber, @customerId, @categoryId, @productId, @PlanId, @SalesUserId, @managerId, @Quantity,
            @price, @cost, @list, @sale, @list - @sale, @costAmount,
            @unit, @count, @StartDate, @renewal, @Notes);

    SELECT @publicId;
END
GO
