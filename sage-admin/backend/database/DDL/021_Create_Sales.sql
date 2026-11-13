-- 021: sales recorded in the User Panel. Price and cost are copied from the plan when the sale is saved, so later
-- price-list changes never alter past sales. ManagerId is the seller's manager at the time of the sale (team sales);
-- changing a person's manager later does not move their past sales. A sale is cancelled, never deleted.
IF OBJECT_ID(N'dbo.SaleNumberSequence', N'SO') IS NULL
    CREATE SEQUENCE dbo.SaleNumberSequence AS BIGINT START WITH 1 INCREMENT BY 1;
GO

IF OBJECT_ID(N'dbo.Sales', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Sales
    (
        Id                      BIGINT IDENTITY(1,1) NOT NULL,
        PublicId                UNIQUEIDENTIFIER     NOT NULL CONSTRAINT DF_Sales_PublicId DEFAULT NEWID(),
        SaleNumber              VARCHAR(20)          NOT NULL,           -- e.g. S-2026-000123
        CustomerId              BIGINT               NOT NULL,
        CategoryId              BIGINT               NOT NULL,
        ProductId               BIGINT               NOT NULL,
        PlanId                  BIGINT               NOT NULL,
        SalesUserId             BIGINT               NOT NULL,
        ManagerId               BIGINT               NULL,
        Quantity                INT                  NOT NULL,
        UnitPrice               DECIMAL(18,2)        NOT NULL,
        UnitCost                DECIMAL(18,2)        NOT NULL,
        ListAmount              DECIMAL(18,2)        NOT NULL,           -- UnitPrice x Quantity
        SaleAmount              DECIMAL(18,2)        NOT NULL,           -- what the customer pays
        DiscountAmount          DECIMAL(18,2)        NOT NULL,           -- ListAmount - SaleAmount
        CostAmount              DECIMAL(18,2)        NOT NULL,           -- UnitCost x Quantity
        DurationUnit            VARCHAR(10)          NOT NULL,
        DurationCount           INT                  NOT NULL,
        StartDate               DATE                 NOT NULL,
        RenewalDate             DATE                 NOT NULL,
        Status                  VARCHAR(20)          NOT NULL CONSTRAINT DF_Sales_Status DEFAULT 'Active',
        Notes                   NVARCHAR(1000)       NULL,
        CreatedAtUtc            DATETIME2(3)         NOT NULL CONSTRAINT DF_Sales_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        CancelledAtUtc          DATETIME2(3)         NULL,
        CancelledBySalesUserId  BIGINT               NULL,
        CancelReason            NVARCHAR(500)        NULL,
        CONSTRAINT PK_Sales PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_Sales_PublicId UNIQUE (PublicId),
        CONSTRAINT UQ_Sales_SaleNumber UNIQUE (SaleNumber),
        CONSTRAINT FK_Sales_Customers FOREIGN KEY (CustomerId) REFERENCES dbo.Customers (Id),
        CONSTRAINT FK_Sales_ProductCategories FOREIGN KEY (CategoryId) REFERENCES dbo.ProductCategories (Id),
        CONSTRAINT FK_Sales_Products FOREIGN KEY (ProductId) REFERENCES dbo.Products (Id),
        CONSTRAINT FK_Sales_ProductPlans FOREIGN KEY (PlanId) REFERENCES dbo.ProductPlans (Id),
        CONSTRAINT FK_Sales_SalesUser FOREIGN KEY (SalesUserId) REFERENCES dbo.SalesUsers (Id),
        CONSTRAINT FK_Sales_Manager FOREIGN KEY (ManagerId) REFERENCES dbo.SalesUsers (Id),
        CONSTRAINT FK_Sales_CancelledBy FOREIGN KEY (CancelledBySalesUserId) REFERENCES dbo.SalesUsers (Id),
        CONSTRAINT CK_Sales_Quantity CHECK (Quantity BETWEEN 1 AND 100000),
        CONSTRAINT CK_Sales_UnitPrice CHECK (UnitPrice >= 0 AND UnitCost >= 0),
        CONSTRAINT CK_Sales_ListAmount CHECK (ListAmount = UnitPrice * Quantity),
        CONSTRAINT CK_Sales_CostAmount CHECK (CostAmount = UnitCost * Quantity),
        CONSTRAINT CK_Sales_SaleAmount CHECK (SaleAmount >= 0 AND SaleAmount <= ListAmount),
        CONSTRAINT CK_Sales_DiscountAmount CHECK (DiscountAmount = ListAmount - SaleAmount),
        CONSTRAINT CK_Sales_DurationUnit CHECK (DurationUnit IN ('Month','Quarter','Year')),
        CONSTRAINT CK_Sales_DurationCount CHECK (DurationCount BETWEEN 1 AND 120),
        CONSTRAINT CK_Sales_RenewalDate CHECK (RenewalDate > StartDate),
        CONSTRAINT CK_Sales_Status CHECK (Status IN ('Active','Cancelled')),
        CONSTRAINT CK_Sales_Cancelled CHECK ((Status = 'Active' AND CancelledAtUtc IS NULL) OR (Status = 'Cancelled' AND CancelledAtUtc IS NOT NULL))
    );
    CREATE NONCLUSTERED INDEX IX_Sales_SalesUser_CreatedAtUtc ON dbo.Sales (SalesUserId, CreatedAtUtc DESC);
    CREATE NONCLUSTERED INDEX IX_Sales_Manager_CreatedAtUtc ON dbo.Sales (ManagerId, CreatedAtUtc DESC);
    CREATE NONCLUSTERED INDEX IX_Sales_CustomerId ON dbo.Sales (CustomerId);
    CREATE NONCLUSTERED INDEX IX_Sales_RenewalDate ON dbo.Sales (RenewalDate) WHERE Status = 'Active';
END
GO
