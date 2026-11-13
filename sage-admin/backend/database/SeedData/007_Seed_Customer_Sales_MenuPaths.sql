-- The Admin Panel now has Customers and Sales screens: link their modules into the sidebar (only empty links are filled).
-- Customer.View/Add/Edit control the Customers screen; Sales.View controls the sales list and summary.
UPDATE m SET MenuPath = s.MenuPath, Name = s.Name
FROM dbo.Modules m
JOIN (VALUES ('Customer', N'/customers', N'Customers'),
             ('Sales',    N'/sales',     N'Sales')) AS s(Code, MenuPath, Name) ON s.Code = m.Code
WHERE m.MenuPath IS NULL;
GO
