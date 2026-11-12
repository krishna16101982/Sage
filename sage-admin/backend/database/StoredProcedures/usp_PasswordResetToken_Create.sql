-- Issues a password reset token: invalidates the user's outstanding tokens and inserts the new one, atomically.
CREATE OR ALTER PROCEDURE dbo.usp_PasswordResetToken_Create
    @UserId       BIGINT,
    @TokenHash    CHAR(64),
    @ExpiresAtUtc DATETIME2(3),
    @NowUtc       DATETIME2(3),
    @RequestedIp  VARCHAR(45) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRAN;

    UPDATE dbo.PasswordResetTokens SET UsedAtUtc = @NowUtc
    WHERE UserId = @UserId AND UsedAtUtc IS NULL;

    INSERT dbo.PasswordResetTokens (UserId, TokenHash, ExpiresAtUtc, RequestedIp)
    VALUES (@UserId, @TokenHash, @ExpiresAtUtc, @RequestedIp);

    DECLARE @newId BIGINT = SCOPE_IDENTITY();

    COMMIT;

    SELECT @newId;
END
GO
