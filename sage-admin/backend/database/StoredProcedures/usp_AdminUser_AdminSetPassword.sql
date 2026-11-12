-- An admin sets a temporary password for another admin user: they must change it at next sign-in. Clears any lockout,
-- revokes their sessions and unused reset links, and bumps PermissionVersion. Raises 50404 when not found.
-- (dbo.usp_AdminUser_SetPassword is the user's own change/reset, which clears MustChangePassword instead.)
CREATE OR ALTER PROCEDURE dbo.usp_AdminUser_AdminSetPassword
    @PublicId     UNIQUEIDENTIFIER,
    @PasswordHash NVARCHAR(255),
    @ActorUserId  BIGINT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRAN;

    DECLARE @id BIGINT;
    SELECT @id = Id FROM dbo.AdminUsers WITH (UPDLOCK, HOLDLOCK) WHERE PublicId = @PublicId AND IsDeleted = 0;
    IF @id IS NULL
        THROW 50404, 'Admin user not found', 1;

    DECLARE @now DATETIME2(3) = SYSUTCDATETIME();

    UPDATE dbo.AdminUsers
    SET PasswordHash = @PasswordHash, PasswordUpdatedAtUtc = @now, MustChangePassword = 1,
        FailedLoginCount = 0, LockoutEndUtc = NULL, PermissionVersion = PermissionVersion + 1,
        UpdatedAtUtc = @now, UpdatedBy = @ActorUserId
    WHERE Id = @id;

    UPDATE dbo.RefreshTokens SET RevokedAtUtc = @now, RevokedReason = 'password_reset_by_admin'
    WHERE UserId = @id AND RevokedAtUtc IS NULL;
    UPDATE dbo.PasswordResetTokens SET UsedAtUtc = @now WHERE UserId = @id AND UsedAtUtc IS NULL;

    COMMIT;

    SELECT CAST(1 AS BIGINT);
END
GO
