-- Permissions and modules for /auth/me. A Super Admin gets every active permission; others get what their active groups grant.
CREATE OR ALTER PROCEDURE dbo.usp_AdminUser_GetEffectivePermissions
    @UserId BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM dbo.AdminUsers WHERE Id = @UserId AND IsSuperAdmin = 1 AND IsDeleted = 0)
        SELECT p.Code AS PermissionCode, m.Code AS ModuleCode, m.Name AS ModuleName
        FROM dbo.Permissions p
        JOIN dbo.Modules m ON m.Id = p.ModuleId
        WHERE p.IsActive = 1 AND m.IsActive = 1
        ORDER BY m.SortOrder, p.Code;
    ELSE
        SELECT v.PermissionCode, v.ModuleCode, m.Name AS ModuleName
        FROM dbo.vw_AdminUserEffectivePermissions v
        JOIN dbo.Modules m ON m.Id = v.ModuleId
        WHERE v.UserId = @UserId
        ORDER BY m.SortOrder, v.PermissionCode;
END
GO
