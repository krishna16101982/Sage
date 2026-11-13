-- User Panel "forgot password": invalidates the sales user's outstanding reset tokens and stores the new one, atomically.
CREATE OR ALTER PROCEDURE dbo.usp_SalesUserPasswordResetToken_Create
    @SalesUserId  BIGINT,
    @TokenHash    CHAR(64),
    @ExpiresAtUtc DATETIME2(3),
    @NowUtc       DATETIME2(3),
    @RequestedIp  VARCHAR(45) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRAN;

    UPDATE dbo.SalesUserPasswordResetTokens SET UsedAtUtc = @NowUtc
    WHERE SalesUserId = @SalesUserId AND UsedAtUtc IS NULL;

    INSERT dbo.SalesUserPasswordResetTokens (SalesUserId, TokenHash, ExpiresAtUtc, RequestedIp)
    VALUES (@SalesUserId, @TokenHash, @ExpiresAtUtc, @RequestedIp);

    DECLARE @newId BIGINT = SCOPE_IDENTITY();

    COMMIT;

    SELECT @newId;
END
GO
