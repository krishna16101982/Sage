-- ============================================================================================
-- Release 2026-11-23: leads, lead lists, order edit and tracking, Order forms list, lead dashboard, company edit
-- Database changes of commits 0afc143..fa59276 (git). Generated 2026-11-24 11:27.
-- Run once on the target database (e.g. in SSMS: choose the database, then Execute). Every part is safe to
-- run again: tables are created only when missing, procedures use CREATE OR ALTER, seed data adds only
-- missing rows. At the end the DDL / seed scripts are recorded in dbo.SchemaVersions, so a later run of
-- Scripts/Deploy-Database.ps1 skips them.
-- Contents:
--   DDL/035_Create_Leads.sql
--   DDL/036_Create_OrderTrackingStatuses.sql
--   StoredProcedures/usp_Customer_Create.sql
--   StoredProcedures/usp_Customer_Update.sql
--   StoredProcedures/usp_Customer_UpdateDetails.sql
--   StoredProcedures/usp_Lead_Delete.sql
--   StoredProcedures/usp_Lead_GetPaged.sql
--   StoredProcedures/usp_Lead_Save.sql
--   StoredProcedures/usp_Lead_Summary.sql
--   StoredProcedures/usp_LeadListValue_Save.sql
--   StoredProcedures/usp_Sale_AdminGetPaged.sql
--   StoredProcedures/usp_Sale_GetPaged.sql
--   StoredProcedures/usp_SaleCancelRequest_GetPaged.sql
--   StoredProcedures/usp_SaleOrder_Create.sql
--   StoredProcedures/usp_SaleOrder_SetTracking.sql
--   StoredProcedures/usp_SaleOrder_Update.sql
--   StoredProcedures/usp_SalesUserHistory_GetPaged.sql
--   SeedData/013_Seed_Lead_Lists.sql
--   SeedData/014_Rename_Customers_To_Companies.sql
-- ============================================================================================
SET NOCOUNT ON;
GO

-- --------------------------------------------------------------------------------------------
-- DDL/035_Create_Leads.sql
-- --------------------------------------------------------------------------------------------
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

-- --------------------------------------------------------------------------------------------
-- DDL/036_Create_OrderTrackingStatuses.sql
-- --------------------------------------------------------------------------------------------
-- 036: tracking status of an order form (Draft, Sent to DocuSign, Signed, Cancelled), shown on the order form, its
-- page and the Order forms list. Fixed list in its own table. Sales.TrackingStatusId is set for orders saved with the
-- New order form (NULL for the simple New sale page). Cancelled is not stored by choice: it is how a cancelled sale shows
-- (choosing it cancels the sale, or asks the manager to); TrackingStatusId keeps the last status before that.
IF OBJECT_ID(N'dbo.OrderTrackingStatuses', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.OrderTrackingStatuses
    (
        Id         INT            NOT NULL,
        Code       VARCHAR(20)    NOT NULL,
        Name       NVARCHAR(50)   NOT NULL,
        SortOrder  INT            NOT NULL,
        CONSTRAINT PK_OrderTrackingStatuses PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_OrderTrackingStatuses_Code UNIQUE (Code)
    );
    INSERT dbo.OrderTrackingStatuses (Id, Code, Name, SortOrder) VALUES
        (1, 'DRAFT', N'Draft', 1), (2, 'SENT_DOCUSIGN', N'Sent to DocuSign', 2), (3, 'SIGNED', N'Signed', 3), (4, 'CANCELLED', N'Cancelled', 4);
END
GO

IF COL_LENGTH(N'dbo.Sales', N'TrackingStatusId') IS NULL
BEGIN
    ALTER TABLE dbo.Sales ADD TrackingStatusId INT NULL
        CONSTRAINT FK_Sales_TrackingStatus FOREIGN KEY REFERENCES dbo.OrderTrackingStatuses (Id);
END
GO

-- Orders already saved with the order form start as Draft.
UPDATE dbo.Sales SET TrackingStatusId = 1 WHERE TrackingStatusId IS NULL AND BillingFrequencyId IS NOT NULL;
GO

-- --------------------------------------------------------------------------------------------
-- StoredProcedures/usp_Customer_Create.sql
-- --------------------------------------------------------------------------------------------
-- Adds a customer, from the User Panel (@SalesUserId) or the Admin Panel (@AdminUserId). Raises 50409 when the GSTIN
-- is already used, or when a customer with the same name already exists in the same city. Returns the new PublicId.
CREATE OR ALTER PROCEDURE dbo.usp_Customer_Create
    @Name           NVARCHAR(200),
    @ContactPerson  NVARCHAR(150) = NULL,
    @Email          NVARCHAR(256) = NULL,
    @Phone          NVARCHAR(20)  = NULL,
    @GSTIN          VARCHAR(15)   = NULL,
    @Address        NVARCHAR(500) = NULL,
    @City           NVARCHAR(100) = NULL,
    @State          NVARCHAR(100) = NULL,
    @Country        NVARCHAR(100) = N'India',
    @SalesUserId    BIGINT        = NULL,
    @AdminUserId    BIGINT        = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRAN;

    IF @GSTIN IS NOT NULL AND EXISTS (SELECT 1 FROM dbo.Customers WITH (UPDLOCK, HOLDLOCK) WHERE GSTIN = @GSTIN)
        THROW 50409, 'A company with this GSTIN already exists', 1;

    IF EXISTS (SELECT 1 FROM dbo.Customers WITH (UPDLOCK, HOLDLOCK)
               WHERE Name = @Name AND ISNULL(City, N'') = ISNULL(@City, N''))
        THROW 50409, 'A company with this name already exists in this city. Search for it instead of adding it again', 1;

    DECLARE @publicId UNIQUEIDENTIFIER = NEWID();

    INSERT dbo.Customers (PublicId, Name, ContactPerson, Email, Phone, GSTIN, Address, City, State, Country, CreatedBySalesUserId, CreatedByAdminUserId)
    VALUES (@publicId, @Name, @ContactPerson, @Email, @Phone, @GSTIN, @Address, @City, @State, ISNULL(@Country, N'India'), @SalesUserId, @AdminUserId);

    COMMIT;

    SELECT @publicId;
END
GO

-- --------------------------------------------------------------------------------------------
-- StoredProcedures/usp_Customer_Update.sql
-- --------------------------------------------------------------------------------------------
-- Edits a customer (Admin Panel). Raises 50404 when it does not exist, 50409 when another customer already has the
-- GSTIN, or the same name in the same city. Past sales keep pointing at the customer, so they show the new details.
CREATE OR ALTER PROCEDURE dbo.usp_Customer_Update
    @PublicId       UNIQUEIDENTIFIER,
    @Name           NVARCHAR(200),
    @ContactPerson  NVARCHAR(150) = NULL,
    @Email          NVARCHAR(256) = NULL,
    @Phone          NVARCHAR(20)  = NULL,
    @GSTIN          VARCHAR(15)   = NULL,
    @Address        NVARCHAR(500) = NULL,
    @City           NVARCHAR(100) = NULL,
    @State          NVARCHAR(100) = NULL,
    @Country        NVARCHAR(100) = N'India',
    @AdminUserId    BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRAN;

    DECLARE @id BIGINT = (SELECT Id FROM dbo.Customers WITH (UPDLOCK, HOLDLOCK) WHERE PublicId = @PublicId AND IsActive = 1);
    IF @id IS NULL
        THROW 50404, 'Company not found', 1;

    IF @GSTIN IS NOT NULL AND EXISTS (SELECT 1 FROM dbo.Customers WITH (UPDLOCK, HOLDLOCK) WHERE GSTIN = @GSTIN AND Id <> @id)
        THROW 50409, 'Another company already has this GSTIN', 1;

    IF EXISTS (SELECT 1 FROM dbo.Customers WITH (UPDLOCK, HOLDLOCK)
               WHERE Name = @Name AND ISNULL(City, N'') = ISNULL(@City, N'') AND Id <> @id)
        THROW 50409, 'Another company with this name already exists in this city', 1;

    UPDATE dbo.Customers
    SET Name = @Name, ContactPerson = @ContactPerson, Email = @Email, Phone = @Phone, GSTIN = @GSTIN, Address = @Address,
        City = @City, State = @State, Country = ISNULL(@Country, N'India'), UpdatedAtUtc = SYSUTCDATETIME(), UpdatedByAdminUserId = @AdminUserId
    WHERE Id = @id;
    DECLARE @rows INT = @@ROWCOUNT;

    COMMIT;

    SELECT @rows;
END
GO

-- --------------------------------------------------------------------------------------------
-- StoredProcedures/usp_Customer_UpdateDetails.sql
-- --------------------------------------------------------------------------------------------
-- User Panel > Companies > Edit: changes a company's contact, GSTIN and address. The company name cannot be changed here
-- (it is read-only on the screen). Companies are shared, so any active sales user may edit them; the API audits who did.
-- Raises 50404 (not found), 50409 (another company has the GSTIN, or the same name in the new city) or 50400.
CREATE OR ALTER PROCEDURE dbo.usp_Customer_UpdateDetails
    @PublicId       UNIQUEIDENTIFIER,
    @ContactPerson  NVARCHAR(150) = NULL,
    @Email          NVARCHAR(256) = NULL,
    @Phone          NVARCHAR(20)  = NULL,
    @GSTIN          VARCHAR(15)   = NULL,
    @Address        NVARCHAR(500) = NULL,
    @City           NVARCHAR(100) = NULL,
    @State          NVARCHAR(100) = NULL,
    @Country        NVARCHAR(100) = N'India',
    @SalesUserId    BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.SalesUsers WHERE Id = @SalesUserId AND IsDeleted = 0 AND Status = 'Active')
        THROW 50400, 'Your account is not active', 1;

    BEGIN TRAN;

    DECLARE @id BIGINT, @name NVARCHAR(200);
    SELECT @id = Id, @name = Name FROM dbo.Customers WITH (UPDLOCK, HOLDLOCK) WHERE PublicId = @PublicId AND IsActive = 1;
    IF @id IS NULL
        THROW 50404, 'Company not found', 1;

    IF @GSTIN IS NOT NULL AND EXISTS (SELECT 1 FROM dbo.Customers WITH (UPDLOCK, HOLDLOCK) WHERE GSTIN = @GSTIN AND Id <> @id)
        THROW 50409, 'Another company already has this GSTIN', 1;

    IF EXISTS (SELECT 1 FROM dbo.Customers WITH (UPDLOCK, HOLDLOCK)
               WHERE Name = @name AND ISNULL(City, N'') = ISNULL(@City, N'') AND Id <> @id)
        THROW 50409, 'Another company with this name already exists in this city', 1;

    UPDATE dbo.Customers
    SET ContactPerson = @ContactPerson, Email = @Email, Phone = @Phone, GSTIN = @GSTIN, Address = @Address,
        City = @City, State = @State, Country = ISNULL(@Country, N'India'), UpdatedAtUtc = SYSUTCDATETIME()
    WHERE Id = @id;

    COMMIT;

    SELECT @PublicId;
END
GO

-- --------------------------------------------------------------------------------------------
-- StoredProcedures/usp_Lead_Delete.sql
-- --------------------------------------------------------------------------------------------
-- User Panel > Lead Records: deletes a lead. Only sales managers, for their own leads and leads created by their team.
-- The lead is hidden (IsDeleted, with who and when), never removed. Raises 50404 or 50400. Returns the lead's PublicId.
CREATE OR ALTER PROCEDURE dbo.usp_Lead_Delete
    @LeadPublicId      UNIQUEIDENTIFIER,
    @ActorSalesUserId  BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.SalesUsers WHERE Id = @ActorSalesUserId AND IsDeleted = 0 AND Status = 'Active' AND Role = 'SalesManager')
        THROW 50400, 'Only sales managers can delete leads', 1;

    DECLARE @leadId BIGINT, @creatorId BIGINT;
    SELECT @leadId = Id, @creatorId = CreatedBySalesUserId FROM dbo.Leads WHERE PublicId = @LeadPublicId AND IsDeleted = 0;
    IF @leadId IS NULL
        THROW 50404, 'Lead not found', 1;
    IF @creatorId <> @ActorSalesUserId
       AND NOT EXISTS (SELECT 1 FROM dbo.SalesUsers WHERE Id = @creatorId AND ManagerId = @ActorSalesUserId)
        THROW 50400, 'You can delete your own leads and leads created by your team', 1;

    UPDATE dbo.Leads
    SET IsDeleted = 1, DeletedBySalesUserId = @ActorSalesUserId, DeletedAtUtc = SYSUTCDATETIME()
    WHERE Id = @leadId;

    SELECT @LeadPublicId;
END
GO

-- --------------------------------------------------------------------------------------------
-- StoredProcedures/usp_Lead_GetPaged.sql
-- --------------------------------------------------------------------------------------------
-- User Panel > Lead Records: one page of leads (every sales user sees every lead that is not deleted), or one lead
-- (@LeadPublicId). Paging, filters and sorting all happen here, never in the browser.
--   @Search: a ready LIKE pattern ('\' escape) matched against lead number, company, client names and people's names.
--   @StatusId / @ProductId / @AssignedPublicId / @GeneratedByPublicId: exact filters (NULL = all).
--   @Aging: days since the lead's created date (India time): 'lt7' (< 7), '7to30', '31to60', 'gt60' (> 60).
--   @From / @To: the lead's created date. @SortBy: leadNumber (default), leadDate, company, revenue, aging.
-- For @ActorSalesUserId each row also says what that person may do:
--   CanEdit   the creator or the creator's manager;   CanDelete   CanEdit and a sales manager;
--   CanOrder  a Won lead with no order yet, for CanEdit or the assigned person.
CREATE OR ALTER PROCEDURE dbo.usp_Lead_GetPaged
    @ActorSalesUserId     BIGINT,
    @LeadPublicId         UNIQUEIDENTIFIER = NULL,
    @Search               NVARCHAR(210)    = NULL,
    @StatusId             INT              = NULL,
    @ProductId            INT              = NULL,
    @AssignedPublicId     UNIQUEIDENTIFIER = NULL,
    @GeneratedByPublicId  UNIQUEIDENTIFIER = NULL,
    @Aging                VARCHAR(10)      = NULL,
    @From                 DATE             = NULL,
    @To                   DATE             = NULL,
    @WithoutOrder         BIT              = 0,      -- 1: only leads no order was made from yet (the order form's lead picker)
    @SortBy               VARCHAR(20)      = 'leadNumber',
    @SortDirection        VARCHAR(4)       = 'DESC',
    @Offset               INT              = 0,
    @PageSize             INT              = 20,
    @TotalCount           INT              OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @desc BIT = CASE WHEN @SortDirection = 'DESC' THEN 1 ELSE 0 END;
    DECLARE @today DATE = CAST(SWITCHOFFSET(SYSDATETIMEOFFSET(), '+05:30') AS DATE);
    DECLARE @isManager BIT = CASE WHEN EXISTS (SELECT 1 FROM dbo.SalesUsers WHERE Id = @ActorSalesUserId AND Role = 'SalesManager') THEN 1 ELSE 0 END;
    DECLARE @assignedId BIGINT = NULL, @generatedById BIGINT = NULL;
    IF @AssignedPublicId IS NOT NULL
        SET @assignedId = ISNULL((SELECT Id FROM dbo.SalesUsers WHERE PublicId = @AssignedPublicId), -1);      -- unknown: empty result
    IF @GeneratedByPublicId IS NOT NULL
        SET @generatedById = ISNULL((SELECT Id FROM dbo.SalesUsers WHERE PublicId = @GeneratedByPublicId), -1);
    -- Aging bucket as a range of created dates.
    DECLARE @agingFrom DATE = CASE @Aging WHEN 'lt7' THEN DATEADD(DAY, -6, @today) WHEN '7to30' THEN DATEADD(DAY, -30, @today)
                                          WHEN '31to60' THEN DATEADD(DAY, -60, @today) END;
    DECLARE @agingTo DATE = CASE @Aging WHEN '7to30' THEN DATEADD(DAY, -7, @today) WHEN '31to60' THEN DATEADD(DAY, -31, @today)
                                        WHEN 'gt60' THEN DATEADD(DAY, -61, @today) END;

    ;WITH matching AS
    (
        SELECT l.Id
        FROM dbo.Leads l
        JOIN dbo.Customers cu ON cu.Id = l.CustomerId
        JOIN dbo.SalesUsers gb ON gb.Id = l.GeneratedBySalesUserId
        JOIN dbo.SalesUsers asg ON asg.Id = l.AssignedSalesUserId
        WHERE l.IsDeleted = 0
          AND (@LeadPublicId IS NULL OR l.PublicId = @LeadPublicId)
          AND (@StatusId IS NULL OR l.StatusId = @StatusId)
          AND (@ProductId IS NULL OR l.ProductId = @ProductId)
          AND (@assignedId IS NULL OR l.AssignedSalesUserId = @assignedId)
          AND (@generatedById IS NULL OR l.GeneratedBySalesUserId = @generatedById)
          AND (@agingFrom IS NULL OR l.LeadDate >= @agingFrom)
          AND (@agingTo IS NULL OR l.LeadDate <= @agingTo)
          AND (@From IS NULL OR l.LeadDate >= @From)
          AND (@To IS NULL OR l.LeadDate <= @To)
          AND (ISNULL(@WithoutOrder, 0) = 0 OR l.SaleId IS NULL)
          AND (@Search IS NULL OR l.LeadNumber LIKE @Search ESCAPE '\' OR cu.Name LIKE @Search ESCAPE '\'
               OR l.ClientName LIKE @Search ESCAPE '\' OR l.SecondaryClientName LIKE @Search ESCAPE '\'
               OR CONCAT(gb.FirstName, N' ', gb.LastName) LIKE @Search ESCAPE '\'
               OR CONCAT(asg.FirstName, N' ', asg.LastName) LIKE @Search ESCAPE '\')
    )
    SELECT @TotalCount = COUNT(*) FROM matching
    OPTION (RECOMPILE);

    SELECT l.PublicId, l.LeadNumber, cu.PublicId AS CustomerPublicId, cu.Name AS CompanyName,
           l.ClientName, l.ClientDesignation, l.ClientEmail, l.ClientPhone,
           l.SecondaryClientName, l.SecondaryClientDesignation, l.SecondaryClientEmail, l.SecondaryClientPhone,
           l.ProductId, pr.Name AS ProductName, l.SourceId, so.Name AS SourceName, l.Requirement,
           l.StatusId, st.Code AS StatusCode, st.Name AS StatusName, l.TypeId, ty.Name AS TypeName,
           gb.PublicId AS GeneratedByPublicId, CONCAT(gb.FirstName, N' ', gb.LastName) AS GeneratedByName,
           asg.PublicId AS AssignedPublicId, CONCAT(asg.FirstName, N' ', asg.LastName) AS AssignedName,
           l.MonthlyExpectedRevenue, l.BillingCycleId, bc.Name AS BillingCycleName,
           l.NextFollowUpDate, l.LeadDate, l.ClosingDate, l.Remarks,
           DATEDIFF(DAY, l.LeadDate, @today) AS AgingDays,
           s.PublicId AS SalePublicId, s.SaleNumber,
           CONCAT(cb.FirstName, N' ', cb.LastName) AS CreatedByName, l.CreatedAtUtc, l.UpdatedAtUtc,
           CAST(x.CanEdit AS BIT) AS CanEdit,
           CAST(CASE WHEN x.CanEdit = 1 AND @isManager = 1 THEN 1 ELSE 0 END AS BIT) AS CanDelete,
           CAST(CASE WHEN st.Code = 'WON' AND l.SaleId IS NULL AND (x.CanEdit = 1 OR l.AssignedSalesUserId = @ActorSalesUserId) THEN 1 ELSE 0 END AS BIT) AS CanOrder
    FROM dbo.Leads l
    JOIN dbo.Customers cu ON cu.Id = l.CustomerId
    JOIN dbo.SalesUsers gb ON gb.Id = l.GeneratedBySalesUserId
    JOIN dbo.SalesUsers asg ON asg.Id = l.AssignedSalesUserId
    JOIN dbo.SalesUsers cb ON cb.Id = l.CreatedBySalesUserId
    JOIN dbo.LeadStatuses st ON st.Id = l.StatusId
    JOIN dbo.LeadTypes ty ON ty.Id = l.TypeId
    LEFT JOIN dbo.LeadProducts pr ON pr.Id = l.ProductId
    LEFT JOIN dbo.LeadSources so ON so.Id = l.SourceId
    LEFT JOIN dbo.LeadBillingCycles bc ON bc.Id = l.BillingCycleId
    LEFT JOIN dbo.Sales s ON s.Id = l.SaleId
    CROSS APPLY (SELECT CASE WHEN l.CreatedBySalesUserId = @ActorSalesUserId OR cb.ManagerId = @ActorSalesUserId THEN 1 ELSE 0 END AS CanEdit) x
    WHERE l.IsDeleted = 0
      AND (@LeadPublicId IS NULL OR l.PublicId = @LeadPublicId)
      AND (@StatusId IS NULL OR l.StatusId = @StatusId)
      AND (@ProductId IS NULL OR l.ProductId = @ProductId)
      AND (@assignedId IS NULL OR l.AssignedSalesUserId = @assignedId)
      AND (@generatedById IS NULL OR l.GeneratedBySalesUserId = @generatedById)
      AND (@agingFrom IS NULL OR l.LeadDate >= @agingFrom)
      AND (@agingTo IS NULL OR l.LeadDate <= @agingTo)
      AND (@From IS NULL OR l.LeadDate >= @From)
      AND (@To IS NULL OR l.LeadDate <= @To)
      AND (ISNULL(@WithoutOrder, 0) = 0 OR l.SaleId IS NULL)
      AND (@Search IS NULL OR l.LeadNumber LIKE @Search ESCAPE '\' OR cu.Name LIKE @Search ESCAPE '\'
           OR l.ClientName LIKE @Search ESCAPE '\' OR l.SecondaryClientName LIKE @Search ESCAPE '\'
           OR CONCAT(gb.FirstName, N' ', gb.LastName) LIKE @Search ESCAPE '\'
           OR CONCAT(asg.FirstName, N' ', asg.LastName) LIKE @Search ESCAPE '\')
    ORDER BY
        CASE WHEN @SortBy = 'leadNumber' AND @desc = 0 THEN l.Id END ASC,
        CASE WHEN @SortBy = 'leadNumber' AND @desc = 1 THEN l.Id END DESC,
        CASE WHEN @SortBy = 'leadDate'   AND @desc = 0 THEN l.LeadDate END ASC,
        CASE WHEN @SortBy = 'leadDate'   AND @desc = 1 THEN l.LeadDate END DESC,
        CASE WHEN @SortBy = 'aging'      AND @desc = 0 THEN l.LeadDate END DESC,
        CASE WHEN @SortBy = 'aging'      AND @desc = 1 THEN l.LeadDate END ASC,
        CASE WHEN @SortBy = 'company'    AND @desc = 0 THEN cu.Name END ASC,
        CASE WHEN @SortBy = 'company'    AND @desc = 1 THEN cu.Name END DESC,
        CASE WHEN @SortBy = 'revenue'    AND @desc = 0 THEN l.MonthlyExpectedRevenue END ASC,
        CASE WHEN @SortBy = 'revenue'    AND @desc = 1 THEN l.MonthlyExpectedRevenue END DESC,
        l.Id DESC
    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
    OPTION (RECOMPILE);
END
GO

-- --------------------------------------------------------------------------------------------
-- StoredProcedures/usp_Lead_Save.sql
-- --------------------------------------------------------------------------------------------
-- User Panel > Lead Records: adds a lead (@LeadPublicId NULL) or changes one (DDL 035).
--   Change: only the person who created the lead or that person's (current) manager; a deleted lead cannot be changed;
--   the company cannot be changed.
--   Lists: product, source and billing cycle must be active values (a value turned off later may stay on the lead);
--   status and lead type from their fixed tables. Lead generated by / assigned person: active sales users (persons or
--   managers); one who was switched off later may stay on the lead.
-- Raises 50404 (lead / company not found) or 50400 (anything not allowed). Returns the lead's PublicId.
CREATE OR ALTER PROCEDURE dbo.usp_Lead_Save
    @LeadPublicId                 UNIQUEIDENTIFIER = NULL,
    @ActorSalesUserId             BIGINT,
    @CustomerPublicId             UNIQUEIDENTIFIER,
    @ClientName                   NVARCHAR(150)  = NULL,
    @ClientDesignation            NVARCHAR(100)  = NULL,
    @ClientEmail                  NVARCHAR(256)  = NULL,
    @ClientPhone                  NVARCHAR(20)   = NULL,
    @SecondaryClientName          NVARCHAR(150)  = NULL,
    @SecondaryClientDesignation   NVARCHAR(100)  = NULL,
    @SecondaryClientEmail         NVARCHAR(256)  = NULL,
    @SecondaryClientPhone         NVARCHAR(20)   = NULL,
    @ProductId                    INT            = NULL,
    @SourceId                     INT            = NULL,
    @Requirement                  NVARCHAR(2000) = NULL,
    @StatusId                     INT,
    @TypeId                       INT,
    @GeneratedByPublicId          UNIQUEIDENTIFIER,
    @AssignedPublicId             UNIQUEIDENTIFIER,
    @MonthlyExpectedRevenue       DECIMAL(18,2)  = NULL,
    @BillingCycleId               INT            = NULL,
    @NextFollowUpDate             DATE           = NULL,
    @LeadDate                     DATE,
    @ClosingDate                  DATE           = NULL,
    @Remarks                      NVARCHAR(2000) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.SalesUsers WHERE Id = @ActorSalesUserId AND IsDeleted = 0 AND Status = 'Active')
        THROW 50400, 'Your account is not active', 1;

    -- The lead being changed, as it is now.
    DECLARE @leadId BIGINT, @curCustomerId BIGINT, @curProductId INT, @curSourceId INT, @curBillingCycleId INT,
            @curGeneratedById BIGINT, @curAssignedId BIGINT, @creatorId BIGINT;
    IF @LeadPublicId IS NOT NULL
    BEGIN
        SELECT @leadId = Id, @curCustomerId = CustomerId, @curProductId = ProductId, @curSourceId = SourceId,
               @curBillingCycleId = BillingCycleId, @curGeneratedById = GeneratedBySalesUserId,
               @curAssignedId = AssignedSalesUserId, @creatorId = CreatedBySalesUserId
        FROM dbo.Leads WHERE PublicId = @LeadPublicId AND IsDeleted = 0;
        IF @leadId IS NULL
            THROW 50404, 'Lead not found', 1;
        IF @creatorId <> @ActorSalesUserId
           AND NOT EXISTS (SELECT 1 FROM dbo.SalesUsers WHERE Id = @creatorId AND ManagerId = @ActorSalesUserId)
            THROW 50400, 'Only the person who created this lead and their manager can change it', 1;
    END

    -- Company: chosen when the lead is added, then fixed.
    DECLARE @customerId BIGINT, @customerActive BIT;
    SELECT @customerId = Id, @customerActive = IsActive FROM dbo.Customers WHERE PublicId = @CustomerPublicId;
    IF @customerId IS NULL
        THROW 50404, 'Company not found', 1;
    IF @leadId IS NOT NULL AND @customerId <> @curCustomerId
        THROW 50400, 'The company of a lead cannot be changed', 1;
    IF @leadId IS NULL AND @customerActive = 0
        THROW 50400, 'This company is inactive', 1;

    -- Lists.
    IF @ProductId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.LeadProducts WHERE Id = @ProductId AND (IsActive = 1 OR @ProductId = @curProductId))
        THROW 50400, 'Choose a product from the list', 1;
    IF @SourceId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.LeadSources WHERE Id = @SourceId AND (IsActive = 1 OR @SourceId = @curSourceId))
        THROW 50400, 'Choose a lead source from the list', 1;
    IF @BillingCycleId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.LeadBillingCycles WHERE Id = @BillingCycleId AND (IsActive = 1 OR @BillingCycleId = @curBillingCycleId))
        THROW 50400, 'Choose a billing cycle from the list', 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.LeadStatuses WHERE Id = @StatusId)
        THROW 50400, 'Choose a lead status from the list', 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.LeadTypes WHERE Id = @TypeId)
        THROW 50400, 'Choose a lead type from the list', 1;

    -- People.
    DECLARE @generatedById BIGINT, @assignedId BIGINT;
    SELECT @generatedById = Id FROM dbo.SalesUsers
    WHERE PublicId = @GeneratedByPublicId AND IsDeleted = 0 AND (Status = 'Active' OR Id = @curGeneratedById);
    IF @generatedById IS NULL
        THROW 50400, 'Choose who generated the lead from the list of active sales users', 1;
    SELECT @assignedId = Id FROM dbo.SalesUsers
    WHERE PublicId = @AssignedPublicId AND IsDeleted = 0 AND (Status = 'Active' OR Id = @curAssignedId);
    IF @assignedId IS NULL
        THROW 50400, 'Choose the assigned person from the list of active sales users', 1;

    IF @MonthlyExpectedRevenue < 0
        THROW 50400, 'Monthly expected revenue cannot be negative', 1;
    IF @LeadDate IS NULL
        THROW 50400, 'Enter the created date', 1;

    IF @leadId IS NULL
    BEGIN
        DECLARE @n BIGINT = NEXT VALUE FOR dbo.LeadNumberSequence;
        DECLARE @digits VARCHAR(20) = CAST(@n AS VARCHAR(20));
        DECLARE @leadNumber VARCHAR(20) = CONCAT('LD-', CASE WHEN LEN(@digits) < 4 THEN RIGHT('0000' + @digits, 4) ELSE @digits END);
        SET @LeadPublicId = NEWID();

        INSERT dbo.Leads (PublicId, LeadNumber, CustomerId, ClientName, ClientDesignation, ClientEmail, ClientPhone,
                          SecondaryClientName, SecondaryClientDesignation, SecondaryClientEmail, SecondaryClientPhone,
                          ProductId, SourceId, Requirement, StatusId, TypeId, GeneratedBySalesUserId, AssignedSalesUserId,
                          MonthlyExpectedRevenue, BillingCycleId, NextFollowUpDate, LeadDate, ClosingDate, Remarks, CreatedBySalesUserId)
        VALUES (@LeadPublicId, @leadNumber, @customerId, @ClientName, @ClientDesignation, @ClientEmail, @ClientPhone,
                @SecondaryClientName, @SecondaryClientDesignation, @SecondaryClientEmail, @SecondaryClientPhone,
                @ProductId, @SourceId, @Requirement, @StatusId, @TypeId, @generatedById, @assignedId,
                @MonthlyExpectedRevenue, @BillingCycleId, @NextFollowUpDate, @LeadDate, @ClosingDate, @Remarks, @ActorSalesUserId);
    END
    ELSE
        UPDATE dbo.Leads
        SET ClientName = @ClientName, ClientDesignation = @ClientDesignation, ClientEmail = @ClientEmail, ClientPhone = @ClientPhone,
            SecondaryClientName = @SecondaryClientName, SecondaryClientDesignation = @SecondaryClientDesignation,
            SecondaryClientEmail = @SecondaryClientEmail, SecondaryClientPhone = @SecondaryClientPhone,
            ProductId = @ProductId, SourceId = @SourceId, Requirement = @Requirement, StatusId = @StatusId, TypeId = @TypeId,
            GeneratedBySalesUserId = @generatedById, AssignedSalesUserId = @assignedId,
            MonthlyExpectedRevenue = @MonthlyExpectedRevenue, BillingCycleId = @BillingCycleId,
            NextFollowUpDate = @NextFollowUpDate, LeadDate = @LeadDate, ClosingDate = @ClosingDate, Remarks = @Remarks,
            UpdatedBySalesUserId = @ActorSalesUserId, UpdatedAtUtc = SYSUTCDATETIME()
        WHERE Id = @leadId;

    SELECT @LeadPublicId;
END
GO

-- --------------------------------------------------------------------------------------------
-- StoredProcedures/usp_Lead_Summary.sql
-- --------------------------------------------------------------------------------------------
-- User Panel > Home (lead dashboard): every figure in one pass over the leads that are not deleted.
-- @Mine = 1 counts only the leads @ActorSalesUserId created, generated or is assigned to; 0 = every lead (everyone sees all).
-- One row per figure: Section, Id / Code / Name, SortOrder, Total (leads), Won (leads), Revenue (monthly expected revenue
-- of the Won leads, INR).
--   Total    one row: all leads, won leads, won revenue.
--   Status   one row per status (fixed list), zero rows included.
--   Source   one row per lead source with leads, plus "Not set".
--   Aging    < 7, 7-30, 31-60, > 60 days since the created date (India time).
--   Type     one row per lead type: total and won (win %).
--   Product  one row per product with won leads: won count and won revenue (the "product breakdown").
CREATE OR ALTER PROCEDURE dbo.usp_Lead_Summary
    @ActorSalesUserId  BIGINT,
    @Mine              BIT = 0
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @today DATE = CAST(SWITCHOFFSET(SYSDATETIMEOFFSET(), '+05:30') AS DATE);
    DECLARE @won INT = (SELECT Id FROM dbo.LeadStatuses WHERE Code = 'WON');

    ;WITH l AS
    (
        SELECT ld.StatusId, ld.SourceId, ld.TypeId, ld.ProductId,
               CASE WHEN ld.StatusId = @won THEN 1 ELSE 0 END AS IsWon,
               CASE WHEN ld.StatusId = @won THEN ISNULL(ld.MonthlyExpectedRevenue, 0) ELSE 0 END AS WonRevenue,
               DATEDIFF(DAY, ld.LeadDate, @today) AS AgingDays
        FROM dbo.Leads ld
        WHERE ld.IsDeleted = 0
          AND (ISNULL(@Mine, 0) = 0 OR @ActorSalesUserId IN (ld.CreatedBySalesUserId, ld.GeneratedBySalesUserId, ld.AssignedSalesUserId))
    )
    SELECT 'Total' AS Section, 0 AS Id, CAST(NULL AS VARCHAR(20)) AS Code, N'All leads' AS Name, 0 AS SortOrder,
           COUNT(*) AS Total, ISNULL(SUM(IsWon), 0) AS Won, ISNULL(SUM(WonRevenue), 0) AS Revenue
    FROM l
    UNION ALL
    SELECT 'Status', s.Id, s.Code, s.Name, s.SortOrder, COUNT(l.StatusId), ISNULL(SUM(l.IsWon), 0), ISNULL(SUM(l.WonRevenue), 0)
    FROM dbo.LeadStatuses s LEFT JOIN l ON l.StatusId = s.Id
    GROUP BY s.Id, s.Code, s.Name, s.SortOrder
    UNION ALL
    SELECT 'Source', ISNULL(src.Id, 0), NULL, ISNULL(src.Name, N'Not set'), ISNULL(src.SortOrder, 100000), COUNT(*), SUM(l.IsWon), SUM(l.WonRevenue)
    FROM l LEFT JOIN dbo.LeadSources src ON src.Id = l.SourceId
    GROUP BY src.Id, src.Name, src.SortOrder
    UNION ALL
    SELECT 'Aging', b.Id, b.Code, b.Name, b.Id, COUNT(l.AgingDays), ISNULL(SUM(l.IsWon), 0), ISNULL(SUM(l.WonRevenue), 0)
    FROM (VALUES (1, 'lt7', N'< 7 days', -100000, 6), (2, '7to30', N'7-30 days', 7, 30),
                 (3, '31to60', N'31-60 days', 31, 60), (4, 'gt60', N'> 60 days', 61, 100000)) AS b (Id, Code, Name, MinDays, MaxDays)
    LEFT JOIN l ON l.AgingDays BETWEEN b.MinDays AND b.MaxDays
    GROUP BY b.Id, b.Code, b.Name
    UNION ALL
    SELECT 'Type', t.Id, t.Code, t.Name, t.SortOrder, COUNT(l.TypeId), ISNULL(SUM(l.IsWon), 0), ISNULL(SUM(l.WonRevenue), 0)
    FROM dbo.LeadTypes t LEFT JOIN l ON l.TypeId = t.Id
    GROUP BY t.Id, t.Code, t.Name, t.SortOrder
    UNION ALL
    SELECT 'Product', ISNULL(p.Id, 0), NULL, ISNULL(p.Name, N'Not set'), ISNULL(p.SortOrder, 100000), SUM(l.IsWon), SUM(l.IsWon), SUM(l.WonRevenue)
    FROM l LEFT JOIN dbo.LeadProducts p ON p.Id = l.ProductId
    WHERE l.IsWon = 1
    GROUP BY p.Id, p.Name, p.SortOrder
    ORDER BY Section, SortOrder, Name;
END
GO

-- --------------------------------------------------------------------------------------------
-- StoredProcedures/usp_LeadListValue_Save.sql
-- --------------------------------------------------------------------------------------------
-- Admin Panel > Settings > Lead lists: adds (@Id NULL) or changes one value of a lead list (DDL 035).
-- @List: Product (dbo.LeadProducts), Source (dbo.LeadSources) or BillingCycle (dbo.LeadBillingCycles).
-- A value is turned off rather than deleted, so leads that use it keep showing it.
-- Raises 50404 (not found), 50409 (same name already in the list) or 50400. Returns the value's Id.
CREATE OR ALTER PROCEDURE dbo.usp_LeadListValue_Save
    @List         VARCHAR(20),
    @Id           INT            = NULL,
    @Name         NVARCHAR(100),
    @SortOrder    INT            = 0,
    @IsActive     BIT            = 1,
    @ActorUserId  BIGINT         = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF NULLIF(LTRIM(RTRIM(@Name)), N'') IS NULL
        THROW 50400, 'Name is required', 1;

    BEGIN TRAN;

    IF @List = 'Product'
    BEGIN
        IF @Id IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.LeadProducts WITH (UPDLOCK, HOLDLOCK) WHERE Id = @Id)
            THROW 50404, 'List value not found', 1;
        IF EXISTS (SELECT 1 FROM dbo.LeadProducts WITH (UPDLOCK, HOLDLOCK) WHERE Name = @Name AND (@Id IS NULL OR Id <> @Id))
            THROW 50409, 'This list already has a value with the same name', 1;
        IF @Id IS NULL
        BEGIN
            INSERT dbo.LeadProducts (Name, SortOrder, IsActive, UpdatedBy) VALUES (@Name, @SortOrder, @IsActive, @ActorUserId);
            SET @Id = SCOPE_IDENTITY();
        END
        ELSE
            UPDATE dbo.LeadProducts SET Name = @Name, SortOrder = @SortOrder, IsActive = @IsActive, UpdatedAtUtc = SYSUTCDATETIME(), UpdatedBy = @ActorUserId WHERE Id = @Id;
    END
    ELSE IF @List = 'Source'
    BEGIN
        IF @Id IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.LeadSources WITH (UPDLOCK, HOLDLOCK) WHERE Id = @Id)
            THROW 50404, 'List value not found', 1;
        IF EXISTS (SELECT 1 FROM dbo.LeadSources WITH (UPDLOCK, HOLDLOCK) WHERE Name = @Name AND (@Id IS NULL OR Id <> @Id))
            THROW 50409, 'This list already has a value with the same name', 1;
        IF @Id IS NULL
        BEGIN
            INSERT dbo.LeadSources (Name, SortOrder, IsActive, UpdatedBy) VALUES (@Name, @SortOrder, @IsActive, @ActorUserId);
            SET @Id = SCOPE_IDENTITY();
        END
        ELSE
            UPDATE dbo.LeadSources SET Name = @Name, SortOrder = @SortOrder, IsActive = @IsActive, UpdatedAtUtc = SYSUTCDATETIME(), UpdatedBy = @ActorUserId WHERE Id = @Id;
    END
    ELSE IF @List = 'BillingCycle'
    BEGIN
        IF @Id IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.LeadBillingCycles WITH (UPDLOCK, HOLDLOCK) WHERE Id = @Id)
            THROW 50404, 'List value not found', 1;
        IF EXISTS (SELECT 1 FROM dbo.LeadBillingCycles WITH (UPDLOCK, HOLDLOCK) WHERE Name = @Name AND (@Id IS NULL OR Id <> @Id))
            THROW 50409, 'This list already has a value with the same name', 1;
        IF @Id IS NULL
        BEGIN
            INSERT dbo.LeadBillingCycles (Name, SortOrder, IsActive, UpdatedBy) VALUES (@Name, @SortOrder, @IsActive, @ActorUserId);
            SET @Id = SCOPE_IDENTITY();
        END
        ELSE
            UPDATE dbo.LeadBillingCycles SET Name = @Name, SortOrder = @SortOrder, IsActive = @IsActive, UpdatedAtUtc = SYSUTCDATETIME(), UpdatedBy = @ActorUserId WHERE Id = @Id;
    END
    ELSE
        THROW 50400, 'Unknown list', 1;

    COMMIT;

    SELECT CAST(@Id AS BIGINT);
END
GO

-- --------------------------------------------------------------------------------------------
-- StoredProcedures/usp_Sale_AdminGetPaged.sql
-- --------------------------------------------------------------------------------------------
-- Admin Panel: one page of ALL sales, plus totals of the matching active sales.
--   @SellerPublicId:  sales made by one sales person (or manager).
--   @ManagerPublicId: manager-wise = the manager's own sales plus the sales of the people who reported to them when
--                     the sale was made (Sales.ManagerId).
-- @Search is a ready LIKE pattern ('\' escape) on sale number, customer and product; @Status Active / Cancelled / NULL;
-- @SoldFrom / @SoldTo filter on the day the sale was recorded (UTC). @SortBy: soldAt, saleNumber, customer, product,
-- seller, saleAmount, renewalDate. Column order is the same as the Admin API's single-sale query.
CREATE OR ALTER PROCEDURE dbo.usp_Sale_AdminGetPaged
    @SellerPublicId    UNIQUEIDENTIFIER = NULL,
    @ManagerPublicId   UNIQUEIDENTIFIER = NULL,
    @CustomerPublicId  UNIQUEIDENTIFIER = NULL,
    @Search            NVARCHAR(210)    = NULL,
    @Status            VARCHAR(20)      = NULL,
    @SoldFrom          DATE             = NULL,
    @SoldTo            DATE             = NULL,
    @SortBy            VARCHAR(20)      = 'soldAt',
    @SortDirection     VARCHAR(4)       = 'DESC',
    @Offset            INT              = 0,
    @PageSize          INT              = 20,
    @TotalCount        INT              OUTPUT,
    @TotalSaleAmount   DECIMAL(18,2)    OUTPUT,
    @TotalMargin       DECIMAL(18,2)    OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @desc BIT = CASE WHEN @SortDirection = 'DESC' THEN 1 ELSE 0 END;
    DECLARE @sellerId BIGINT = NULL, @managerId BIGINT = NULL, @customerId BIGINT = NULL;
    IF @SellerPublicId IS NOT NULL SET @sellerId = ISNULL((SELECT Id FROM dbo.SalesUsers WHERE PublicId = @SellerPublicId), -1);
    IF @ManagerPublicId IS NOT NULL SET @managerId = ISNULL((SELECT Id FROM dbo.SalesUsers WHERE PublicId = @ManagerPublicId), -1);
    IF @CustomerPublicId IS NOT NULL SET @customerId = ISNULL((SELECT Id FROM dbo.Customers WHERE PublicId = @CustomerPublicId), -1);
    DECLARE @fromUtc DATETIME2(3) = CAST(@SoldFrom AS DATETIME2(3));
    DECLARE @toUtc DATETIME2(3) = DATEADD(DAY, 1, CAST(@SoldTo AS DATETIME2(3)));

    SELECT @TotalCount = COUNT(*),
           @TotalSaleAmount = ISNULL(SUM(CASE WHEN s.Status = 'Active' THEN s.SaleAmount END), 0),
           @TotalMargin = ISNULL(SUM(CASE WHEN s.Status = 'Active' THEN s.SaleAmount - s.CostAmount END), 0)
    FROM dbo.Sales s
    JOIN dbo.Customers cu ON cu.Id = s.CustomerId
    JOIN dbo.Products p ON p.Id = s.ProductId
    WHERE (@sellerId IS NULL OR s.SalesUserId = @sellerId)
      AND (@managerId IS NULL OR s.ManagerId = @managerId OR s.SalesUserId = @managerId)
      AND (@customerId IS NULL OR s.CustomerId = @customerId)
      AND (@Status IS NULL OR s.Status = @Status)
      AND (@fromUtc IS NULL OR s.CreatedAtUtc >= @fromUtc)
      AND (@toUtc IS NULL OR s.CreatedAtUtc < @toUtc)
      AND (@Search IS NULL OR s.SaleNumber LIKE @Search ESCAPE '\' OR cu.Name LIKE @Search ESCAPE '\' OR p.Name LIKE @Search ESCAPE '\')
    OPTION (RECOMPILE);

    SELECT s.PublicId, s.SaleNumber, cu.PublicId AS CustomerPublicId, cu.Name AS CustomerName, c.Name AS CategoryName,
           p.Name AS ProductName, pl.Name AS PlanName,
           su.PublicId AS SellerPublicId, CONCAT(su.FirstName, N' ', su.LastName) AS SellerName, su.Role AS SellerRole,
           CASE WHEN mg.Id IS NULL THEN NULL ELSE CONCAT(mg.FirstName, N' ', mg.LastName) END AS ManagerName,
           s.Quantity, s.UnitPrice, s.UnitCost, s.ListAmount, s.SaleAmount, s.DiscountAmount, s.CostAmount,
           s.DurationUnit, s.DurationCount, s.StartDate, s.RenewalDate, s.Status, s.Notes, s.CreatedAtUtc,
           s.CancelledAtUtc, CASE WHEN cb.Id IS NULL THEN NULL ELSE CONCAT(cb.FirstName, N' ', cb.LastName) END AS CancelledByName, s.CancelReason, s.GstPercent, s.TaxAmount, s.TotalAmount, s.FreeMonths,
           -- Order form tracking status (DDL 036): a cancelled order shows Cancelled; NULL for a sale from the simple page.
           CASE WHEN s.BillingFrequencyId IS NULL THEN NULL WHEN s.Status = 'Cancelled' THEN 'CANCELLED' ELSE ts.Code END AS TrackingCode,
           CASE WHEN s.BillingFrequencyId IS NULL THEN NULL WHEN s.Status = 'Cancelled' THEN N'Cancelled' ELSE ts.Name END AS TrackingName
    FROM dbo.Sales s
    LEFT JOIN dbo.OrderTrackingStatuses ts ON ts.Id = s.TrackingStatusId
    JOIN dbo.Customers cu ON cu.Id = s.CustomerId
    JOIN dbo.ProductCategories c ON c.Id = s.CategoryId
    JOIN dbo.Products p ON p.Id = s.ProductId
    JOIN dbo.ProductPlans pl ON pl.Id = s.PlanId
    JOIN dbo.SalesUsers su ON su.Id = s.SalesUserId
    LEFT JOIN dbo.SalesUsers mg ON mg.Id = s.ManagerId
    LEFT JOIN dbo.SalesUsers cb ON cb.Id = s.CancelledBySalesUserId
    WHERE (@sellerId IS NULL OR s.SalesUserId = @sellerId)
      AND (@managerId IS NULL OR s.ManagerId = @managerId OR s.SalesUserId = @managerId)
      AND (@customerId IS NULL OR s.CustomerId = @customerId)
      AND (@Status IS NULL OR s.Status = @Status)
      AND (@fromUtc IS NULL OR s.CreatedAtUtc >= @fromUtc)
      AND (@toUtc IS NULL OR s.CreatedAtUtc < @toUtc)
      AND (@Search IS NULL OR s.SaleNumber LIKE @Search ESCAPE '\' OR cu.Name LIKE @Search ESCAPE '\' OR p.Name LIKE @Search ESCAPE '\')
    ORDER BY
        CASE WHEN @SortBy = 'soldAt'      AND @desc = 0 THEN s.CreatedAtUtc END ASC,
        CASE WHEN @SortBy = 'soldAt'      AND @desc = 1 THEN s.CreatedAtUtc END DESC,
        CASE WHEN @SortBy = 'saleNumber'  AND @desc = 0 THEN s.SaleNumber   END ASC,
        CASE WHEN @SortBy = 'saleNumber'  AND @desc = 1 THEN s.SaleNumber   END DESC,
        CASE WHEN @SortBy = 'customer'    AND @desc = 0 THEN cu.Name        END ASC,
        CASE WHEN @SortBy = 'customer'    AND @desc = 1 THEN cu.Name        END DESC,
        CASE WHEN @SortBy = 'product'     AND @desc = 0 THEN p.Name         END ASC,
        CASE WHEN @SortBy = 'product'     AND @desc = 1 THEN p.Name         END DESC,
        CASE WHEN @SortBy = 'seller'      AND @desc = 0 THEN su.FirstName   END ASC,
        CASE WHEN @SortBy = 'seller'      AND @desc = 1 THEN su.FirstName   END DESC,
        CASE WHEN @SortBy = 'saleAmount'  AND @desc = 0 THEN s.SaleAmount   END ASC,
        CASE WHEN @SortBy = 'saleAmount'  AND @desc = 1 THEN s.SaleAmount   END DESC,
        CASE WHEN @SortBy = 'renewalDate' AND @desc = 0 THEN s.RenewalDate  END ASC,
        CASE WHEN @SortBy = 'renewalDate' AND @desc = 1 THEN s.RenewalDate  END DESC,
        s.Id DESC
    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
    OPTION (RECOMPILE);
END
GO

-- --------------------------------------------------------------------------------------------
-- StoredProcedures/usp_Sale_GetPaged.sql
-- --------------------------------------------------------------------------------------------
-- One page of sales for a sales user, plus totals.
--   @Scope 'mine': sales made by @SalesUserId.
--   @Scope 'team': sales made by the people @SalesUserId manages (the sale's ManagerId); @SellerPublicId narrows to one person.
-- @Search is a ready LIKE pattern ('\' escape) matched against sale number, company, product and the lead number the
-- order was made from; @Status is Active / Cancelled / NULL.
-- @SoldFrom / @SoldTo filter on the date the sale was recorded (UTC). @SortBy: soldAt, saleNumber, customer, product, saleAmount, renewalDate.
-- @TotalSaleAmount and @TotalMargin cover the matching ACTIVE sales (cancelled sales are not counted).
-- Column order is the same as the API's single-sale query; the last columns say whether a cancellation request is waiting
-- and whether the sale was saved with the New order form (it can then be edited).
CREATE OR ALTER PROCEDURE dbo.usp_Sale_GetPaged
    @SalesUserId      BIGINT,
    @Scope            VARCHAR(10)      = 'mine',
    @SellerPublicId   UNIQUEIDENTIFIER = NULL,
    @Search           NVARCHAR(210)    = NULL,
    @Status           VARCHAR(20)      = NULL,
    @SoldFrom         DATE             = NULL,
    @SoldTo           DATE             = NULL,
    @SortBy           VARCHAR(20)      = 'soldAt',
    @SortDirection    VARCHAR(4)       = 'DESC',
    @Offset           INT              = 0,
    @PageSize         INT              = 20,
    @TotalCount       INT              OUTPUT,
    @TotalSaleAmount  DECIMAL(18,2)    OUTPUT,
    @TotalMargin      DECIMAL(18,2)    OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @desc BIT = CASE WHEN @SortDirection = 'DESC' THEN 1 ELSE 0 END;
    DECLARE @team BIT = CASE WHEN @Scope = 'team' THEN 1 ELSE 0 END;
    DECLARE @sellerId BIGINT = NULL;
    IF @SellerPublicId IS NOT NULL
        SET @sellerId = ISNULL((SELECT Id FROM dbo.SalesUsers WHERE PublicId = @SellerPublicId), -1); -- unknown: empty result
    DECLARE @fromUtc DATETIME2(3) = CAST(@SoldFrom AS DATETIME2(3));
    DECLARE @toUtc DATETIME2(3) = DATEADD(DAY, 1, CAST(@SoldTo AS DATETIME2(3)));

    SELECT @TotalCount = COUNT(*),
           @TotalSaleAmount = ISNULL(SUM(CASE WHEN s.Status = 'Active' THEN s.SaleAmount END), 0),
           @TotalMargin = ISNULL(SUM(CASE WHEN s.Status = 'Active' THEN s.SaleAmount - s.CostAmount END), 0)
    FROM dbo.Sales s
    JOIN dbo.Customers cu ON cu.Id = s.CustomerId
    JOIN dbo.Products p ON p.Id = s.ProductId
    WHERE ((@team = 0 AND s.SalesUserId = @SalesUserId) OR (@team = 1 AND s.ManagerId = @SalesUserId))
      AND (@sellerId IS NULL OR s.SalesUserId = @sellerId)
      AND (@Status IS NULL OR s.Status = @Status)
      AND (@fromUtc IS NULL OR s.CreatedAtUtc >= @fromUtc)
      AND (@toUtc IS NULL OR s.CreatedAtUtc < @toUtc)
      AND (@Search IS NULL OR s.SaleNumber LIKE @Search ESCAPE '\' OR cu.Name LIKE @Search ESCAPE '\' OR p.Name LIKE @Search ESCAPE '\'
           OR EXISTS (SELECT 1 FROM dbo.Leads l WHERE l.SaleId = s.Id AND l.LeadNumber LIKE @Search ESCAPE '\'))
    OPTION (RECOMPILE);

    SELECT s.PublicId, s.SaleNumber, cu.PublicId AS CustomerPublicId, cu.Name AS CustomerName, c.Name AS CategoryName,
           p.Name AS ProductName, pl.Name AS PlanName,
           su.PublicId AS SellerPublicId, CONCAT(su.FirstName, N' ', su.LastName) AS SellerName,
           s.Quantity, s.UnitPrice, s.UnitCost, s.ListAmount, s.SaleAmount, s.DiscountAmount, s.CostAmount,
           s.DurationUnit, s.DurationCount, s.StartDate, s.RenewalDate, s.Status, s.Notes, s.CreatedAtUtc,
           s.CancelledAtUtc, CASE WHEN cb.Id IS NULL THEN NULL ELSE CONCAT(cb.FirstName, N' ', cb.LastName) END AS CancelledByName, s.CancelReason,
           CAST(CASE WHEN EXISTS (SELECT 1 FROM dbo.SaleCancelRequests r WHERE r.SaleId = s.Id AND r.Status = 'Pending') THEN 1 ELSE 0 END AS BIT) AS HasPendingCancelRequest,
           CAST(CASE WHEN s.BillingFrequencyId IS NOT NULL THEN 1 ELSE 0 END AS BIT) AS HasOrderForm,
           -- Order forms list: tracking status (a cancelled order shows Cancelled), total with GST, free months, its lead.
           s.TrackingStatusId,
           CASE WHEN s.BillingFrequencyId IS NULL THEN NULL WHEN s.Status = 'Cancelled' THEN 'CANCELLED' ELSE ts.Code END AS TrackingCode,
           CASE WHEN s.BillingFrequencyId IS NULL THEN NULL WHEN s.Status = 'Cancelled' THEN N'Cancelled' ELSE ts.Name END AS TrackingName,
           s.FreeMonths, s.TotalAmount, ld.LeadNumber, ld.PublicId AS LeadPublicId
    FROM dbo.Sales s
    LEFT JOIN dbo.OrderTrackingStatuses ts ON ts.Id = s.TrackingStatusId
    OUTER APPLY (SELECT TOP 1 l.LeadNumber, l.PublicId FROM dbo.Leads l WHERE l.SaleId = s.Id) ld
    JOIN dbo.Customers cu ON cu.Id = s.CustomerId
    JOIN dbo.ProductCategories c ON c.Id = s.CategoryId
    JOIN dbo.Products p ON p.Id = s.ProductId
    JOIN dbo.ProductPlans pl ON pl.Id = s.PlanId
    JOIN dbo.SalesUsers su ON su.Id = s.SalesUserId
    LEFT JOIN dbo.SalesUsers cb ON cb.Id = s.CancelledBySalesUserId
    WHERE ((@team = 0 AND s.SalesUserId = @SalesUserId) OR (@team = 1 AND s.ManagerId = @SalesUserId))
      AND (@sellerId IS NULL OR s.SalesUserId = @sellerId)
      AND (@Status IS NULL OR s.Status = @Status)
      AND (@fromUtc IS NULL OR s.CreatedAtUtc >= @fromUtc)
      AND (@toUtc IS NULL OR s.CreatedAtUtc < @toUtc)
      AND (@Search IS NULL OR s.SaleNumber LIKE @Search ESCAPE '\' OR cu.Name LIKE @Search ESCAPE '\' OR p.Name LIKE @Search ESCAPE '\'
           OR EXISTS (SELECT 1 FROM dbo.Leads l WHERE l.SaleId = s.Id AND l.LeadNumber LIKE @Search ESCAPE '\'))
    ORDER BY
        CASE WHEN @SortBy = 'soldAt'      AND @desc = 0 THEN s.CreatedAtUtc END ASC,
        CASE WHEN @SortBy = 'soldAt'      AND @desc = 1 THEN s.CreatedAtUtc END DESC,
        CASE WHEN @SortBy = 'saleNumber'  AND @desc = 0 THEN s.SaleNumber   END ASC,
        CASE WHEN @SortBy = 'saleNumber'  AND @desc = 1 THEN s.SaleNumber   END DESC,
        CASE WHEN @SortBy = 'customer'    AND @desc = 0 THEN cu.Name        END ASC,
        CASE WHEN @SortBy = 'customer'    AND @desc = 1 THEN cu.Name        END DESC,
        CASE WHEN @SortBy = 'product'     AND @desc = 0 THEN p.Name         END ASC,
        CASE WHEN @SortBy = 'product'     AND @desc = 1 THEN p.Name         END DESC,
        CASE WHEN @SortBy = 'saleAmount'  AND @desc = 0 THEN s.SaleAmount   END ASC,
        CASE WHEN @SortBy = 'saleAmount'  AND @desc = 1 THEN s.SaleAmount   END DESC,
        CASE WHEN @SortBy = 'renewalDate' AND @desc = 0 THEN s.RenewalDate  END ASC,
        CASE WHEN @SortBy = 'renewalDate' AND @desc = 1 THEN s.RenewalDate  END DESC,
        s.Id DESC
    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
    OPTION (RECOMPILE);
END
GO

-- --------------------------------------------------------------------------------------------
-- StoredProcedures/usp_SaleCancelRequest_GetPaged.sql
-- --------------------------------------------------------------------------------------------
-- User Panel: one page of cancellation requests. A sales manager (@IsManager = 1) sees the requests on their team's
-- sales (the sale's ManagerId); a sales person sees their own requests. @Status: Pending / Approved / Rejected / NULL (all).
-- Waiting requests first, then newest first. Paging happens here, never in the browser.
CREATE OR ALTER PROCEDURE dbo.usp_SaleCancelRequest_GetPaged
    @SalesUserId  BIGINT,
    @IsManager    BIT,
    @Status       VARCHAR(20) = NULL,
    @Offset       INT         = 0,
    @PageSize     INT         = 20,
    @TotalCount   INT         OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT @TotalCount = COUNT(*)
    FROM dbo.SaleCancelRequests r
    JOIN dbo.Sales s ON s.Id = r.SaleId
    WHERE (@Status IS NULL OR r.Status = @Status)
      AND ((@IsManager = 1 AND s.ManagerId = @SalesUserId) OR (@IsManager = 0 AND r.RequestedBySalesUserId = @SalesUserId))
    OPTION (RECOMPILE);

    SELECT r.PublicId, s.PublicId AS SalePublicId, s.SaleNumber, cu.Name AS CustomerName, p.Name AS ProductName, s.SaleAmount,
           rb.PublicId AS RequestedByPublicId, CONCAT(rb.FirstName, N' ', rb.LastName) AS RequestedByName,
           r.Reason, r.Status, r.RequestedAtUtc,
           CASE WHEN db.Id IS NULL THEN NULL ELSE CONCAT(db.FirstName, N' ', db.LastName) END AS DecidedByName,
           r.DecidedAtUtc, r.DecisionNote
    FROM dbo.SaleCancelRequests r
    JOIN dbo.Sales s ON s.Id = r.SaleId
    JOIN dbo.Customers cu ON cu.Id = s.CustomerId
    JOIN dbo.Products p ON p.Id = s.ProductId
    JOIN dbo.SalesUsers rb ON rb.Id = r.RequestedBySalesUserId
    LEFT JOIN dbo.SalesUsers db ON db.Id = r.DecidedBySalesUserId
    WHERE (@Status IS NULL OR r.Status = @Status)
      AND ((@IsManager = 1 AND s.ManagerId = @SalesUserId) OR (@IsManager = 0 AND r.RequestedBySalesUserId = @SalesUserId))
    ORDER BY CASE WHEN r.Status = 'Pending' THEN 0 ELSE 1 END, r.RequestedAtUtc DESC, r.Id DESC
    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
    OPTION (RECOMPILE);
END
GO

-- --------------------------------------------------------------------------------------------
-- StoredProcedures/usp_SaleOrder_Create.sql
-- --------------------------------------------------------------------------------------------
-- User Panel "New order form": records the sale through dbo.usp_Sale_Create (same price-list rules, same Sales row) and,
-- in the same transaction, adds the order-form details (DDL 029):
--   @GstPercent 0-28 -> TaxAmount = SaleAmount x GST.   @FreeMonths 0 to Settings Sales.MaxFreeMonths: added to the
--   renewal date at the same price (unless @RenewalDate is given).   Billing frequency / method and payment method / term:
--   (and currency) active values of the right list in dbo.MasterValues.   Contacts and bill-to address are stored as given (the API
--   checks their format).
-- Price: one plan term per unit = the seat range's price for seat-priced plans (DDL 032: every seat at the price of the
--   range the quantity falls in, financial year of the order date) or the plan's own price; times the number of plan
--   terms paid for (24 paid months of a 1-year plan = x2). The sale/discount and the company discount limit are checked
--   against that list amount. UnitPrice / UnitCost are stored for the whole paid term.
-- Raises 50404 / 50400 like usp_Sale_Create. Returns the new sale's PublicId.
CREATE OR ALTER PROCEDURE dbo.usp_SaleOrder_Create
    @SalesUserId                BIGINT,
    @CustomerPublicId           UNIQUEIDENTIFIER,
    @PlanId                     BIGINT,
    @Quantity                   INT,
    @SaleAmount                 DECIMAL(18,2)  = NULL,
    @DiscountAmount             DECIMAL(18,2)  = NULL,
    @DurationUnit               VARCHAR(10)    = NULL,
    @DurationCount              INT            = NULL,
    @StartDate                  DATE,
    @RenewalDate                DATE           = NULL,
    @Notes                      NVARCHAR(1000) = NULL,
    @GstPercent                 DECIMAL(5,2),
    @FreeMonths                 INT            = 0,
    @BillingFrequencyId         INT,
    @BillingMethodId            INT,
    @PaymentMethodId            INT,
    @PaymentTermId              INT,
    @CurrencyId                 INT,
    @PrimaryContactName         NVARCHAR(150),
    @PrimaryContactDesignation  NVARCHAR(100)  = NULL,
    @PrimaryContactEmail        NVARCHAR(256)  = NULL,
    @PrimaryContactPhone        NVARCHAR(20)   = NULL,
    @BillingContactName         NVARCHAR(150)  = NULL,
    @BillingContactDesignation  NVARCHAR(100)  = NULL,
    @BillingContactEmail        NVARCHAR(256)  = NULL,
    @BillingContactPhone        NVARCHAR(20)   = NULL,
    @BillToAddress              NVARCHAR(500)  = NULL,
    @OrderWording               NVARCHAR(MAX)  = NULL,  -- JSON: the order form's wording sections (DDL 031)
    @IncludedItemQuantity       INT            = NULL,  -- quantity of the plan's included item, e.g. Team Link Extend seats (DDL 034)
    @TrackingStatusId           INT            = 1      -- Draft / Sent to DocuSign / Signed (DDL 036); a new order is never Cancelled
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    -- Order-form details are checked first, so nothing is saved when one is wrong.
    IF @GstPercent IS NULL OR @GstPercent < 0 OR @GstPercent > 28
        THROW 50400, 'GST must be between 0 and 28 percent', 1;

    DECLARE @free INT = ISNULL(@FreeMonths, 0);
    DECLARE @maxFree INT = ISNULL(TRY_CAST((SELECT SettingValue FROM dbo.Settings WHERE SettingKey = 'Sales.MaxFreeMonths') AS INT), 12);
    IF @free < 0 OR @free > @maxFree
    BEGIN
        DECLARE @freeMessage NVARCHAR(200) = CONCAT(N'Free months must be between 0 and ', @maxFree, N' (company setting)');
        THROW 50400, @freeMessage, 1;
    END

    IF NOT EXISTS (SELECT 1 FROM dbo.MasterValues WHERE Id = @BillingFrequencyId AND MasterType = 'BillingFrequency' AND IsActive = 1)
        THROW 50400, 'Choose a billing frequency from the list', 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.MasterValues WHERE Id = @BillingMethodId AND MasterType = 'BillingMethod' AND IsActive = 1)
        THROW 50400, 'Choose a billing method from the list', 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.MasterValues WHERE Id = @PaymentMethodId AND MasterType = 'PaymentMethod' AND IsActive = 1)
        THROW 50400, 'Choose a payment method from the list', 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.MasterValues WHERE Id = @PaymentTermId AND MasterType = 'PaymentTerm' AND IsActive = 1)
        THROW 50400, 'Choose a payment term from the list', 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.MasterValues WHERE Id = @CurrencyId AND MasterType = 'Currency' AND IsActive = 1)
        THROW 50400, 'Choose a currency from the list', 1;
    IF @OrderWording IS NOT NULL AND ISJSON(@OrderWording) = 0
        THROW 50400, 'The order wording is not valid', 1;
    IF @IncludedItemQuantity IS NOT NULL AND (@IncludedItemQuantity < 1 OR @IncludedItemQuantity > 10000000)
        THROW 50400, 'The Team Link Extend seat quantity must be between 1 and 10000000', 1;
    IF NULLIF(LTRIM(RTRIM(@PrimaryContactName)), N'') IS NULL
        THROW 50400, 'Enter the primary contact''s name', 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.OrderTrackingStatuses WHERE Id = ISNULL(@TrackingStatusId, 1) AND Code <> 'CANCELLED')
        THROW 50400, 'Choose Draft, Sent to DocuSign or Signed for a new order', 1;

    -- Price of one plan term (per unit): the seat range's for seat-priced plans (financial year of the order date, India
    -- time; FY27 = Apr 2026 - Mar 2027), otherwise the plan's own. The paid term may be several plan terms: 24 paid months
    -- of a 1-year plan = 2 terms, so the unit price and cost are multiplied by paid months / plan months.
    DECLARE @planPrice DECIMAL(18,2), @planCost DECIMAL(18,2), @planUnit VARCHAR(10), @planCount INT;
    SELECT @planPrice = Price, @planCost = Cost, @planUnit = DurationUnit, @planCount = DurationCount FROM dbo.ProductPlans WHERE Id = @PlanId;
    IF @planPrice IS NULL
        THROW 50404, 'Plan not found', 1;
    IF @Quantity IS NULL OR @Quantity < 1 OR @Quantity > 100000
        THROW 50400, 'Quantity must be between 1 and 100000', 1;

    DECLARE @paidMonths INT = CASE ISNULL(@DurationUnit, @planUnit) WHEN 'Year' THEN 12 WHEN 'Quarter' THEN 3 ELSE 1 END * ISNULL(@DurationCount, @planCount);
    DECLARE @planMonths INT = CASE @planUnit WHEN 'Year' THEN 12 WHEN 'Quarter' THEN 3 ELSE 1 END * @planCount;
    -- (A wrong duration is refused by usp_Sale_Create below.)
    DECLARE @terms DECIMAL(18,6) = CASE WHEN @paidMonths >= 1 AND @planMonths >= 1 THEN CAST(@paidMonths AS DECIMAL(18,6)) / @planMonths ELSE 1 END;

    DECLARE @today DATE = CAST(SWITCHOFFSET(SYSDATETIMEOFFSET(), '+05:30') AS DATE);
    DECLARE @fiscalYear INT = CASE WHEN MONTH(@today) >= 4 THEN YEAR(@today) + 1 ELSE YEAR(@today) END;
    DECLARE @seatPriced BIT = CASE WHEN EXISTS (SELECT 1 FROM dbo.ProductPlanSeatPrices WHERE PlanId = @PlanId) THEN 1 ELSE 0 END;
    DECLARE @unitPrice DECIMAL(18,2) = @planPrice, @unitCost DECIMAL(18,2) = @planCost, @seatRange NVARCHAR(30),
            @list DECIMAL(18,2), @costAmount DECIMAL(18,2), @sale DECIMAL(18,2);

    IF @seatPriced = 1
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM dbo.ProductPlanSeatPrices WHERE PlanId = @PlanId AND FiscalYear = @fiscalYear)
        BEGIN
            DECLARE @yearMessage NVARCHAR(200) = CONCAT(N'This plan has no seat prices for FY', RIGHT(CAST(@fiscalYear AS VARCHAR(4)), 2), N' - ask the admin to add them');
            THROW 50400, @yearMessage, 1;
        END
        SET @unitPrice = NULL;
        SELECT TOP 1 @unitPrice = sp.Price, @unitCost = ISNULL(sp.Cost, @planCost),
               @seatRange = CASE WHEN sp.MaxSeats IS NULL THEN CONCAT(sp.MinSeats, '+') ELSE CONCAT(sp.MinSeats, '-', sp.MaxSeats) END
        FROM dbo.ProductPlanSeatPrices sp
        WHERE sp.PlanId = @PlanId AND sp.FiscalYear = @fiscalYear
          AND sp.MinSeats <= @Quantity AND (sp.MaxSeats IS NULL OR sp.MaxSeats >= @Quantity)
        ORDER BY sp.MinSeats DESC;
        IF @unitPrice IS NULL
            THROW 50400, 'No seat price covers this number of seats', 1;
    END

    SET @unitPrice = ROUND(@unitPrice * @terms, 2);
    SET @unitCost = ROUND(@unitCost * @terms, 2);
    SET @list = @unitPrice * @Quantity;
    SET @costAmount = @unitCost * @Quantity;

    -- The same amount rules as usp_Sale_Create, against this list amount.
    IF (@SaleAmount IS NULL AND @DiscountAmount IS NULL) OR (@SaleAmount IS NOT NULL AND @DiscountAmount IS NOT NULL)
        THROW 50400, 'Enter either the sale price or the discount', 1;
    IF @DiscountAmount < 0
        THROW 50400, 'Discount cannot be negative', 1;
    IF @DiscountAmount > @list
        THROW 50400, 'Discount cannot be more than the list amount', 1;
    IF @SaleAmount < 0
        THROW 50400, 'Sale price cannot be negative', 1;
    IF @SaleAmount > @list
        THROW 50400, 'Sale price cannot be more than the list amount', 1;
    SET @sale = ISNULL(@SaleAmount, @list - @DiscountAmount);

    DECLARE @maxDiscount DECIMAL(9,2) = ISNULL(TRY_CAST((SELECT SettingValue FROM dbo.Settings WHERE SettingKey = 'Sales.MaxDiscountPercent') AS DECIMAL(9,2)), 100);
    IF @list > 0 AND @maxDiscount < 100 AND (@list - @sale) * 100 > @maxDiscount * @list
    BEGIN
        DECLARE @limitMessage NVARCHAR(200) = CONCAT(N'The discount cannot be more than ', FORMAT(@maxDiscount, '0.##'), N' percent of the list amount (company limit)');
        THROW 50400, @limitMessage, 1;
    END

    BEGIN TRAN;

    -- The sale itself: seller, customer, plan, duration, sale number, exactly as the "New sale" page. It is created at the
    -- plan's plain price with no discount and gets this order's amounts right after.
    DECLARE @created TABLE (PublicId UNIQUEIDENTIFIER);
    INSERT @created
    EXEC dbo.usp_Sale_Create
        @SalesUserId = @SalesUserId, @CustomerPublicId = @CustomerPublicId, @PlanId = @PlanId, @Quantity = @Quantity,
        @SaleAmount = NULL, @DiscountAmount = 0, @DurationUnit = @DurationUnit, @DurationCount = @DurationCount,
        @StartDate = @StartDate, @RenewalDate = @RenewalDate, @Notes = @Notes;
    DECLARE @publicId UNIQUEIDENTIFIER = (SELECT TOP 1 PublicId FROM @created);

    UPDATE dbo.Sales
    SET UnitPrice = @unitPrice, UnitCost = @unitCost, ListAmount = @list, SaleAmount = @sale,
        DiscountAmount = @list - @sale, CostAmount = @costAmount,
        PriceFiscalYear = CASE WHEN @seatPriced = 1 THEN @fiscalYear END, PriceSeatRange = @seatRange
    WHERE PublicId = @publicId;

    UPDATE dbo.Sales
    SET GstPercent = @GstPercent,
        TaxAmount = ROUND(SaleAmount * @GstPercent / 100, 2),
        FreeMonths = @free,
        RenewalDate = CASE WHEN @RenewalDate IS NULL THEN DATEADD(MONTH, @free, RenewalDate) ELSE RenewalDate END,
        BillingFrequencyId = @BillingFrequencyId, BillingMethodId = @BillingMethodId,
        PaymentMethodId = @PaymentMethodId, PaymentTermId = @PaymentTermId, CurrencyId = @CurrencyId,
        PrimaryContactName = @PrimaryContactName, PrimaryContactDesignation = @PrimaryContactDesignation,
        PrimaryContactEmail = @PrimaryContactEmail, PrimaryContactPhone = @PrimaryContactPhone,
        BillingContactName = @BillingContactName, BillingContactDesignation = @BillingContactDesignation,
        BillingContactEmail = @BillingContactEmail, BillingContactPhone = @BillingContactPhone,
        BillToAddress = @BillToAddress,
        OrderWording = @OrderWording,
        IncludedItemQuantity = @IncludedItemQuantity,
        TrackingStatusId = ISNULL(@TrackingStatusId, 1)
    WHERE PublicId = @publicId;

    COMMIT;

    SELECT @publicId;
END
GO

-- --------------------------------------------------------------------------------------------
-- StoredProcedures/usp_SaleOrder_SetTracking.sql
-- --------------------------------------------------------------------------------------------
-- Order forms list: changes an order's tracking status (Draft, Sent to DocuSign, Signed; DDL 036) without opening it.
-- The seller or the seller's manager, for an active order saved with the order form. Cancelled is not set here: the API
-- cancels the sale (manager) or sends a cancellation request (sales person) instead.
-- Raises 50404 (not found) or 50400. Returns the sale's PublicId.
CREATE OR ALTER PROCEDURE dbo.usp_SaleOrder_SetTracking
    @SalePublicId      UNIQUEIDENTIFIER,
    @SalesUserId       BIGINT,
    @TrackingStatusId  INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @saleId BIGINT, @status VARCHAR(20), @hasOrderForm BIT;
    SELECT @saleId = Id, @status = Status, @hasOrderForm = CASE WHEN BillingFrequencyId IS NOT NULL THEN 1 ELSE 0 END
    FROM dbo.Sales
    WHERE PublicId = @SalePublicId AND (SalesUserId = @SalesUserId OR ManagerId = @SalesUserId);
    IF @saleId IS NULL
        THROW 50404, 'Sale not found', 1;
    IF @hasOrderForm = 0
        THROW 50400, 'Only orders saved with the New order form have a tracking status', 1;
    IF @status <> 'Active'
        THROW 50400, 'A cancelled order cannot change its tracking status', 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.OrderTrackingStatuses WHERE Id = @TrackingStatusId AND Code <> 'CANCELLED')
        THROW 50400, 'Choose Draft, Sent to DocuSign or Signed', 1;

    UPDATE dbo.Sales SET TrackingStatusId = @TrackingStatusId WHERE Id = @saleId;
    SELECT @SalePublicId;
END
GO

-- --------------------------------------------------------------------------------------------
-- StoredProcedures/usp_SaleOrder_Update.sql
-- --------------------------------------------------------------------------------------------
-- User Panel: edits an order saved with the "New order form" (dbo.usp_SaleOrder_Create). Everything on the form can
-- change: customer, plan, seats, sale price / discount, duration, start date, free months, GST, the five list values,
-- contacts, bill-to address, wording and the included item quantity. The sale number, seller, manager and order date stay.
--   Who: the seller or the seller's manager (ManagerId saved with the sale), and their account must be active.
--   Which: active orders saved with the order form, with no cancellation request waiting.
--   Price: worked out again with the same rules as usp_SaleOrder_Create; seat prices are those of the financial year of
--   the ORDER DATE (CreatedAtUtc, India time), so an order edited after 1 April keeps its year's prices.
--   A changed customer must be active and a changed plan available for sale; the order's own customer / plan may have
--   been switched off since without blocking the edit.
--   Renewal date = start + paid duration + free months (unless @RenewalDate is given), as when the order was saved.
-- Raises 50404 (sale / customer / plan not found) or 50400 (anything not allowed). Returns the sale's PublicId.
CREATE OR ALTER PROCEDURE dbo.usp_SaleOrder_Update
    @SalePublicId               UNIQUEIDENTIFIER,
    @EditorSalesUserId          BIGINT,
    @CustomerPublicId           UNIQUEIDENTIFIER,
    @PlanId                     BIGINT,
    @Quantity                   INT,
    @SaleAmount                 DECIMAL(18,2)  = NULL,
    @DiscountAmount             DECIMAL(18,2)  = NULL,
    @DurationUnit               VARCHAR(10)    = NULL,
    @DurationCount              INT            = NULL,
    @StartDate                  DATE,
    @RenewalDate                DATE           = NULL,
    @Notes                      NVARCHAR(1000) = NULL,
    @GstPercent                 DECIMAL(5,2),
    @FreeMonths                 INT            = 0,
    @BillingFrequencyId         INT,
    @BillingMethodId            INT,
    @PaymentMethodId            INT,
    @PaymentTermId              INT,
    @CurrencyId                 INT,
    @PrimaryContactName         NVARCHAR(150),
    @PrimaryContactDesignation  NVARCHAR(100)  = NULL,
    @PrimaryContactEmail        NVARCHAR(256)  = NULL,
    @PrimaryContactPhone        NVARCHAR(20)   = NULL,
    @BillingContactName         NVARCHAR(150)  = NULL,
    @BillingContactDesignation  NVARCHAR(100)  = NULL,
    @BillingContactEmail        NVARCHAR(256)  = NULL,
    @BillingContactPhone        NVARCHAR(20)   = NULL,
    @BillToAddress              NVARCHAR(500)  = NULL,
    @OrderWording               NVARCHAR(MAX)  = NULL,
    @IncludedItemQuantity       INT            = NULL,
    @TrackingStatusId           INT            = NULL   -- Draft / Sent to DocuSign / Signed; NULL keeps it (Cancelled is done by cancelling the sale)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    -- The order, as the editor may see it (their own, or their team's for the manager).
    DECLARE @saleId BIGINT, @status VARCHAR(20), @hasOrderForm BIT, @createdAtUtc DATETIME2(3),
            @currentCustomerId BIGINT, @currentPlanId BIGINT;
    SELECT @saleId = Id, @status = Status, @createdAtUtc = CreatedAtUtc,
           @hasOrderForm = CASE WHEN BillingFrequencyId IS NOT NULL THEN 1 ELSE 0 END,
           @currentCustomerId = CustomerId, @currentPlanId = PlanId
    FROM dbo.Sales
    WHERE PublicId = @SalePublicId AND (SalesUserId = @EditorSalesUserId OR ManagerId = @EditorSalesUserId);
    IF @saleId IS NULL
        THROW 50404, 'Sale not found', 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.SalesUsers WHERE Id = @EditorSalesUserId AND IsDeleted = 0 AND Status = 'Active')
        THROW 50400, 'Your account is not active', 1;
    IF @hasOrderForm = 0
        THROW 50400, 'Only orders saved with the New order form can be edited', 1;
    IF @status <> 'Active'
        THROW 50400, 'A cancelled order cannot be edited', 1;
    IF EXISTS (SELECT 1 FROM dbo.SaleCancelRequests WHERE SaleId = @saleId AND Status = 'Pending')
        THROW 50400, 'This order has a cancellation request waiting for the manager; it cannot be edited until it is decided', 1;

    -- Order-form details (same checks as usp_SaleOrder_Create).
    IF @GstPercent IS NULL OR @GstPercent < 0 OR @GstPercent > 28
        THROW 50400, 'GST must be between 0 and 28 percent', 1;

    DECLARE @free INT = ISNULL(@FreeMonths, 0);
    DECLARE @maxFree INT = ISNULL(TRY_CAST((SELECT SettingValue FROM dbo.Settings WHERE SettingKey = 'Sales.MaxFreeMonths') AS INT), 12);
    IF @free < 0 OR @free > @maxFree
    BEGIN
        DECLARE @freeMessage NVARCHAR(200) = CONCAT(N'Free months must be between 0 and ', @maxFree, N' (company setting)');
        THROW 50400, @freeMessage, 1;
    END

    IF NOT EXISTS (SELECT 1 FROM dbo.MasterValues WHERE Id = @BillingFrequencyId AND MasterType = 'BillingFrequency' AND IsActive = 1)
        THROW 50400, 'Choose a billing frequency from the list', 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.MasterValues WHERE Id = @BillingMethodId AND MasterType = 'BillingMethod' AND IsActive = 1)
        THROW 50400, 'Choose a billing method from the list', 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.MasterValues WHERE Id = @PaymentMethodId AND MasterType = 'PaymentMethod' AND IsActive = 1)
        THROW 50400, 'Choose a payment method from the list', 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.MasterValues WHERE Id = @PaymentTermId AND MasterType = 'PaymentTerm' AND IsActive = 1)
        THROW 50400, 'Choose a payment term from the list', 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.MasterValues WHERE Id = @CurrencyId AND MasterType = 'Currency' AND IsActive = 1)
        THROW 50400, 'Choose a currency from the list', 1;
    IF @OrderWording IS NOT NULL AND ISJSON(@OrderWording) = 0
        THROW 50400, 'The order wording is not valid', 1;
    IF @IncludedItemQuantity IS NOT NULL AND (@IncludedItemQuantity < 1 OR @IncludedItemQuantity > 10000000)
        THROW 50400, 'The Team Link Extend seat quantity must be between 1 and 10000000', 1;
    IF NULLIF(LTRIM(RTRIM(@PrimaryContactName)), N'') IS NULL
        THROW 50400, 'Enter the primary contact''s name', 1;
    IF @TrackingStatusId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.OrderTrackingStatuses WHERE Id = @TrackingStatusId AND Code <> 'CANCELLED')
        THROW 50400, 'Choose Draft, Sent to DocuSign or Signed (Cancelled cancels the sale)', 1;

    -- Customer: a different one must be active.
    DECLARE @customerId BIGINT, @customerActive BIT;
    SELECT @customerId = Id, @customerActive = IsActive FROM dbo.Customers WHERE PublicId = @CustomerPublicId;
    IF @customerId IS NULL
        THROW 50404, 'Company not found', 1;
    IF @customerActive = 0 AND @customerId <> @currentCustomerId
        THROW 50400, 'This company is inactive', 1;

    -- Plan: a different one must be available for sale (plan, product and category active).
    DECLARE @productId BIGINT, @categoryId BIGINT, @planPrice DECIMAL(18,2), @planCost DECIMAL(18,2),
            @planUnit VARCHAR(10), @planCount INT, @available BIT;
    SELECT @productId = p.Id, @categoryId = c.Id, @planPrice = pl.Price, @planCost = pl.Cost,
           @planUnit = pl.DurationUnit, @planCount = pl.DurationCount,
           @available = CASE WHEN pl.IsActive = 1 AND p.IsActive = 1 AND c.IsActive = 1 THEN 1 ELSE 0 END
    FROM dbo.ProductPlans pl
    JOIN dbo.Products p ON p.Id = pl.ProductId
    JOIN dbo.ProductCategories c ON c.Id = p.CategoryId
    WHERE pl.Id = @PlanId;
    IF @productId IS NULL
        THROW 50404, 'Plan not found', 1;
    IF @available = 0 AND @PlanId <> @currentPlanId
        THROW 50400, 'This plan is not available for sale', 1;

    IF @Quantity IS NULL OR @Quantity < 1 OR @Quantity > 100000
        THROW 50400, 'Quantity must be between 1 and 100000', 1;

    -- Duration and start date.
    DECLARE @unit VARCHAR(10) = ISNULL(@DurationUnit, @planUnit);
    DECLARE @count INT = ISNULL(@DurationCount, @planCount);
    IF @unit NOT IN ('Month', 'Quarter', 'Year')
        THROW 50400, 'Duration unit must be Month, Quarter or Year', 1;
    IF @count < 1 OR @count > 120
        THROW 50400, 'Duration must be between 1 and 120', 1;
    IF @StartDate IS NULL
        THROW 50400, 'Start date is required', 1;

    -- Price, as usp_SaleOrder_Create, with the financial year of the order date (India time).
    DECLARE @paidMonths INT = CASE @unit WHEN 'Year' THEN 12 WHEN 'Quarter' THEN 3 ELSE 1 END * @count;
    DECLARE @planMonths INT = CASE @planUnit WHEN 'Year' THEN 12 WHEN 'Quarter' THEN 3 ELSE 1 END * @planCount;
    DECLARE @terms DECIMAL(18,6) = CASE WHEN @paidMonths >= 1 AND @planMonths >= 1 THEN CAST(@paidMonths AS DECIMAL(18,6)) / @planMonths ELSE 1 END;

    DECLARE @orderDate DATE = CAST(SWITCHOFFSET(CAST(@createdAtUtc AS DATETIMEOFFSET), '+05:30') AS DATE);
    DECLARE @fiscalYear INT = CASE WHEN MONTH(@orderDate) >= 4 THEN YEAR(@orderDate) + 1 ELSE YEAR(@orderDate) END;
    DECLARE @seatPriced BIT = CASE WHEN EXISTS (SELECT 1 FROM dbo.ProductPlanSeatPrices WHERE PlanId = @PlanId) THEN 1 ELSE 0 END;
    DECLARE @unitPrice DECIMAL(18,2) = @planPrice, @unitCost DECIMAL(18,2) = @planCost, @seatRange NVARCHAR(30),
            @list DECIMAL(18,2), @costAmount DECIMAL(18,2), @sale DECIMAL(18,2);

    IF @seatPriced = 1
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM dbo.ProductPlanSeatPrices WHERE PlanId = @PlanId AND FiscalYear = @fiscalYear)
        BEGIN
            DECLARE @yearMessage NVARCHAR(200) = CONCAT(N'This plan has no seat prices for FY', RIGHT(CAST(@fiscalYear AS VARCHAR(4)), 2), N' (the order''s year) - ask the admin to add them');
            THROW 50400, @yearMessage, 1;
        END
        SET @unitPrice = NULL;
        SELECT TOP 1 @unitPrice = sp.Price, @unitCost = ISNULL(sp.Cost, @planCost),
               @seatRange = CASE WHEN sp.MaxSeats IS NULL THEN CONCAT(sp.MinSeats, '+') ELSE CONCAT(sp.MinSeats, '-', sp.MaxSeats) END
        FROM dbo.ProductPlanSeatPrices sp
        WHERE sp.PlanId = @PlanId AND sp.FiscalYear = @fiscalYear
          AND sp.MinSeats <= @Quantity AND (sp.MaxSeats IS NULL OR sp.MaxSeats >= @Quantity)
        ORDER BY sp.MinSeats DESC;
        IF @unitPrice IS NULL
            THROW 50400, 'No seat price covers this number of seats', 1;
    END

    SET @unitPrice = ROUND(@unitPrice * @terms, 2);
    SET @unitCost = ROUND(@unitCost * @terms, 2);
    SET @list = @unitPrice * @Quantity;
    SET @costAmount = @unitCost * @Quantity;

    IF (@SaleAmount IS NULL AND @DiscountAmount IS NULL) OR (@SaleAmount IS NOT NULL AND @DiscountAmount IS NOT NULL)
        THROW 50400, 'Enter either the sale price or the discount', 1;
    IF @DiscountAmount < 0
        THROW 50400, 'Discount cannot be negative', 1;
    IF @DiscountAmount > @list
        THROW 50400, 'Discount cannot be more than the list amount', 1;
    IF @SaleAmount < 0
        THROW 50400, 'Sale price cannot be negative', 1;
    IF @SaleAmount > @list
        THROW 50400, 'Sale price cannot be more than the list amount', 1;
    SET @sale = ISNULL(@SaleAmount, @list - @DiscountAmount);

    DECLARE @maxDiscount DECIMAL(9,2) = ISNULL(TRY_CAST((SELECT SettingValue FROM dbo.Settings WHERE SettingKey = 'Sales.MaxDiscountPercent') AS DECIMAL(9,2)), 100);
    IF @list > 0 AND @maxDiscount < 100 AND (@list - @sale) * 100 > @maxDiscount * @list
    BEGIN
        DECLARE @limitMessage NVARCHAR(200) = CONCAT(N'The discount cannot be more than ', FORMAT(@maxDiscount, '0.##'), N' percent of the list amount (company limit)');
        THROW 50400, @limitMessage, 1;
    END

    -- Renewal date: start + paid duration + free months, as when the order was saved.
    DECLARE @renewal DATE = ISNULL(@RenewalDate,
        DATEADD(MONTH, @free, CASE @unit WHEN 'Month' THEN DATEADD(MONTH, @count, @StartDate)
                                         WHEN 'Quarter' THEN DATEADD(MONTH, 3 * @count, @StartDate)
                                         ELSE DATEADD(YEAR, @count, @StartDate) END));
    IF @renewal <= @StartDate
        THROW 50400, 'Renewal date must be after the start date', 1;

    UPDATE dbo.Sales
    SET CustomerId = @customerId, CategoryId = @categoryId, ProductId = @productId, PlanId = @PlanId, Quantity = @Quantity,
        UnitPrice = @unitPrice, UnitCost = @unitCost, ListAmount = @list, SaleAmount = @sale,
        DiscountAmount = @list - @sale, CostAmount = @costAmount,
        PriceFiscalYear = CASE WHEN @seatPriced = 1 THEN @fiscalYear END, PriceSeatRange = @seatRange,
        DurationUnit = @unit, DurationCount = @count, StartDate = @StartDate, RenewalDate = @renewal, Notes = @Notes,
        GstPercent = @GstPercent, TaxAmount = ROUND(@sale * @GstPercent / 100, 2), FreeMonths = @free,
        BillingFrequencyId = @BillingFrequencyId, BillingMethodId = @BillingMethodId,
        PaymentMethodId = @PaymentMethodId, PaymentTermId = @PaymentTermId, CurrencyId = @CurrencyId,
        PrimaryContactName = @PrimaryContactName, PrimaryContactDesignation = @PrimaryContactDesignation,
        PrimaryContactEmail = @PrimaryContactEmail, PrimaryContactPhone = @PrimaryContactPhone,
        BillingContactName = @BillingContactName, BillingContactDesignation = @BillingContactDesignation,
        BillingContactEmail = @BillingContactEmail, BillingContactPhone = @BillingContactPhone,
        BillToAddress = @BillToAddress,
        OrderWording = @OrderWording,
        IncludedItemQuantity = @IncludedItemQuantity,
        TrackingStatusId = ISNULL(@TrackingStatusId, TrackingStatusId)
    WHERE Id = @saleId;

    SELECT @SalePublicId;
END
GO

-- --------------------------------------------------------------------------------------------
-- StoredProcedures/usp_SalesUserHistory_GetPaged.sql
-- --------------------------------------------------------------------------------------------
-- Admin Panel > Sales users > History: one page of the audit rows about sales users (dbo.AuditLogs, EntityType SalesUser),
-- newest first, for one person (@SalesUserPublicId) or everyone. Paging happens here, never in the browser; the API turns
-- each row into the changes it records. Sign-ins and password changes are left out.
CREATE OR ALTER PROCEDURE dbo.usp_SalesUserHistory_GetPaged
    @SalesUserPublicId  UNIQUEIDENTIFIER = NULL,
    @Offset             INT              = 0,
    @PageSize           INT              = 20,
    @TotalCount         INT              OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @entityId NVARCHAR(100) = CASE WHEN @SalesUserPublicId IS NULL THEN NULL ELSE CAST(@SalesUserPublicId AS NVARCHAR(100)) END;

    SELECT @TotalCount = COUNT(*)
    FROM dbo.AuditLogs a
    WHERE a.EntityType = 'SalesUser'
      AND a.Action IN ('Create', 'Update', 'StatusChange', 'CreateTeamMember', 'TeamMemberStatus', 'CancelPermission')
      AND (@entityId IS NULL OR a.EntityId = @entityId)
    OPTION (RECOMPILE);

    SELECT a.CreatedAtUtc, a.Action, a.EntityId, a.OldValues, a.NewValues, a.ActorUserId,
           CONCAT(au.FirstName, N' ', au.LastName) AS AdminName,
           su.PublicId AS SalesUserId, CONCAT(su.FirstName, N' ', su.LastName) AS SalesUserName, su.Username
    FROM dbo.AuditLogs a
    LEFT JOIN dbo.AdminUsers au ON au.Id = a.ActorUserId
    LEFT JOIN dbo.SalesUsers su ON su.PublicId = TRY_CAST(a.EntityId AS UNIQUEIDENTIFIER)
    WHERE a.EntityType = 'SalesUser'
      AND a.Action IN ('Create', 'Update', 'StatusChange', 'CreateTeamMember', 'TeamMemberStatus', 'CancelPermission')
      AND (@entityId IS NULL OR a.EntityId = @entityId)
    ORDER BY a.Id DESC
    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
    OPTION (RECOMPILE);
END
GO

-- --------------------------------------------------------------------------------------------
-- SeedData/013_Seed_Lead_Lists.sql
-- --------------------------------------------------------------------------------------------
-- 013: starting values of the lead lists (DDL 035). Admins change them later in Admin Panel > Settings > Lead lists.
-- Adds only missing values, so running it again changes nothing.
MERGE dbo.LeadProducts AS t
USING (VALUES (N'AWS', 1), (N'AZURE', 2), (N'BACKUP', 3), (N'COLOCATION', 4), (N'DLP', 5), (N'G-SUITE', 6), (N'GCP', 7),
              (N'LinkedIn Sales Nav', 8), (N'M365', 9), (N'TALLY ON CLOUD', 10), (N'i2k2 DC', 11)) AS s (Name, SortOrder)
    ON t.Name = s.Name
WHEN NOT MATCHED THEN INSERT (Name, SortOrder) VALUES (s.Name, s.SortOrder);
GO

MERGE dbo.LeadSources AS t
USING (VALUES (N'Cold Call', 1), (N'Email Campaign', 2), (N'Event', 3), (N'LinkedIn', 4), (N'Partner', 5),
              (N'Referral', 6), (N'Website', 7)) AS s (Name, SortOrder)
    ON t.Name = s.Name
WHEN NOT MATCHED THEN INSERT (Name, SortOrder) VALUES (s.Name, s.SortOrder);
GO

MERGE dbo.LeadBillingCycles AS t
USING (VALUES (N'Monthly', 1), (N'Quarterly', 2), (N'Half Yearly', 3), (N'Yearly', 4)) AS s (Name, SortOrder)
    ON t.Name = s.Name
WHEN NOT MATCHED THEN INSERT (Name, SortOrder) VALUES (s.Name, s.SortOrder);
GO

-- --------------------------------------------------------------------------------------------
-- SeedData/014_Rename_Customers_To_Companies.sql
-- --------------------------------------------------------------------------------------------
-- 014: customers are called companies on every screen. The Admin Panel's menu item comes from dbo.Modules (code stays
-- 'Customer', so permissions Customer.View / Add / Edit are unchanged); only the name people see changes.
UPDATE dbo.Modules SET Name = N'Companies'
WHERE Code = 'Customer' AND Name IN (N'Customers', N'Customer Management');
GO

-- --------------------------------------------------------------------------------------------
-- Record the DDL / seed scripts (same name and SHA-256 as Deploy-Database.ps1).
-- --------------------------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM dbo.SchemaVersions WHERE ScriptName = N'DDL/035_Create_Leads.sql') INSERT dbo.SchemaVersions (ScriptName, Checksum) VALUES (N'DDL/035_Create_Leads.sql', 'b0c957b929a219d96c04bdd5d73b2dd8e43116c42c15b282819b333917671481');
IF NOT EXISTS (SELECT 1 FROM dbo.SchemaVersions WHERE ScriptName = N'DDL/036_Create_OrderTrackingStatuses.sql') INSERT dbo.SchemaVersions (ScriptName, Checksum) VALUES (N'DDL/036_Create_OrderTrackingStatuses.sql', '46c677fd4169dc1206f6b915df09c4a7434f246d9bbcb748cace64b973ae554b');
IF NOT EXISTS (SELECT 1 FROM dbo.SchemaVersions WHERE ScriptName = N'SeedData/013_Seed_Lead_Lists.sql') INSERT dbo.SchemaVersions (ScriptName, Checksum) VALUES (N'SeedData/013_Seed_Lead_Lists.sql', '7d9b711d7f5f1486e1c42551d1a9dd432687f693e71f8a77955edb260d6144c6');
IF NOT EXISTS (SELECT 1 FROM dbo.SchemaVersions WHERE ScriptName = N'SeedData/014_Rename_Customers_To_Companies.sql') INSERT dbo.SchemaVersions (ScriptName, Checksum) VALUES (N'SeedData/014_Rename_Customers_To_Companies.sql', '8917a86c4524cfd2e8ec65996ed679e703150894ac414ff0da0d908f79d5d725');
GO
PRINT 'Release applied: Release 2026-11-23: leads, lead lists, order edit and tracking, Order forms list, lead dashboard, company edit';
GO
