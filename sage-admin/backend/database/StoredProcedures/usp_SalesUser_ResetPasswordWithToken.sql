-- User Panel "reset password" from the emailed link. In one transaction: checks the token (unused, not expired, active
-- user), uses it up, sets the new password, clears MustChangePassword and the lockout, ends every User Panel session and
-- bumps PermissionVersion. The token row is locked, so the same link cannot be used twice at the same moment.
-- Returns the sales user's Id, or 0 when the link is not valid (the caller shows one "invalid or expired" message).
CREATE OR ALTER PROCEDURE dbo.usp_SalesUser_ResetPasswordWithToken
    @TokenHash    CHAR(64),
    @PasswordHash NVARCHAR(255),
    @NowUtc       DATETIME2(3)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRAN;

    DECLARE @tokenId BIGINT, @userId BIGINT;
    SELECT @tokenId = t.Id, @userId = t.SalesUserId
    FROM dbo.SalesUserPasswordResetTokens t WITH (UPDLOCK, HOLDLOCK)
    JOIN dbo.SalesUsers u ON u.Id = t.SalesUserId
    WHERE t.TokenHash = @TokenHash AND t.UsedAtUtc IS NULL AND t.ExpiresAtUtc > @NowUtc
      AND u.IsDeleted = 0 AND u.Status = 'Active';

    IF @tokenId IS NULL
    BEGIN
        COMMIT;
        SELECT CAST(0 AS BIGINT);
        RETURN;
    END

    UPDATE dbo.SalesUserPasswordResetTokens SET UsedAtUtc = @NowUtc
    WHERE SalesUserId = @userId AND UsedAtUtc IS NULL;

    UPDATE dbo.SalesUsers
    SET PasswordHash = @PasswordHash, PasswordUpdatedAtUtc = @NowUtc, MustChangePassword = 0,
        FailedLoginCount = 0, LockoutEndUtc = NULL, PermissionVersion = PermissionVersion + 1
    WHERE Id = @userId;

    UPDATE dbo.SalesUserRefreshTokens SET RevokedAtUtc = @NowUtc, RevokedReason = 'password_reset'
    WHERE SalesUserId = @userId AND RevokedAtUtc IS NULL;

    COMMIT;

    SELECT @userId;
END
GO
