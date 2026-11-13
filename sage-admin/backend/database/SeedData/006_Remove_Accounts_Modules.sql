-- Removes the Accounts modules (Invoices, Payments, Financial Reports) that 001 seeded: Sage has no accounts screens.
-- Deletes their group assignments, permissions and the modules. Users who held one of those permissions get
-- PermissionVersion + 1, so their sessions pick up the change on the next request. Runs once.
SET XACT_ABORT ON;
BEGIN TRAN;

DECLARE @modules TABLE (Id BIGINT PRIMARY KEY);
INSERT @modules (Id)
SELECT Id FROM dbo.Modules
WHERE IsSystem = 0 AND (Category = N'Accounts' OR Code IN ('Invoice', 'Payment', 'FinancialReports'));

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
