-- Creates an admin user (Active, not Super Admin, must change password at first sign-in) and, when given, makes
-- @GroupPublicId their primary group. Raises 50409 (username/email taken among non-deleted users) or 50400 (group missing
-- or inactive). Returns the new PublicId.
CREATE OR ALTER PROCEDURE dbo.usp_AdminUser_Create
    @FirstName     NVARCHAR(100),
    @LastName      NVARCHAR(100),
    @Username      NVARCHAR(50),
    @Email         NVARCHAR(256),
    @MobileNumber  NVARCHAR(20)     = NULL,
    @PasswordHash  NVARCHAR(255),
    @GroupPublicId UNIQUEIDENTIFIER = NULL,
    @ActorUserId   BIGINT           = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRAN;

    IF EXISTS (SELECT 1 FROM dbo.AdminUsers WITH (UPDLOCK, HOLDLOCK) WHERE Username = @Username AND IsDeleted = 0)
        THROW 50409, 'An admin user with this username already exists', 1;
    IF EXISTS (SELECT 1 FROM dbo.AdminUsers WITH (UPDLOCK, HOLDLOCK) WHERE Email = @Email AND IsDeleted = 0)
        THROW 50409, 'An admin user with this email already exists', 1;

    DECLARE @groupId BIGINT = NULL;
    IF @GroupPublicId IS NOT NULL
    BEGIN
        SELECT @groupId = Id FROM dbo.PermissionGroups WITH (HOLDLOCK) WHERE PublicId = @GroupPublicId AND Status = 'Active';
        IF @groupId IS NULL
            THROW 50400, 'The group must be an active permission group', 1;
    END

    DECLARE @publicId UNIQUEIDENTIFIER = NEWID();

    INSERT dbo.AdminUsers (PublicId, FirstName, LastName, Username, Email, MobileNumber, PasswordHash, MustChangePassword, IsSuperAdmin, Status, CreatedBy)
    VALUES (@publicId, @FirstName, @LastName, @Username, @Email, @MobileNumber, @PasswordHash, 1, 0, 'Active', @ActorUserId);

    IF @groupId IS NOT NULL
        INSERT dbo.AdminUserGroups (UserId, GroupId, IsPrimary, AssignedBy) VALUES (SCOPE_IDENTITY(), @groupId, 1, @ActorUserId);

    COMMIT;

    SELECT @publicId;
END
GO
