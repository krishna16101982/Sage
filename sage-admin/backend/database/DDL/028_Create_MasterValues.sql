-- 028: master lists for the order form (Admin Panel > Settings > Order form lists). One table holds every list; MasterType
-- says which list a row belongs to. Each list has at most one default (filled in on a new order form). A value that is in
-- use is deactivated, never deleted, so old orders keep showing it.
IF OBJECT_ID(N'dbo.MasterValues', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.MasterValues
    (
        Id            INT IDENTITY(1,1) NOT NULL,
        MasterType    VARCHAR(30)    NOT NULL,
        Code          VARCHAR(30)    NOT NULL,
        Name          NVARCHAR(100)  NOT NULL,
        SortOrder     INT            NOT NULL CONSTRAINT DF_MasterValues_SortOrder DEFAULT 0,
        IsDefault     BIT            NOT NULL CONSTRAINT DF_MasterValues_IsDefault DEFAULT 0,
        IsActive      BIT            NOT NULL CONSTRAINT DF_MasterValues_IsActive DEFAULT 1,
        CreatedAtUtc  DATETIME2(3)   NOT NULL CONSTRAINT DF_MasterValues_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        UpdatedAtUtc  DATETIME2(3)   NULL,
        UpdatedBy     BIGINT         NULL,
        CONSTRAINT PK_MasterValues PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_MasterValues_Type_Code UNIQUE (MasterType, Code),
        CONSTRAINT UQ_MasterValues_Type_Name UNIQUE (MasterType, Name),
        CONSTRAINT FK_MasterValues_UpdatedBy FOREIGN KEY (UpdatedBy) REFERENCES dbo.AdminUsers (Id),
        CONSTRAINT CK_MasterValues_Type CHECK (MasterType IN ('BillingFrequency', 'BillingMethod', 'PaymentMethod', 'PaymentTerm')),
        CONSTRAINT CK_MasterValues_DefaultIsActive CHECK (IsDefault = 0 OR IsActive = 1)
    );
    -- At most one default per list.
    CREATE UNIQUE NONCLUSTERED INDEX UX_MasterValues_OneDefault ON dbo.MasterValues (MasterType) WHERE IsDefault = 1;
    -- The order form reads each list's active values in order.
    CREATE NONCLUSTERED INDEX IX_MasterValues_Type_Active ON dbo.MasterValues (MasterType, IsActive, SortOrder) INCLUDE (Code, Name, IsDefault);
END
GO
