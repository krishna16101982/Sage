-- Initial reference data. Idempotent (MERGE on codes). Further modules/permissions are managed through the Admin UI.
-- Admin modules (system): permission management itself.
DECLARE @modules TABLE (Code VARCHAR(50), Name NVARCHAR(100), Category NVARCHAR(100), SortOrder INT, IsSystem BIT);
INSERT @modules VALUES
 ('AdminUser',       N'Admin Users',            N'Administration', 10, 1),
 ('Group',           N'Permission Groups',      N'Administration', 20, 1),
 ('Permission',      N'Permissions',            N'Administration', 30, 1),
 ('AuditLog',        N'Audit Logs',             N'Administration', 40, 1),
 ('Setting',         N'Settings',               N'Administration', 50, 1),
 ('Dashboard',       N'Dashboard',              N'Administration',  5, 1),
 ('Employee',        N'Employee Management',    N'HR',            100, 0),
 ('Attendance',      N'Attendance Management',  N'HR',            110, 0),
 ('Payroll',         N'Payroll Management',     N'HR',            120, 0),
 ('Customer',        N'Customer Management',    N'Sales',         200, 0),
 ('Sales',           N'Sales',                  N'Sales',         210, 0),
 ('Invoice',         N'Invoices',               N'Accounts',      300, 0),
 ('Payment',         N'Payments',               N'Accounts',      310, 0),
 ('FinancialReports',N'Financial Reports',      N'Accounts',      320, 0);

MERGE dbo.Modules AS t USING @modules AS s ON t.Code = s.Code
WHEN NOT MATCHED THEN INSERT (Code, Name, Category, SortOrder, IsSystem) VALUES (s.Code, s.Name, s.Category, s.SortOrder, s.IsSystem);

DECLARE @perms TABLE (ModuleCode VARCHAR(50), Action VARCHAR(50), IsSystem BIT);
INSERT @perms VALUES
 ('Dashboard','View',1),
 ('AdminUser','View',1),('AdminUser','Add',1),('AdminUser','Edit',1),('AdminUser','ChangeStatus',1),('AdminUser','ResetPassword',1),
 ('Group','View',1),('Group','Add',1),('Group','Edit',1),('Group','Delete',1),('Group','AssignPermissions',1),
 ('Permission','View',1),('Permission','Add',1),('Permission','Edit',1),
 ('AuditLog','View',1),
 ('Setting','View',1),('Setting','Edit',1),
 ('Employee','View',0),('Employee','Add',0),('Employee','Edit',0),('Employee','Delete',0),('Employee','Export',0),
 ('Attendance','View',0),('Attendance','Add',0),('Attendance','Edit',0),('Attendance','Delete',0),
 ('Payroll','View',0),('Payroll','Add',0),('Payroll','Edit',0),('Payroll','Delete',0),
 ('Customer','View',0),('Customer','Add',0),('Customer','Edit',0),('Customer','Delete',0),
 ('Sales','View',0),('Sales','Add',0),('Sales','Edit',0),('Sales','Delete',0),
 ('Invoice','View',0),('Invoice','Add',0),('Invoice','Edit',0),('Invoice','Delete',0),
 ('Payment','View',0),('Payment','Add',0),('Payment','Edit',0),
 ('FinancialReports','View',0),('FinancialReports','Export',0);

MERGE dbo.Permissions AS t
USING (SELECT m.Id AS ModuleId, p.Action, CONCAT(m.Code, '.', p.Action) AS Code, CONCAT(m.Name, N' - ', p.Action) AS Name, p.IsSystem
       FROM @perms p JOIN dbo.Modules m ON m.Code = p.ModuleCode) AS s
ON t.Code = s.Code
WHEN NOT MATCHED THEN INSERT (ModuleId, Action, Code, Name, IsSystem) VALUES (s.ModuleId, s.Action, s.Code, s.Name, s.IsSystem);
GO
