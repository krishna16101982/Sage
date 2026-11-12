-- User Panel sign-in outcome for a known sales user, in one call:
--   @IsSuccess = 0: count the failure; at @MaxFailedAttempts start the lockout (until @LockoutEndUtc) and reset the counter.
--   @IsSuccess = 1: reset the counters, stamp LastLoginAtUtc, and store @NewPasswordHash when the hash policy was raised.
CREATE OR ALTER PROCEDURE dbo.usp_SalesUser_RecordLogin
    @SalesUserId       BIGINT,
    @IsSuccess         BIT,
    @NowUtc            DATETIME2(3),
    @MaxFailedAttempts INT           = 5,
    @LockoutEndUtc     DATETIME2(3)  = NULL,
    @NewPasswordHash   NVARCHAR(255) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @IsSuccess = 1
        UPDATE dbo.SalesUsers
        SET FailedLoginCount = 0, LockoutEndUtc = NULL, LastLoginAtUtc = @NowUtc,
            PasswordHash = COALESCE(@NewPasswordHash, PasswordHash)
        WHERE Id = @SalesUserId;
    ELSE
        UPDATE dbo.SalesUsers
        SET LockoutEndUtc    = CASE WHEN FailedLoginCount + 1 >= @MaxFailedAttempts THEN @LockoutEndUtc ELSE LockoutEndUtc END,
            FailedLoginCount = CASE WHEN FailedLoginCount + 1 >= @MaxFailedAttempts THEN 0 ELSE FailedLoginCount + 1 END
        WHERE Id = @SalesUserId;

    SELECT CAST(@@ROWCOUNT AS BIGINT);
END
GO
