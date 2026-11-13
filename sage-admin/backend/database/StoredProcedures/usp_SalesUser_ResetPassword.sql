-- An admin sets a temporary password: the user must change it at next sign-in. Clears any lockout and bumps
-- PermissionVersion so existing sessions stop working. Raises 50404 when not found.
CREATE OR ALTER PROCEDURE dbo.usp_SalesUser_ResetPassword
    @PublicId     UNIQUEIDENTIFIER,
    @PasswordHash NVARCHAR(255),
    @ActorUserId  BIGINT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRAN;

    DECLARE @id BIGINT;
    SELECT @id = Id FROM dbo.SalesUsers WITH (UPDLOCK, HOLDLOCK) WHERE PublicId = @PublicId AND IsDeleted = 0;
    IF @id IS NULL
        THROW 50404, 'Sales user not found', 1;

    UPDATE dbo.SalesUsers
    SET PasswordHash = @PasswordHash, PasswordUpdatedAtUtc = SYSUTCDATETIME(), MustChangePassword = 1,
        FailedLoginCount = 0, LockoutEndUtc = NULL, PermissionVersion = PermissionVersion + 1,
        UpdatedAtUtc = SYSUTCDATETIME(), UpdatedBy = @ActorUserId
    WHERE Id = @id;

    -- The old password's sessions end.
    UPDATE dbo.SalesUserRefreshTokens SET RevokedAtUtc = SYSUTCDATETIME(), RevokedReason = 'password_reset_by_admin'
    WHERE SalesUserId = @id AND RevokedAtUtc IS NULL;

    -- An emailed reset link stops working once an admin has set a password.
    UPDATE dbo.SalesUserPasswordResetTokens SET UsedAtUtc = SYSUTCDATETIME()
    WHERE SalesUserId = @id AND UsedAtUtc IS NULL;

    COMMIT;

    SELECT CAST(1 AS BIGINT);
END
GO
