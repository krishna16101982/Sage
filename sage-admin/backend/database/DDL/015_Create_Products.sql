-- 015: products sold (e.g. GreytHR, Freshdesk). Each belongs to one category.
IF OBJECT_ID(N'dbo.Products', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Products
    (
        Id            BIGINT IDENTITY(1,1) NOT NULL,
        CategoryId    BIGINT               NOT NULL,
        Code          VARCHAR(50)          NOT NULL,
        Name          NVARCHAR(100)        NOT NULL,
        Description   NVARCHAR(500)        NULL,
        SortOrder     INT                  NOT NULL CONSTRAINT DF_Products_SortOrder DEFAULT 0,
        IsActive      BIT                  NOT NULL CONSTRAINT DF_Products_IsActive DEFAULT 1,
        CreatedAtUtc  DATETIME2(3)         NOT NULL CONSTRAINT DF_Products_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        CreatedBy     BIGINT               NULL,
        UpdatedAtUtc  DATETIME2(3)         NULL,
        UpdatedBy     BIGINT               NULL,
        CONSTRAINT PK_Products PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_Products_ProductCategories FOREIGN KEY (CategoryId) REFERENCES dbo.ProductCategories (Id),
        CONSTRAINT UQ_Products_Code UNIQUE (Code),
        CONSTRAINT UQ_Products_Category_Name UNIQUE (CategoryId, Name),
        CONSTRAINT CK_Products_Code CHECK (LEN(Code) >= 2)
    );
    CREATE NONCLUSTERED INDEX IX_Products_CategoryId ON dbo.Products (CategoryId);
END
GO
