-- Activates or deactivates a sales user. Activating also clears a lockout. Any change bumps PermissionVersion,
-- so a deactivated user's sessions stop working on their next request. Raises 50404 when not found.
CREATE OR ALTER PROCEDURE dbo.usp_SalesUser_SetStatus
    @PublicId    UNIQUEIDENTIFIER,
    @Status      VARCHAR(20),
    @ActorUserId BIGINT = NULL
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
    SET Status = @Status,
        FailedLoginCount = CASE WHEN @Status = 'Active' THEN 0 ELSE FailedLoginCount END,
        LockoutEndUtc = CASE WHEN @Status = 'Active' THEN NULL ELSE LockoutEndUtc END,
        PermissionVersion = PermissionVersion + 1,
        UpdatedAtUtc = SYSUTCDATETIME(), UpdatedBy = @ActorUserId
    WHERE Id = @id;

    -- Deactivation ends every User Panel session.
    IF @Status <> 'Active'
        UPDATE dbo.SalesUserRefreshTokens SET RevokedAtUtc = SYSUTCDATETIME(), RevokedReason = 'user_inactive'
        WHERE SalesUserId = @id AND RevokedAtUtc IS NULL;

    COMMIT;

    SELECT CAST(1 AS BIGINT);
END
GO
