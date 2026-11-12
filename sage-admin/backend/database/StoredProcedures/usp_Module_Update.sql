-- Updates a module. When IsActive changes, every user holding one of its permissions gets PermissionVersion + 1
-- so their access is re-evaluated on the next request. Returns rows affected (0 = not found).
CREATE OR ALTER PROCEDURE dbo.usp_Module_Update
    @Id        BIGINT,
    @Name      NVARCHAR(100),
    @Category  NVARCHAR(100) = NULL,
    @SortOrder INT,
    @IsActive  BIT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRAN;

    DECLARE @wasActive BIT;
    SELECT @wasActive = IsActive FROM dbo.Modules WITH (UPDLOCK) WHERE Id = @Id;

    UPDATE dbo.Modules
    SET Name = @Name, Category = @Category, SortOrder = @SortOrder, IsActive = @IsActive, UpdatedAtUtc = SYSUTCDATETIME()
    WHERE Id = @Id;

    DECLARE @rows BIGINT = @@ROWCOUNT;

    IF @rows > 0 AND @wasActive <> @IsActive
        UPDATE dbo.AdminUsers SET PermissionVersion = PermissionVersion + 1
        WHERE Id IN (SELECT ug.UserId
                     FROM dbo.AdminUserGroups ug
                     JOIN dbo.GroupPermissions gp ON gp.GroupId = ug.GroupId
                     JOIN dbo.Permissions p ON p.Id = gp.PermissionId
                     WHERE p.ModuleId = @Id);

    COMMIT;

    SELECT @rows;
END
GO
