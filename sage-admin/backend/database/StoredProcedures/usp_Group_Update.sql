-- Updates a group's name, description and status (the code never changes).
-- Raises 50404 when the group does not exist and 50409 when another group already has the name.
-- When the status changes, every member gets PermissionVersion + 1 so their access is re-evaluated on the next request.
CREATE OR ALTER PROCEDURE dbo.usp_Group_Update
    @PublicId    UNIQUEIDENTIFIER,
    @Name        NVARCHAR(100),
    @Description NVARCHAR(500) = NULL,
    @Status      VARCHAR(20),
    @ActorUserId BIGINT        = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRAN;

    DECLARE @id BIGINT, @oldStatus VARCHAR(20);
    SELECT @id = Id, @oldStatus = Status FROM dbo.PermissionGroups WITH (UPDLOCK, HOLDLOCK) WHERE PublicId = @PublicId;

    IF @id IS NULL
        THROW 50404, 'Group not found', 1;
    IF EXISTS (SELECT 1 FROM dbo.PermissionGroups WITH (UPDLOCK, HOLDLOCK) WHERE Name = @Name AND Id <> @id)
        THROW 50409, 'A group with this name already exists', 1;

    UPDATE dbo.PermissionGroups
    SET Name = @Name, Description = @Description, Status = @Status, UpdatedAtUtc = SYSUTCDATETIME(), UpdatedBy = @ActorUserId
    WHERE Id = @id;

    IF @oldStatus <> @Status
        UPDATE dbo.AdminUsers SET PermissionVersion = PermissionVersion + 1
        WHERE Id IN (SELECT UserId FROM dbo.AdminUserGroups WHERE GroupId = @id);

    COMMIT;

    SELECT CAST(1 AS BIGINT);
END
GO
