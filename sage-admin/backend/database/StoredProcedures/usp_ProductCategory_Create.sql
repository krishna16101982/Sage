-- Creates a product category. Raises 50409 when the code or name is taken. Returns the new Id.
CREATE OR ALTER PROCEDURE dbo.usp_ProductCategory_Create
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

    IF EXISTS (SELECT 1 FROM dbo.ProductCategories WITH (UPDLOCK, HOLDLOCK) WHERE Code = @Code)
        THROW 50409, 'A category with this code already exists', 1;
    IF EXISTS (SELECT 1 FROM dbo.ProductCategories WITH (UPDLOCK, HOLDLOCK) WHERE Name = @Name)
        THROW 50409, 'A category with this name already exists', 1;

    INSERT dbo.ProductCategories (Code, Name, Description, SortOrder, IsActive, CreatedBy)
    VALUES (@Code, @Name, @Description, @SortOrder, 1, @ActorUserId);

    DECLARE @newId BIGINT = SCOPE_IDENTITY();

    COMMIT;

    SELECT @newId;
END
GO
