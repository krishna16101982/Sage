-- Product catalog: the admin permission module that manages it, and the initial categories and products.
-- Idempotent (MERGE on codes). Plans and prices are entered through the Admin Panel.
MERGE dbo.Modules AS t
USING (SELECT 'Product' AS Code, N'Product Catalog' AS Name, N'Sales Administration' AS Category, 60 AS SortOrder) AS s
ON t.Code = s.Code
WHEN NOT MATCHED THEN INSERT (Code, Name, Category, SortOrder, IsSystem) VALUES (s.Code, s.Name, s.Category, s.SortOrder, 1);

MERGE dbo.Permissions AS t
USING (SELECT m.Id AS ModuleId, a.Action, CONCAT(m.Code, '.', a.Action) AS Code, CONCAT(m.Name, N' - ', a.Action) AS Name
       FROM (VALUES ('View'), ('Add'), ('Edit')) AS a(Action)
       JOIN dbo.Modules m ON m.Code = 'Product') AS s
ON t.Code = s.Code
WHEN NOT MATCHED THEN INSERT (ModuleId, Action, Code, Name, IsSystem) VALUES (s.ModuleId, s.Action, s.Code, s.Name, 1);

MERGE dbo.ProductCategories AS t
USING (VALUES ('SaaS', N'SaaS', N'Software as a Service subscriptions', 10),
              ('CyberSecurity', N'Cyber Security', N'Cyber security products and services', 20)) AS s(Code, Name, Description, SortOrder)
ON t.Code = s.Code
WHEN NOT MATCHED THEN INSERT (Code, Name, Description, SortOrder) VALUES (s.Code, s.Name, s.Description, s.SortOrder);

MERGE dbo.Products AS t
USING (SELECT c.Id AS CategoryId, p.Code, p.Name, p.SortOrder
       FROM (VALUES ('SHTMS', N'SHTMS', 10),
                    ('GreytHR', N'GreytHR', 20),
                    ('LinkedIn', N'LinkedIn', 30),
                    ('Freshdesk', N'Freshdesk', 40)) AS p(Code, Name, SortOrder)
       JOIN dbo.ProductCategories c ON c.Code = 'SaaS') AS s
ON t.Code = s.Code
WHEN NOT MATCHED THEN INSERT (CategoryId, Code, Name, SortOrder) VALUES (s.CategoryId, s.Code, s.Name, s.SortOrder);
GO
