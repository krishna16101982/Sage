-- Activates or deactivates an admin user. Activating also clears a lockout; deactivating revokes all their sessions.
-- Any change bumps PermissionVersion so access tokens stop working on the next request. Raises 50404 when not found.
CREATE OR ALTER PROCEDURE dbo.usp_AdminUser_SetStatus
    @PublicId    UNIQUEIDENTIFIER,
    @Status      VARCHAR(20),
    @ActorUserId BIGINT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRAN;

    DECLARE @id BIGINT;
    SELECT @id = Id FROM dbo.AdminUsers WITH (UPDLOCK, HOLDLOCK) WHERE PublicId = @PublicId AND IsDeleted = 0;
    IF @id IS NULL
        THROW 50404, 'Admin user not found', 1;

    UPDATE dbo.AdminUsers
    SET Status = @Status,
        FailedLoginCount = CASE WHEN @Status = 'Active' THEN 0 ELSE FailedLoginCount END,
        LockoutEndUtc = CASE WHEN @Status = 'Active' THEN NULL ELSE LockoutEndUtc END,
        PermissionVersion = PermissionVersion + 1,
        UpdatedAtUtc = SYSUTCDATETIME(), UpdatedBy = @ActorUserId
    WHERE Id = @id;

    IF @Status <> 'Active'
        UPDATE dbo.RefreshTokens SET RevokedAtUtc = SYSUTCDATETIME(), RevokedReason = 'user_inactive'
        WHERE UserId = @id AND RevokedAtUtc IS NULL;

    COMMIT;

    SELECT CAST(1 AS BIGINT);
END
GO
