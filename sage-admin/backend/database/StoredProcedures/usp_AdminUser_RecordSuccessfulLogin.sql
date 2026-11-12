-- Resets the failure counters after a successful login. @NewPasswordHash is set only when the hash policy was raised.
CREATE OR ALTER PROCEDURE dbo.usp_AdminUser_RecordSuccessfulLogin
    @UserId          BIGINT,
    @NowUtc          DATETIME2(3),
    @NewPasswordHash NVARCHAR(255) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.AdminUsers
    SET FailedLoginCount = 0,
        LockoutEndUtc    = NULL,
        LastLoginAtUtc   = @NowUtc,
        PasswordHash     = COALESCE(@NewPasswordHash, PasswordHash)
    WHERE Id = @UserId;

    SELECT CAST(@@ROWCOUNT AS BIGINT);
END
GO
