-- 020: customers the sales team sells to. Shared: every sales user can search all customers and add new ones.
-- Duplicates are refused by usp_Customer_Create (same GSTIN, or same name in the same city).
IF OBJECT_ID(N'dbo.Customers', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Customers
    (
        Id                    BIGINT IDENTITY(1,1) NOT NULL,
        PublicId              UNIQUEIDENTIFIER     NOT NULL CONSTRAINT DF_Customers_PublicId DEFAULT NEWID(),
        Name                  NVARCHAR(200)        NOT NULL,
        ContactPerson         NVARCHAR(150)        NULL,
        Email                 NVARCHAR(256)        NULL,
        Phone                 NVARCHAR(20)         NULL,
        GSTIN                 VARCHAR(15)          NULL,
        Address               NVARCHAR(500)        NULL,
        City                  NVARCHAR(100)        NULL,
        State                 NVARCHAR(100)        NULL,
        Country               NVARCHAR(100)        NOT NULL CONSTRAINT DF_Customers_Country DEFAULT N'India',
        IsActive              BIT                  NOT NULL CONSTRAINT DF_Customers_IsActive DEFAULT 1,
        CreatedAtUtc          DATETIME2(3)         NOT NULL CONSTRAINT DF_Customers_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        CreatedBySalesUserId  BIGINT               NULL,
        UpdatedAtUtc          DATETIME2(3)         NULL,
        CONSTRAINT PK_Customers PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_Customers_PublicId UNIQUE (PublicId),
        CONSTRAINT FK_Customers_CreatedBySalesUser FOREIGN KEY (CreatedBySalesUserId) REFERENCES dbo.SalesUsers (Id),
        -- GSTIN: 2-digit state code, 10-character PAN, entity number, 'Z', check character.
        CONSTRAINT CK_Customers_GSTIN CHECK (GSTIN IS NULL OR GSTIN LIKE '[0-9][0-9][A-Z][A-Z][A-Z][A-Z][A-Z][0-9][0-9][0-9][0-9][A-Z][0-9A-Z]Z[0-9A-Z]')
    );
    CREATE UNIQUE NONCLUSTERED INDEX UX_Customers_GSTIN ON dbo.Customers (GSTIN) WHERE GSTIN IS NOT NULL;
    CREATE NONCLUSTERED INDEX IX_Customers_Name ON dbo.Customers (Name);
END
GO
