-- 035: leads (User Panel > Lead Records). Every sales user sees every lead; only the person who created a lead and that
-- person's manager change it, and only managers delete it (a deleted lead is hidden, never removed).
-- The company of a lead is a dbo.Customers row (customer = company everywhere) and cannot be changed after saving.
-- Each list has its own table:
--   LeadProducts, LeadSources, LeadBillingCycles   Admin Panel > Settings > Lead lists (add, rename, reorder, turn off)
--   LeadStatuses, LeadTypes                        fixed values (Open, Warm, Won, Lost, Hold, Disqualified / Self Working, Passed)
-- A list value that is in use is turned off, never deleted, so old leads keep showing it.

IF OBJECT_ID(N'dbo.LeadProducts', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.LeadProducts
    (
        Id            INT IDENTITY(1,1) NOT NULL,
        Name          NVARCHAR(100)  NOT NULL,
        SortOrder     INT            NOT NULL CONSTRAINT DF_LeadProducts_SortOrder DEFAULT 0,
        IsActive      BIT            NOT NULL CONSTRAINT DF_LeadProducts_IsActive DEFAULT 1,
        CreatedAtUtc  DATETIME2(3)   NOT NULL CONSTRAINT DF_LeadProducts_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        UpdatedAtUtc  DATETIME2(3)   NULL,
        UpdatedBy     BIGINT         NULL,
        CONSTRAINT PK_LeadProducts PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_LeadProducts_Name UNIQUE (Name),
        CONSTRAINT FK_LeadProducts_UpdatedBy FOREIGN KEY (UpdatedBy) REFERENCES dbo.AdminUsers (Id)
    );
END
GO

IF OBJECT_ID(N'dbo.LeadSources', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.LeadSources
    (
        Id            INT IDENTITY(1,1) NOT NULL,
        Name          NVARCHAR(100)  NOT NULL,
        SortOrder     INT            NOT NULL CONSTRAINT DF_LeadSources_SortOrder DEFAULT 0,
        IsActive      BIT            NOT NULL CONSTRAINT DF_LeadSources_IsActive DEFAULT 1,
        CreatedAtUtc  DATETIME2(3)   NOT NULL CONSTRAINT DF_LeadSources_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        UpdatedAtUtc  DATETIME2(3)   NULL,
        UpdatedBy     BIGINT         NULL,
        CONSTRAINT PK_LeadSources PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_LeadSources_Name UNIQUE (Name),
        CONSTRAINT FK_LeadSources_UpdatedBy FOREIGN KEY (UpdatedBy) REFERENCES dbo.AdminUsers (Id)
    );
END
GO

IF OBJECT_ID(N'dbo.LeadBillingCycles', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.LeadBillingCycles
    (
        Id            INT IDENTITY(1,1) NOT NULL,
        Name          NVARCHAR(100)  NOT NULL,
        SortOrder     INT            NOT NULL CONSTRAINT DF_LeadBillingCycles_SortOrder DEFAULT 0,
        IsActive      BIT            NOT NULL CONSTRAINT DF_LeadBillingCycles_IsActive DEFAULT 1,
        CreatedAtUtc  DATETIME2(3)   NOT NULL CONSTRAINT DF_LeadBillingCycles_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        UpdatedAtUtc  DATETIME2(3)   NULL,
        UpdatedBy     BIGINT         NULL,
        CONSTRAINT PK_LeadBillingCycles PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_LeadBillingCycles_Name UNIQUE (Name),
        CONSTRAINT FK_LeadBillingCycles_UpdatedBy FOREIGN KEY (UpdatedBy) REFERENCES dbo.AdminUsers (Id)
    );
END
GO

-- Fixed lists: the code is what the application looks at (WON shows "New Order"); the name is what people see.
IF OBJECT_ID(N'dbo.LeadStatuses', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.LeadStatuses
    (
        Id         INT            NOT NULL,
        Code       VARCHAR(20)    NOT NULL,
        Name       NVARCHAR(50)   NOT NULL,
        SortOrder  INT            NOT NULL,
        CONSTRAINT PK_LeadStatuses PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_LeadStatuses_Code UNIQUE (Code)
    );
    INSERT dbo.LeadStatuses (Id, Code, Name, SortOrder) VALUES
        (1, 'OPEN', N'Open', 1), (2, 'WARM', N'Warm', 2), (3, 'WON', N'Won', 3),
        (4, 'LOST', N'Lost', 4), (5, 'HOLD', N'Hold', 5), (6, 'DISQUALIFIED', N'Disqualified', 6);
END
GO

IF OBJECT_ID(N'dbo.LeadTypes', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.LeadTypes
    (
        Id         INT            NOT NULL,
        Code       VARCHAR(20)    NOT NULL,
        Name       NVARCHAR(50)   NOT NULL,
        SortOrder  INT            NOT NULL,
        CONSTRAINT PK_LeadTypes PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_LeadTypes_Code UNIQUE (Code)
    );
    INSERT dbo.LeadTypes (Id, Code, Name, SortOrder) VALUES (1, 'SELF', N'Self Working', 1), (2, 'PASSED', N'Passed', 2);
END
GO

IF OBJECT_ID(N'dbo.LeadNumberSequence', N'SO') IS NULL
    CREATE SEQUENCE dbo.LeadNumberSequence AS BIGINT START WITH 1 INCREMENT BY 1;
GO

IF OBJECT_ID(N'dbo.Leads', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Leads
    (
        Id                           BIGINT IDENTITY(1,1) NOT NULL,
        PublicId                     UNIQUEIDENTIFIER     NOT NULL CONSTRAINT DF_Leads_PublicId DEFAULT NEWID(),
        LeadNumber                   VARCHAR(20)          NOT NULL,           -- LD-0050
        CustomerId                   BIGINT               NOT NULL,           -- the company
        ClientName                   NVARCHAR(150)        NULL,
        ClientDesignation            NVARCHAR(100)        NULL,
        ClientEmail                  NVARCHAR(256)        NULL,
        ClientPhone                  NVARCHAR(20)         NULL,
        SecondaryClientName          NVARCHAR(150)        NULL,
        SecondaryClientDesignation   NVARCHAR(100)        NULL,
        SecondaryClientEmail         NVARCHAR(256)        NULL,
        SecondaryClientPhone         NVARCHAR(20)         NULL,
        ProductId                    INT                  NULL,
        SourceId                     INT                  NULL,
        Requirement                  NVARCHAR(2000)       NULL,
        StatusId                     INT                  NOT NULL,
        TypeId                       INT                  NOT NULL,
        GeneratedBySalesUserId       BIGINT               NOT NULL,
        AssignedSalesUserId          BIGINT               NOT NULL,
        MonthlyExpectedRevenue       DECIMAL(18,2)        NULL,
        BillingCycleId               INT                  NULL,
        NextFollowUpDate             DATE                 NULL,
        LeadDate                     DATE                 NOT NULL,           -- "Created Date" on the form; aging counts from it
        ClosingDate                  DATE                 NULL,
        Remarks                      NVARCHAR(2000)       NULL,
        SaleId                       BIGINT               NULL,               -- the order made from this lead (New Order)
        CreatedBySalesUserId         BIGINT               NOT NULL,
        CreatedAtUtc                 DATETIME2(3)         NOT NULL CONSTRAINT DF_Leads_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        UpdatedBySalesUserId         BIGINT               NULL,
        UpdatedAtUtc                 DATETIME2(3)         NULL,
        IsDeleted                    BIT                  NOT NULL CONSTRAINT DF_Leads_IsDeleted DEFAULT 0,
        DeletedBySalesUserId         BIGINT               NULL,
        DeletedAtUtc                 DATETIME2(3)         NULL,
        CONSTRAINT PK_Leads PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_Leads_PublicId UNIQUE (PublicId),
        CONSTRAINT UQ_Leads_LeadNumber UNIQUE (LeadNumber),
        CONSTRAINT FK_Leads_Customers FOREIGN KEY (CustomerId) REFERENCES dbo.Customers (Id),
        CONSTRAINT FK_Leads_Product FOREIGN KEY (ProductId) REFERENCES dbo.LeadProducts (Id),
        CONSTRAINT FK_Leads_Source FOREIGN KEY (SourceId) REFERENCES dbo.LeadSources (Id),
        CONSTRAINT FK_Leads_Status FOREIGN KEY (StatusId) REFERENCES dbo.LeadStatuses (Id),
        CONSTRAINT FK_Leads_Type FOREIGN KEY (TypeId) REFERENCES dbo.LeadTypes (Id),
        CONSTRAINT FK_Leads_BillingCycle FOREIGN KEY (BillingCycleId) REFERENCES dbo.LeadBillingCycles (Id),
        CONSTRAINT FK_Leads_GeneratedBy FOREIGN KEY (GeneratedBySalesUserId) REFERENCES dbo.SalesUsers (Id),
        CONSTRAINT FK_Leads_Assigned FOREIGN KEY (AssignedSalesUserId) REFERENCES dbo.SalesUsers (Id),
        CONSTRAINT FK_Leads_CreatedBy FOREIGN KEY (CreatedBySalesUserId) REFERENCES dbo.SalesUsers (Id),
        CONSTRAINT FK_Leads_UpdatedBy FOREIGN KEY (UpdatedBySalesUserId) REFERENCES dbo.SalesUsers (Id),
        CONSTRAINT FK_Leads_DeletedBy FOREIGN KEY (DeletedBySalesUserId) REFERENCES dbo.SalesUsers (Id),
        CONSTRAINT FK_Leads_Sale FOREIGN KEY (SaleId) REFERENCES dbo.Sales (Id),
        CONSTRAINT CK_Leads_Revenue CHECK (MonthlyExpectedRevenue IS NULL OR MonthlyExpectedRevenue >= 0),
        CONSTRAINT CK_Leads_Deleted CHECK ((IsDeleted = 0 AND DeletedAtUtc IS NULL) OR (IsDeleted = 1 AND DeletedAtUtc IS NOT NULL))
    );
    CREATE NONCLUSTERED INDEX IX_Leads_LeadDate ON dbo.Leads (LeadDate DESC) WHERE IsDeleted = 0;
    CREATE NONCLUSTERED INDEX IX_Leads_CustomerId ON dbo.Leads (CustomerId);
    CREATE NONCLUSTERED INDEX IX_Leads_Assigned ON dbo.Leads (AssignedSalesUserId) WHERE IsDeleted = 0;
    CREATE NONCLUSTERED INDEX IX_Leads_GeneratedBy ON dbo.Leads (GeneratedBySalesUserId) WHERE IsDeleted = 0;
END
GO
