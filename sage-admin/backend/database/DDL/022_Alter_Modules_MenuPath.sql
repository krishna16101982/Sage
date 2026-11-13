-- 022: the Admin Panel sidebar is built from dbo.Modules. MenuPath is the screen the module opens (e.g. '/products');
-- NULL keeps the module out of the sidebar (e.g. modules that only carry permissions, or whose screen is not built yet).
-- A module is shown to users who hold its '<Code>.View' permission (Super Admin: all), ordered by SortOrder and grouped by Category.
IF COL_LENGTH(N'dbo.Modules', N'MenuPath') IS NULL
BEGIN
    ALTER TABLE dbo.Modules ADD MenuPath NVARCHAR(200) NULL;
END
GO

IF OBJECT_ID(N'dbo.CK_Modules_MenuPath', N'C') IS NULL
BEGIN
    ALTER TABLE dbo.Modules ADD CONSTRAINT CK_Modules_MenuPath CHECK (MenuPath IS NULL OR (MenuPath LIKE '/%' AND MenuPath NOT LIKE '%[^a-z0-9/-]%'));
END
GO
