-- Creates a permission. Code is built here as Module.Code + '.' + Action.
-- Raises 50404 when the module does not exist and 50409 when the permission already exists
-- (the check and the insert hold one lock, so no race).
CREATE OR ALTER PROCEDURE dbo.usp_Permission_Create
    @ModuleId    BIGINT,
    @Action      VARCHAR(50),
    @Name        NVARCHAR(150),
    @Description NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @code VARCHAR(101) = (SELECT Code FROM dbo.Modules WHERE Id = @ModuleId) + '.' + @Action;
    IF @code IS NULL
        THROW 50404, 'Module not found.', 1;

    BEGIN TRAN;

    IF EXISTS (SELECT 1 FROM dbo.Permissions WITH (UPDLOCK, HOLDLOCK) WHERE Code = @code OR (ModuleId = @ModuleId AND Action = @Action))
        THROW 50409, 'A permission with this code already exists.', 1;

    INSERT dbo.Permissions (ModuleId, Action, Code, Name, Description, IsActive, IsSystem)
    VALUES (@ModuleId, @Action, @code, @Name, @Description, 1, 0);

    DECLARE @newId BIGINT = SCOPE_IDENTITY();

    COMMIT;

    SELECT @newId;
END
GO
