-- Updates a plan (its product never changes). Price and cost changes affect future sales only: a sale keeps the
-- price and cost it was saved with. Raises 50404 (not found) or 50409 (name taken for the product).
CREATE OR ALTER PROCEDURE dbo.usp_ProductPlan_Update
    @Id            BIGINT,
    @Name          NVARCHAR(100),
    @Description   NVARCHAR(500) = NULL,
    @DurationUnit  VARCHAR(10),
    @DurationCount INT,
    @Price         DECIMAL(18,2),
    @Cost          DECIMAL(18,2),
    @SortOrder     INT,
    @IsActive      BIT,
    @ActorUserId   BIGINT        = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRAN;

    DECLARE @productId BIGINT;
    SELECT @productId = ProductId FROM dbo.ProductPlans WITH (UPDLOCK, HOLDLOCK) WHERE Id = @Id;
    IF @productId IS NULL
        THROW 50404, 'Plan not found', 1;
    IF EXISTS (SELECT 1 FROM dbo.ProductPlans WITH (UPDLOCK, HOLDLOCK) WHERE ProductId = @productId AND Name = @Name AND Id <> @Id)
        THROW 50409, 'A plan with this name already exists for the product', 1;

    UPDATE dbo.ProductPlans
    SET Name = @Name, Description = @Description, DurationUnit = @DurationUnit, DurationCount = @DurationCount,
        Price = @Price, Cost = @Cost, SortOrder = @SortOrder, IsActive = @IsActive,
        UpdatedAtUtc = SYSUTCDATETIME(), UpdatedBy = @ActorUserId
    WHERE Id = @Id;

    COMMIT;

    SELECT CAST(1 AS BIGINT);
END
GO
