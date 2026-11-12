-- Rotates a User Panel refresh token: inserts the new token in the same family and revokes the old one, atomically.
-- Raises 50001 when the old token was already used (a concurrent refresh won the race); nothing is written then.
CREATE OR ALTER PROCEDURE dbo.usp_SalesUserRefreshToken_Rotate
    @OldTokenId   BIGINT,
    @SalesUserId  BIGINT,
    @TokenHash    CHAR(64),
    @FamilyId     UNIQUEIDENTIFIER,
    @ExpiresAtUtc DATETIME2(3),
    @NowUtc       DATETIME2(3),
    @CreatedByIp  VARCHAR(45)   = NULL,
    @UserAgent    NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRAN;

    INSERT dbo.SalesUserRefreshTokens (SalesUserId, TokenHash, FamilyId, ExpiresAtUtc, CreatedByIp, UserAgent)
    VALUES (@SalesUserId, @TokenHash, @FamilyId, @ExpiresAtUtc, @CreatedByIp, @UserAgent);

    DECLARE @newId BIGINT = SCOPE_IDENTITY();

    UPDATE dbo.SalesUserRefreshTokens
    SET RevokedAtUtc = @NowUtc, RevokedReason = 'rotated', ReplacedByTokenId = @newId
    WHERE Id = @OldTokenId AND RevokedAtUtc IS NULL;

    IF @@ROWCOUNT = 0
        THROW 50001, 'Refresh token already used.', 1;

    COMMIT;

    SELECT @newId;
END
GO
