-- Sets a new password in one transaction: clears lockout, invalidates outstanding reset tokens, revokes every refresh token
-- and bumps PermissionVersion so existing access tokens stop working immediately. Returns rows affected (0 = not found).
CREATE OR ALTER PROCEDURE dbo.usp_AdminUser_SetPassword
    @UserId       BIGINT,
    @PasswordHash NVARCHAR(255),
    @NowUtc       DATETIME2(3),
    @RevokeReason VARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRAN;

    UPDATE dbo.AdminUsers
    SET PasswordHash         = @PasswordHash,
        PasswordUpdatedAtUtc = @NowUtc,
        MustChangePassword   = 0,
        FailedLoginCount     = 0,
        LockoutEndUtc        = NULL,
        PermissionVersion    = PermissionVersion + 1,
        UpdatedAtUtc         = @NowUtc
    WHERE Id = @UserId;

    DECLARE @rows BIGINT = @@ROWCOUNT;

    UPDATE dbo.PasswordResetTokens SET UsedAtUtc = @NowUtc
    WHERE UserId = @UserId AND UsedAtUtc IS NULL;

    UPDATE dbo.RefreshTokens SET RevokedAtUtc = @NowUtc, RevokedReason = @RevokeReason
    WHERE UserId = @UserId AND RevokedAtUtc IS NULL;

    COMMIT;

    SELECT @rows;
END
GO
