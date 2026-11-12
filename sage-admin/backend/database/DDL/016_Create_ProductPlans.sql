-- 016: plans of a product (e.g. Monthly, Yearly, Business, Individual) = the price list.
-- Price and Cost are per unit (INR) for one plan term (DurationCount x DurationUnit). Sales copy them when saved.
IF OBJECT_ID(N'dbo.ProductPlans', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ProductPlans
    (
        Id             BIGINT IDENTITY(1,1) NOT NULL,
        ProductId      BIGINT               NOT NULL,
        Name           NVARCHAR(100)        NOT NULL,
        Description    NVARCHAR(500)        NULL,
        DurationUnit   VARCHAR(10)          NOT NULL CONSTRAINT DF_ProductPlans_DurationUnit DEFAULT 'Year',
        DurationCount  INT                  NOT NULL CONSTRAINT DF_ProductPlans_DurationCount DEFAULT 1,
        Price          DECIMAL(18,2)        NOT NULL,
        Cost           DECIMAL(18,2)        NOT NULL,
        SortOrder      INT                  NOT NULL CONSTRAINT DF_ProductPlans_SortOrder DEFAULT 0,
        IsActive       BIT                  NOT NULL CONSTRAINT DF_ProductPlans_IsActive DEFAULT 1,
        CreatedAtUtc   DATETIME2(3)         NOT NULL CONSTRAINT DF_ProductPlans_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        CreatedBy      BIGINT               NULL,
        UpdatedAtUtc   DATETIME2(3)         NULL,
        UpdatedBy      BIGINT               NULL,
        CONSTRAINT PK_ProductPlans PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_ProductPlans_Products FOREIGN KEY (ProductId) REFERENCES dbo.Products (Id),
        CONSTRAINT UQ_ProductPlans_Product_Name UNIQUE (ProductId, Name),
        CONSTRAINT CK_ProductPlans_DurationUnit CHECK (DurationUnit IN ('Month','Quarter','Year')),
        CONSTRAINT CK_ProductPlans_DurationCount CHECK (DurationCount BETWEEN 1 AND 120),
        CONSTRAINT CK_ProductPlans_Price CHECK (Price >= 0),
        CONSTRAINT CK_ProductPlans_Cost CHECK (Cost >= 0)
    );
    CREATE NONCLUSTERED INDEX IX_ProductPlans_ProductId ON dbo.ProductPlans (ProductId);
END
GO
