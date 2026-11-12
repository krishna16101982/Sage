-- Creates a product in a category. Raises 50404 (category not found), 50400 (category inactive) or
-- 50409 (code taken, or name taken within the category). Returns the new Id.
CREATE OR ALTER PROCEDURE dbo.usp_Product_Create
    @CategoryId  BIGINT,
    @Code        VARCHAR(50),
    @Name        NVARCHAR(100),
    @Description NVARCHAR(500) = NULL,
    @SortOrder   INT           = 0,
    @ActorUserId BIGINT        = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRAN;

    DECLARE @categoryActive BIT;
    SELECT @categoryActive = IsActive FROM dbo.ProductCategories WITH (HOLDLOCK) WHERE Id = @CategoryId;
    IF @categoryActive IS NULL
        THROW 50404, 'Category not found', 1;
    IF @categoryActive = 0
        THROW 50400, 'Products cannot be added to an inactive category', 1;

    IF EXISTS (SELECT 1 FROM dbo.Products WITH (UPDLOCK, HOLDLOCK) WHERE Code = @Code)
        THROW 50409, 'A product with this code already exists', 1;
    IF EXISTS (SELECT 1 FROM dbo.Products WITH (UPDLOCK, HOLDLOCK) WHERE CategoryId = @CategoryId AND Name = @Name)
        THROW 50409, 'A product with this name already exists in the category', 1;

    INSERT dbo.Products (CategoryId, Code, Name, Description, SortOrder, IsActive, CreatedBy)
    VALUES (@CategoryId, @Code, @Name, @Description, @SortOrder, 1, @ActorUserId);

    DECLARE @newId BIGINT = SCOPE_IDENTITY();

    COMMIT;

    SELECT @newId;
END
GO
