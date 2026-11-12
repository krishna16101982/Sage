-- Admin permission module for managing User Panel accounts (sales persons / sales managers). Idempotent.
MERGE dbo.Modules AS t
USING (SELECT 'SalesUser' AS Code, N'Sales Users' AS Name, N'Sales Administration' AS Category, 70 AS SortOrder) AS s
ON t.Code = s.Code
WHEN NOT MATCHED THEN INSERT (Code, Name, Category, SortOrder, IsSystem) VALUES (s.Code, s.Name, s.Category, s.SortOrder, 1);

MERGE dbo.Permissions AS t
USING (SELECT m.Id AS ModuleId, a.Action, CONCAT(m.Code, '.', a.Action) AS Code, CONCAT(m.Name, N' - ', a.Action) AS Name
       FROM (VALUES ('View'), ('Add'), ('Edit'), ('ChangeStatus'), ('ResetPassword')) AS a(Action)
       JOIN dbo.Modules m ON m.Code = 'SalesUser') AS s
ON t.Code = s.Code
WHEN NOT MATCHED THEN INSERT (ModuleId, Action, Code, Name, IsSystem) VALUES (s.ModuleId, s.Action, s.Code, s.Name, 1);
GO
