-- Updates a product category (the code never changes). Raises 50404 when not found and 50409 when the name is taken.
CREATE OR ALTER PROCEDURE dbo.usp_ProductCategory_Update
    @Id          BIGINT,
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

    IF NOT EXISTS (SELECT 1 FROM dbo.ProductCategories WITH (UPDLOCK, HOLDLOCK) WHERE Id = @Id)
        THROW 50404, 'Category not found', 1;
    IF EXISTS (SELECT 1 FROM dbo.ProductCategories WITH (UPDLOCK, HOLDLOCK) WHERE Name = @Name AND Id <> @Id)
        THROW 50409, 'A category with this name already exists', 1;

    UPDATE dbo.ProductCategories
    SET Name = @Name, Description = @Description, SortOrder = @SortOrder, IsActive = @IsActive,
        UpdatedAtUtc = SYSUTCDATETIME(), UpdatedBy = @ActorUserId
    WHERE Id = @Id;

    COMMIT;

    SELECT CAST(1 AS BIGINT);
END
GO
