-- 029: order-form details on a sale, saved by the User Panel's "New order form" (dbo.usp_SaleOrder_Create). Same
-- Sales table, extra columns, all optional: a sale from the simple "New sale" page keeps GST 0, no free months and no
-- billing details, exactly as before.
--   GstPercent / TaxAmount   GST on the sale amount (after discount); TotalAmount = SaleAmount + TaxAmount.
--   FreeMonths               extra months at the same price (12 + 2): they extend the renewal date, not the price.
--   ...Id                    billing frequency / method and payment method / term from dbo.MasterValues.
--   Contacts / BillToAddress copied onto the sale, so later customer edits do not change a signed order.
IF COL_LENGTH(N'dbo.Sales', N'GstPercent') IS NULL
BEGIN
    ALTER TABLE dbo.Sales ADD
        GstPercent                 DECIMAL(5,2)   NOT NULL CONSTRAINT DF_Sales_GstPercent DEFAULT 0,
        TaxAmount                  DECIMAL(18,2)  NOT NULL CONSTRAINT DF_Sales_TaxAmount DEFAULT 0,
        FreeMonths                 INT            NOT NULL CONSTRAINT DF_Sales_FreeMonths DEFAULT 0,
        BillingFrequencyId         INT            NULL,
        BillingMethodId            INT            NULL,
        PaymentMethodId            INT            NULL,
        PaymentTermId              INT            NULL,
        PrimaryContactName         NVARCHAR(150)  NULL,
        PrimaryContactDesignation  NVARCHAR(100)  NULL,
        PrimaryContactEmail        NVARCHAR(256)  NULL,
        PrimaryContactPhone        NVARCHAR(20)   NULL,
        BillingContactName         NVARCHAR(150)  NULL,
        BillingContactDesignation  NVARCHAR(100)  NULL,
        BillingContactEmail        NVARCHAR(256)  NULL,
        BillingContactPhone        NVARCHAR(20)   NULL,
        BillToAddress              NVARCHAR(500)  NULL;
END
GO

IF COL_LENGTH(N'dbo.Sales', N'TotalAmount') IS NULL
    ALTER TABLE dbo.Sales ADD TotalAmount AS (SaleAmount + TaxAmount) PERSISTED;
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Sales_GstPercent')
    ALTER TABLE dbo.Sales ADD CONSTRAINT CK_Sales_GstPercent CHECK (GstPercent BETWEEN 0 AND 28);
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Sales_TaxAmount')
    ALTER TABLE dbo.Sales ADD CONSTRAINT CK_Sales_TaxAmount CHECK (TaxAmount >= 0);
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Sales_FreeMonths')
    ALTER TABLE dbo.Sales ADD CONSTRAINT CK_Sales_FreeMonths CHECK (FreeMonths BETWEEN 0 AND 24);
IF OBJECT_ID(N'dbo.FK_Sales_BillingFrequency', N'F') IS NULL
    ALTER TABLE dbo.Sales ADD CONSTRAINT FK_Sales_BillingFrequency FOREIGN KEY (BillingFrequencyId) REFERENCES dbo.MasterValues (Id);
IF OBJECT_ID(N'dbo.FK_Sales_BillingMethod', N'F') IS NULL
    ALTER TABLE dbo.Sales ADD CONSTRAINT FK_Sales_BillingMethod FOREIGN KEY (BillingMethodId) REFERENCES dbo.MasterValues (Id);
IF OBJECT_ID(N'dbo.FK_Sales_PaymentMethod', N'F') IS NULL
    ALTER TABLE dbo.Sales ADD CONSTRAINT FK_Sales_PaymentMethod FOREIGN KEY (PaymentMethodId) REFERENCES dbo.MasterValues (Id);
IF OBJECT_ID(N'dbo.FK_Sales_PaymentTerm', N'F') IS NULL
    ALTER TABLE dbo.Sales ADD CONSTRAINT FK_Sales_PaymentTerm FOREIGN KEY (PaymentTermId) REFERENCES dbo.MasterValues (Id);
GO
