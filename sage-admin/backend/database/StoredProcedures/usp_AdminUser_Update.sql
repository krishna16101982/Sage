-- Updates an admin user's details and group (the username never changes; NULL group = no group, so no permissions).
-- Raises 50404 (not found), 50409 (email taken) or 50400 (new group missing or inactive; the current group may be kept even
-- if it has since been deactivated). A group change replaces the user's groups and bumps PermissionVersion.
CREATE OR ALTER PROCEDURE dbo.usp_AdminUser_Update
    @PublicId      UNIQUEIDENTIFIER,
    @FirstName     NVARCHAR(100),
    @LastName      NVARCHAR(100),
    @Email         NVARCHAR(256),
    @MobileNumber  NVARCHAR(20)     = NULL,
    @GroupPublicId UNIQUEIDENTIFIER = NULL,
    @ActorUserId   BIGINT           = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRAN;

    DECLARE @id BIGINT;
    SELECT @id = Id FROM dbo.AdminUsers WITH (UPDLOCK, HOLDLOCK) WHERE PublicId = @PublicId AND IsDeleted = 0;
    IF @id IS NULL
        THROW 50404, 'Admin user not found', 1;

    IF EXISTS (SELECT 1 FROM dbo.AdminUsers WITH (UPDLOCK, HOLDLOCK) WHERE Email = @Email AND IsDeleted = 0 AND Id <> @id)
        THROW 50409, 'An admin user with this email already exists', 1;

    DECLARE @currentGroupId BIGINT = (SELECT GroupId FROM dbo.AdminUserGroups WITH (UPDLOCK, HOLDLOCK) WHERE UserId = @id AND IsPrimary = 1);
    DECLARE @groupId BIGINT = NULL;
    IF @GroupPublicId IS NOT NULL
    BEGIN
        SELECT @groupId = Id FROM dbo.PermissionGroups WITH (HOLDLOCK)
        WHERE PublicId = @GroupPublicId AND (Status = 'Active' OR Id = @currentGroupId);
        IF @groupId IS NULL
            THROW 50400, 'The group must be an active permission group', 1;
    END

    DECLARE @groupChanged BIT = CASE WHEN ISNULL(@currentGroupId, 0) <> ISNULL(@groupId, 0) THEN 1 ELSE 0 END;
    IF @groupChanged = 1
    BEGIN
        DELETE dbo.AdminUserGroups WHERE UserId = @id;
        IF @groupId IS NOT NULL
            INSERT dbo.AdminUserGroups (UserId, GroupId, IsPrimary, AssignedBy) VALUES (@id, @groupId, 1, @ActorUserId);
    END

    UPDATE dbo.AdminUsers
    SET FirstName = @FirstName, LastName = @LastName, Email = @Email, MobileNumber = @MobileNumber,
        PermissionVersion = PermissionVersion + @groupChanged, UpdatedAtUtc = SYSUTCDATETIME(), UpdatedBy = @ActorUserId
    WHERE Id = @id;

    COMMIT;

    SELECT CAST(1 AS BIGINT);
END
GO
