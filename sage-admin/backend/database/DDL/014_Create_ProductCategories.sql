-- 014: product categories (e.g. SaaS, Cyber Security). Dynamic, managed in the Admin Panel.
IF OBJECT_ID(N'dbo.ProductCategories', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ProductCategories
    (
        Id            BIGINT IDENTITY(1,1) NOT NULL,
        Code          VARCHAR(50)          NOT NULL,
        Name          NVARCHAR(100)        NOT NULL,
        Description   NVARCHAR(500)        NULL,
        SortOrder     INT                  NOT NULL CONSTRAINT DF_ProductCategories_SortOrder DEFAULT 0,
        IsActive      BIT                  NOT NULL CONSTRAINT DF_ProductCategories_IsActive DEFAULT 1,
        CreatedAtUtc  DATETIME2(3)         NOT NULL CONSTRAINT DF_ProductCategories_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        CreatedBy     BIGINT               NULL,
        UpdatedAtUtc  DATETIME2(3)         NULL,
        UpdatedBy     BIGINT               NULL,
        CONSTRAINT PK_ProductCategories PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_ProductCategories_Code UNIQUE (Code),
        CONSTRAINT UQ_ProductCategories_Name UNIQUE (Name),
        CONSTRAINT CK_ProductCategories_Code CHECK (LEN(Code) >= 2)
    );
END
GO
