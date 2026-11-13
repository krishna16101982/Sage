-- Removes the HR modules (Employee, Attendance, Payroll) that 001 seeded: Sage has no HR screens.
-- Deletes their group assignments, permissions and the modules themselves. Users who held one of those permissions
-- get PermissionVersion + 1, so their sessions pick up the change on the next request. Runs once.
SET XACT_ABORT ON;
BEGIN TRAN;

DECLARE @modules TABLE (Id BIGINT PRIMARY KEY);
INSERT @modules (Id)
SELECT Id FROM dbo.Modules
WHERE IsSystem = 0 AND (Category = N'HR' OR Code IN ('Employee', 'Attendance', 'Payroll'));

UPDATE dbo.AdminUsers SET PermissionVersion = PermissionVersion + 1
WHERE Id IN (SELECT ug.UserId
             FROM dbo.AdminUserGroups ug
             JOIN dbo.GroupPermissions gp ON gp.GroupId = ug.GroupId
             JOIN dbo.Permissions p ON p.Id = gp.PermissionId
             WHERE p.ModuleId IN (SELECT Id FROM @modules));

DELETE gp FROM dbo.GroupPermissions gp JOIN dbo.Permissions p ON p.Id = gp.PermissionId WHERE p.ModuleId IN (SELECT Id FROM @modules);
DELETE FROM dbo.Permissions WHERE ModuleId IN (SELECT Id FROM @modules);
DELETE FROM dbo.Modules WHERE Id IN (SELECT Id FROM @modules);

COMMIT;
GO
