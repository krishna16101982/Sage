-- Updates a permission. When IsActive changes, every user holding it gets PermissionVersion + 1.
-- Returns rows affected (0 = not found).
CREATE OR ALTER PROCEDURE dbo.usp_Permission_Update
    @Id          BIGINT,
    @Name        NVARCHAR(150),
    @Description NVARCHAR(500) = NULL,
    @IsActive    BIT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRAN;

    DECLARE @wasActive BIT;
    SELECT @wasActive = IsActive FROM dbo.Permissions WITH (UPDLOCK) WHERE Id = @Id;

    UPDATE dbo.Permissions
    SET Name = @Name, Description = @Description, IsActive = @IsActive, UpdatedAtUtc = SYSUTCDATETIME()
    WHERE Id = @Id;

    DECLARE @rows BIGINT = @@ROWCOUNT;

    IF @rows > 0 AND @wasActive <> @IsActive
        UPDATE dbo.AdminUsers SET PermissionVersion = PermissionVersion + 1
        WHERE Id IN (SELECT ug.UserId
                     FROM dbo.AdminUserGroups ug
                     JOIN dbo.GroupPermissions gp ON gp.GroupId = ug.GroupId
                     WHERE gp.PermissionId = @Id);

    COMMIT;

    SELECT @rows;
END
GO
