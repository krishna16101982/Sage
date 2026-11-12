-- Counts a failed password attempt. At the limit it starts the lockout and resets the counter (one statement, no race).
CREATE OR ALTER PROCEDURE dbo.usp_AdminUser_RecordFailedLogin
    @UserId            BIGINT,
    @MaxFailedAttempts INT,
    @LockoutEndUtc     DATETIME2(3)
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.AdminUsers
    SET LockoutEndUtc    = CASE WHEN FailedLoginCount + 1 >= @MaxFailedAttempts THEN @LockoutEndUtc ELSE LockoutEndUtc END,
        FailedLoginCount = CASE WHEN FailedLoginCount + 1 >= @MaxFailedAttempts THEN 0 ELSE FailedLoginCount + 1 END
    WHERE Id = @UserId;

    SELECT CAST(@@ROWCOUNT AS BIGINT);
END
GO
