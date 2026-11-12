-- Updates a product (the code never changes). The product may move to another category, but not into an inactive one.
-- Raises 50404 (product or category not found), 50400 (target category inactive) or 50409 (name taken in the category).
CREATE OR ALTER PROCEDURE dbo.usp_Product_Update
    @Id          BIGINT,
    @CategoryId  BIGINT,
    @Name        NVARCHAR(100),
    @Description NVARCHAR(500) = NULL,
    @SortOrder   INT,
    @IsActive    BIT,
    @ActorUserId BIGINT        = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRAN;

    DECLARE @oldCategoryId BIGINT;
    SELECT @oldCategoryId = CategoryId FROM dbo.Products WITH (UPDLOCK, HOLDLOCK) WHERE Id = @Id;
    IF @oldCategoryId IS NULL
        THROW 50404, 'Product not found', 1;

    DECLARE @categoryActive BIT;
    SELECT @categoryActive = IsActive FROM dbo.ProductCategories WITH (HOLDLOCK) WHERE Id = @CategoryId;
    IF @categoryActive IS NULL
        THROW 50404, 'Category not found', 1;
    IF @categoryActive = 0 AND @CategoryId <> @oldCategoryId
        THROW 50400, 'Products cannot be moved into an inactive category', 1;

    IF EXISTS (SELECT 1 FROM dbo.Products WITH (UPDLOCK, HOLDLOCK) WHERE CategoryId = @CategoryId AND Name = @Name AND Id <> @Id)
        THROW 50409, 'A product with this name already exists in the category', 1;

    UPDATE dbo.Products
    SET CategoryId = @CategoryId, Name = @Name, Description = @Description, SortOrder = @SortOrder, IsActive = @IsActive,
        UpdatedAtUtc = SYSUTCDATETIME(), UpdatedBy = @ActorUserId
    WHERE Id = @Id;

    COMMIT;

    SELECT CAST(1 AS BIGINT);
END
GO
