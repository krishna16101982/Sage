-- 023: customers can also be added and edited in the Admin Panel. Record which admin user did it
-- (CreatedBySalesUserId stays for customers added in the User Panel; exactly one of the two "created by" is set).
IF COL_LENGTH(N'dbo.Customers', N'CreatedByAdminUserId') IS NULL
BEGIN
    ALTER TABLE dbo.Customers ADD
        CreatedByAdminUserId  BIGINT NULL CONSTRAINT FK_Customers_CreatedByAdminUser REFERENCES dbo.AdminUsers (Id),
        UpdatedByAdminUserId  BIGINT NULL CONSTRAINT FK_Customers_UpdatedByAdminUser REFERENCES dbo.AdminUsers (Id);
END
GO
