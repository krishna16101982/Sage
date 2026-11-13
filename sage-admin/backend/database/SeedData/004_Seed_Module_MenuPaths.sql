-- Menu links for the modules whose Admin Panel screens exist. Runs once; afterwards links are edited on the
-- Permissions > Modules page. Only fills empty links, so it never overwrites a link an admin has set.
UPDATE m SET MenuPath = s.MenuPath
FROM dbo.Modules m
JOIN (VALUES ('Dashboard',  N'/dashboard'),
             ('AdminUser',  N'/users'),
             ('Group',      N'/groups'),
             ('Permission', N'/permissions'),
             ('AuditLog',   N'/audit-logs'),
             ('Setting',    N'/settings'),
             ('Product',    N'/products'),
             ('SalesUser',  N'/sales-users')) AS s(Code, MenuPath) ON s.Code = m.Code
WHERE m.MenuPath IS NULL;
GO
