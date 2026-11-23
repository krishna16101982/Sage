-- 014: customers are called companies on every screen. The Admin Panel's menu item comes from dbo.Modules (code stays
-- 'Customer', so permissions Customer.View / Add / Edit are unchanged); only the name people see changes.
UPDATE dbo.Modules SET Name = N'Companies'
WHERE Code = 'Customer' AND Name IN (N'Customers', N'Customer Management');
GO
