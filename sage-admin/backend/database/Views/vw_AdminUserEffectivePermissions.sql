-- Effective permissions per user: user -> group -> permission -> module (active rows only).
-- Super Admins are handled in application code (they bypass permission lookup).
CREATE OR ALTER VIEW dbo.vw_AdminUserEffectivePermissions
AS
SELECT DISTINCT
    u.Id        AS UserId,
    p.Id        AS PermissionId,
    p.Code      AS PermissionCode,
    m.Id        AS ModuleId,
    m.Code      AS ModuleCode
FROM dbo.AdminUsers u
JOIN dbo.AdminUserGroups ug     ON ug.UserId = u.Id
JOIN dbo.PermissionGroups g     ON g.Id = ug.GroupId AND g.Status = 'Active'
JOIN dbo.GroupPermissions gp    ON gp.GroupId = g.Id
JOIN dbo.Permissions p          ON p.Id = gp.PermissionId AND p.IsActive = 1
JOIN dbo.Modules m              ON m.Id = p.ModuleId AND m.IsActive = 1
WHERE u.IsDeleted = 0 AND u.Status = 'Active';
GO
