-- A sales manager allows (or stops) one of their team members asking for sales to be cancelled (User Panel > My team).
-- Raises 50404 when @MemberPublicId is not in @ManagerId's team. Bumps the member's PermissionVersion so their session
-- picks up the change on the next request. Returns rows affected (0 when nothing changed).
CREATE OR ALTER PROCEDURE dbo.usp_SalesUser_SetCancelPermission
    @ManagerId               BIGINT,
    @MemberPublicId          UNIQUEIDENTIFIER,
    @CanRequestCancellation  BIT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRAN;

    DECLARE @memberId BIGINT, @current BIT;
    SELECT @memberId = Id, @current = CanRequestCancellation
    FROM dbo.SalesUsers WITH (UPDLOCK, HOLDLOCK)
    WHERE PublicId = @MemberPublicId AND ManagerId = @ManagerId AND IsDeleted = 0;

    IF @memberId IS NULL
        THROW 50404, 'This person is not in your team', 1;

    DECLARE @rows INT = 0;
    IF @current <> @CanRequestCancellation
    BEGIN
        UPDATE dbo.SalesUsers
        SET CanRequestCancellation = @CanRequestCancellation, PermissionVersion = PermissionVersion + 1, UpdatedAtUtc = SYSUTCDATETIME()
        WHERE Id = @memberId;
        SET @rows = @@ROWCOUNT;
    END

    COMMIT;

    SELECT @rows;
END
GO
