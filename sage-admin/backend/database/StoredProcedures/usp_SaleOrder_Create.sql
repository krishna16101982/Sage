-- User Panel "New order form": records the sale through dbo.usp_Sale_Create (same price-list rules, same Sales row) and,
-- in the same transaction, adds the order-form details (DDL 029):
--   @GstPercent 0-28 -> TaxAmount = SaleAmount x GST.   @FreeMonths 0 to Settings Sales.MaxFreeMonths: added to the
--   renewal date at the same price (unless @RenewalDate is given).   Billing frequency / method and payment method / term:
--   (and currency) active values of the right list in dbo.MasterValues.   Contacts and bill-to address are stored as given (the API
--   checks their format).
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
    @OrderWording               NVARCHAR(MAX)  = NULL   -- JSON: the order form's wording sections (DDL 031)
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
    IF NULLIF(LTRIM(RTRIM(@PrimaryContactName)), N'') IS NULL
        THROW 50400, 'Enter the primary contact''s name', 1;

    BEGIN TRAN;

    -- The sale itself: price, discount limit, duration, sale number, exactly as the "New sale" page.
    DECLARE @created TABLE (PublicId UNIQUEIDENTIFIER);
    INSERT @created
    EXEC dbo.usp_Sale_Create
        @SalesUserId = @SalesUserId, @CustomerPublicId = @CustomerPublicId, @PlanId = @PlanId, @Quantity = @Quantity,
        @SaleAmount = @SaleAmount, @DiscountAmount = @DiscountAmount, @DurationUnit = @DurationUnit, @DurationCount = @DurationCount,
        @StartDate = @StartDate, @RenewalDate = @RenewalDate, @Notes = @Notes;
    DECLARE @publicId UNIQUEIDENTIFIER = (SELECT TOP 1 PublicId FROM @created);

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
        OrderWording = @OrderWording
    WHERE PublicId = @publicId;

    COMMIT;

    SELECT @publicId;
END
GO
