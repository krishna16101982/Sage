-- Deletes a group and its permission grants. Raises 50404 when it does not exist and 50409 while any user
-- (including soft-deleted users, whose history still points at it) is assigned to it. The membership check holds a
-- range lock, so a user cannot be assigned while the delete runs.
CREATE OR ALTER PROCEDURE dbo.usp_Group_Delete
    @PublicId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRAN;

    DECLARE @id BIGINT;
    SELECT @id = Id FROM dbo.PermissionGroups WITH (UPDLOCK, HOLDLOCK) WHERE PublicId = @PublicId;

    IF @id IS NULL
        THROW 50404, 'Group not found', 1;
    IF EXISTS (SELECT 1 FROM dbo.AdminUserGroups WITH (UPDLOCK, HOLDLOCK) WHERE GroupId = @id)
        THROW 50409, 'This group has users assigned. Move them to another group before deleting it', 1;

    DELETE dbo.GroupPermissions WHERE GroupId = @id;
    DELETE dbo.PermissionGroups WHERE Id = @id;

    COMMIT;

    SELECT CAST(1 AS BIGINT);
END
GO
