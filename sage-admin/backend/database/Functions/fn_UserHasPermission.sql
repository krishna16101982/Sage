-- Returns 1 when the user is an active Super Admin or holds the permission through an active group.
CREATE OR ALTER FUNCTION dbo.fn_UserHasPermission (@UserId BIGINT, @PermissionCode VARCHAR(101))
RETURNS BIT
AS
BEGIN
    DECLARE @result BIT = 0;
    IF EXISTS (SELECT 1 FROM dbo.AdminUsers WHERE Id = @UserId AND IsSuperAdmin = 1 AND IsDeleted = 0 AND Status = 'Active')
        SET @result = 1;
    ELSE IF EXISTS (SELECT 1 FROM dbo.vw_AdminUserEffectivePermissions WHERE UserId = @UserId AND PermissionCode = @PermissionCode)
        SET @result = 1;
    RETURN @result;
END
GO
