-- Creates a plan (price-list entry) for a product. Raises 50404 (product not found), 50400 (product inactive)
-- or 50409 (plan name taken for the product). Returns the new Id.
CREATE OR ALTER PROCEDURE dbo.usp_ProductPlan_Create
    @ProductId     BIGINT,
    @Name          NVARCHAR(100),
    @Description   NVARCHAR(500) = NULL,
    @DurationUnit  VARCHAR(10),
    @DurationCount INT,
    @Price         DECIMAL(18,2),
    @Cost          DECIMAL(18,2),
    @SortOrder     INT           = 0,
    @ActorUserId   BIGINT        = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRAN;

    DECLARE @productActive BIT;
    SELECT @productActive = IsActive FROM dbo.Products WITH (HOLDLOCK) WHERE Id = @ProductId;
    IF @productActive IS NULL
        THROW 50404, 'Product not found', 1;
    IF @productActive = 0
        THROW 50400, 'Plans cannot be added to an inactive product', 1;

    IF EXISTS (SELECT 1 FROM dbo.ProductPlans WITH (UPDLOCK, HOLDLOCK) WHERE ProductId = @ProductId AND Name = @Name)
        THROW 50409, 'A plan with this name already exists for the product', 1;

    INSERT dbo.ProductPlans (ProductId, Name, Description, DurationUnit, DurationCount, Price, Cost, SortOrder, IsActive, CreatedBy)
    VALUES (@ProductId, @Name, @Description, @DurationUnit, @DurationCount, @Price, @Cost, @SortOrder, 1, @ActorUserId);

    DECLARE @newId BIGINT = SCOPE_IDENTITY();

    COMMIT;

    SELECT @newId;
END
GO
