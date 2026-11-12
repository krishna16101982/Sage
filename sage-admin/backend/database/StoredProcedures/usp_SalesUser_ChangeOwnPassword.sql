-- A sales user changes their own password (User Panel): clears MustChangePassword and the lockout, revokes every
-- User Panel session and bumps PermissionVersion so current access tokens stop working. Returns rows affected.
CREATE OR ALTER PROCEDURE dbo.usp_SalesUser_ChangeOwnPassword
    @SalesUserId  BIGINT,
    @PasswordHash NVARCHAR(255),
    @NowUtc       DATETIME2(3)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRAN;

    UPDATE dbo.SalesUsers
    SET PasswordHash = @PasswordHash, PasswordUpdatedAtUtc = @NowUtc, MustChangePassword = 0,
        FailedLoginCount = 0, LockoutEndUtc = NULL, PermissionVersion = PermissionVersion + 1
    WHERE Id = @SalesUserId AND IsDeleted = 0;

    DECLARE @rows BIGINT = @@ROWCOUNT;

    UPDATE dbo.SalesUserRefreshTokens SET RevokedAtUtc = @NowUtc, RevokedReason = 'password_changed'
    WHERE SalesUserId = @SalesUserId AND RevokedAtUtc IS NULL;

    COMMIT;

    SELECT @rows;
END
GO
