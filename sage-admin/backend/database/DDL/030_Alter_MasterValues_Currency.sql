-- 030: Currency becomes an order-form list too (every drop-down on the order form comes from dbo.MasterValues), and a
-- sale made with the order form records it. Prices in the price list are in INR, so INR is the starting value.
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_MasterValues_Type'
           AND definition NOT LIKE '%Currency%')
BEGIN
    ALTER TABLE dbo.MasterValues DROP CONSTRAINT CK_MasterValues_Type;
    ALTER TABLE dbo.MasterValues ADD CONSTRAINT CK_MasterValues_Type
        CHECK (MasterType IN ('BillingFrequency', 'BillingMethod', 'PaymentMethod', 'PaymentTerm', 'Currency'));
END
GO

IF COL_LENGTH(N'dbo.Sales', N'CurrencyId') IS NULL
    ALTER TABLE dbo.Sales ADD CurrencyId INT NULL;
GO
IF OBJECT_ID(N'dbo.FK_Sales_Currency', N'F') IS NULL
    ALTER TABLE dbo.Sales ADD CONSTRAINT FK_Sales_Currency FOREIGN KEY (CurrencyId) REFERENCES dbo.MasterValues (Id);
GO
