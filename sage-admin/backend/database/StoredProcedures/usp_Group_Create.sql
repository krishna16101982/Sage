-- Creates a permission group (Active, no permissions). Raises 50409 when the code or the name is already taken
-- (the checks and the insert hold one lock, so no race). Returns the new group's PublicId.
CREATE OR ALTER PROCEDURE dbo.usp_Group_Create
    @Code        VARCHAR(50),
    @Name        NVARCHAR(100),
    @Description NVARCHAR(500) = NULL,
    @ActorUserId BIGINT        = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRAN;

    IF EXISTS (SELECT 1 FROM dbo.PermissionGroups WITH (UPDLOCK, HOLDLOCK) WHERE Code = @Code)
        THROW 50409, 'A group with this code already exists', 1;
    IF EXISTS (SELECT 1 FROM dbo.PermissionGroups WITH (UPDLOCK, HOLDLOCK) WHERE Name = @Name)
        THROW 50409, 'A group with this name already exists', 1;

    DECLARE @publicId UNIQUEIDENTIFIER = NEWID();

    INSERT dbo.PermissionGroups (PublicId, Code, Name, Description, Status, IsSystem, CreatedBy)
    VALUES (@publicId, @Code, @Name, @Description, 'Active', 0, @ActorUserId);

    COMMIT;

    SELECT @publicId;
END
GO
