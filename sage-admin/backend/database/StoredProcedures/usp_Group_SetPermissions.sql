-- Replaces a group's permission set with @PermissionIdsJson (a JSON array of permission ids, e.g. [1,5,9]).
-- Rules: every id must exist; a permission that is inactive (or whose module is inactive) may be kept if the group already
-- holds it, but cannot be newly granted. Raises 50404 (group not found) or 50400 (invalid ids).
-- When the set changes, every member gets PermissionVersion + 1. Returns the number of permissions the group now holds.
CREATE OR ALTER PROCEDURE dbo.usp_Group_SetPermissions
    @PublicId          UNIQUEIDENTIFIER,
    @PermissionIdsJson NVARCHAR(MAX),
    @ActorUserId       BIGINT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF ISJSON(@PermissionIdsJson) = 0 OR LEFT(LTRIM(@PermissionIdsJson), 1) <> '['
        THROW 50400, 'Permission list must be a JSON array of ids', 1;

    DECLARE @requested TABLE (RawValue NVARCHAR(4000), Id BIGINT);
    INSERT @requested (RawValue, Id)
    SELECT [value], TRY_CAST([value] AS BIGINT) FROM OPENJSON(@PermissionIdsJson);

    IF EXISTS (SELECT 1 FROM @requested WHERE Id IS NULL)
        THROW 50400, 'Permission list must contain numeric ids only', 1;

    DECLARE @ids TABLE (Id BIGINT PRIMARY KEY);
    INSERT @ids (Id) SELECT DISTINCT Id FROM @requested;

    BEGIN TRAN;

    DECLARE @groupId BIGINT;
    SELECT @groupId = Id FROM dbo.PermissionGroups WITH (UPDLOCK, HOLDLOCK) WHERE PublicId = @PublicId;
    IF @groupId IS NULL
        THROW 50404, 'Group not found', 1;

    IF EXISTS (SELECT 1 FROM @ids i WHERE NOT EXISTS (SELECT 1 FROM dbo.Permissions p WHERE p.Id = i.Id))
        THROW 50400, 'One or more permissions do not exist', 1;

    IF EXISTS (SELECT 1
               FROM @ids i
               JOIN dbo.Permissions p ON p.Id = i.Id
               JOIN dbo.Modules m ON m.Id = p.ModuleId
               WHERE (p.IsActive = 0 OR m.IsActive = 0)
                 AND NOT EXISTS (SELECT 1 FROM dbo.GroupPermissions gp WHERE gp.GroupId = @groupId AND gp.PermissionId = i.Id))
        THROW 50400, 'Inactive permissions cannot be granted', 1;

    DELETE gp FROM dbo.GroupPermissions gp
    WHERE gp.GroupId = @groupId AND NOT EXISTS (SELECT 1 FROM @ids i WHERE i.Id = gp.PermissionId);
    DECLARE @changes INT = @@ROWCOUNT;

    INSERT dbo.GroupPermissions (GroupId, PermissionId, GrantedBy)
    SELECT @groupId, i.Id, @ActorUserId
    FROM @ids i
    WHERE NOT EXISTS (SELECT 1 FROM dbo.GroupPermissions gp WHERE gp.GroupId = @groupId AND gp.PermissionId = i.Id);
    SET @changes += @@ROWCOUNT;

    IF @changes > 0
    BEGIN
        UPDATE dbo.PermissionGroups SET UpdatedAtUtc = SYSUTCDATETIME(), UpdatedBy = @ActorUserId WHERE Id = @groupId;
        UPDATE dbo.AdminUsers SET PermissionVersion = PermissionVersion + 1
        WHERE Id IN (SELECT UserId FROM dbo.AdminUserGroups WHERE GroupId = @groupId);
    END

    COMMIT;

    SELECT CAST(COUNT(*) AS BIGINT) FROM dbo.GroupPermissions WHERE GroupId = @groupId;
END
GO
