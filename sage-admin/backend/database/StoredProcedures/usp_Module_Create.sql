-- Creates a module. Raises 50409 when the code already exists (the check and the insert hold one lock, so no race).
-- @MenuPath: the sidebar link (e.g. '/inventory'), or NULL to keep the module out of the sidebar.
CREATE OR ALTER PROCEDURE dbo.usp_Module_Create
    @Code      VARCHAR(50),
    @Name      NVARCHAR(100),
    @Category  NVARCHAR(100) = NULL,
    @SortOrder INT,
    @MenuPath  NVARCHAR(200) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRAN;

    IF EXISTS (SELECT 1 FROM dbo.Modules WITH (UPDLOCK, HOLDLOCK) WHERE Code = @Code)
        THROW 50409, 'A module with this code already exists.', 1;

    INSERT dbo.Modules (Code, Name, Category, SortOrder, MenuPath, IsActive, IsSystem)
    VALUES (@Code, @Name, @Category, @SortOrder, @MenuPath, 1, 0);

    DECLARE @newId BIGINT = SCOPE_IDENTITY();

    COMMIT;

    SELECT @newId;
END
GO
