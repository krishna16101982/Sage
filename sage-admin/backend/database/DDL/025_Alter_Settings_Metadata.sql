-- 025: Settings screen. Each setting describes itself so the Admin Panel can show and validate it without code:
-- Category and Label for display, SortOrder, and limits (MinValue / MaxValue for 'int', MaxLength and Pattern for 'string').
-- Only settings that the system really reads are added (SeedData 008).
IF COL_LENGTH(N'dbo.Settings', N'Category') IS NULL
BEGIN
    ALTER TABLE dbo.Settings ADD
        Category   NVARCHAR(100) NOT NULL CONSTRAINT DF_Settings_Category DEFAULT N'General',
        Label      NVARCHAR(150) NULL,
        SortOrder  INT           NOT NULL CONSTRAINT DF_Settings_SortOrder DEFAULT 0,
        MinValue   INT           NULL,
        MaxValue   INT           NULL,
        MaxLength  INT           NULL,
        Pattern    VARCHAR(200)  NULL;
END
GO

IF OBJECT_ID(N'dbo.FK_Settings_UpdatedBy', N'F') IS NULL
    ALTER TABLE dbo.Settings ADD CONSTRAINT FK_Settings_UpdatedBy FOREIGN KEY (UpdatedBy) REFERENCES dbo.AdminUsers (Id);
GO

-- Audit log screen: newest first, filtered by entity type or actor.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AuditLogs_CreatedAtUtc' AND object_id = OBJECT_ID(N'dbo.AuditLogs'))
    CREATE NONCLUSTERED INDEX IX_AuditLogs_CreatedAtUtc ON dbo.AuditLogs (CreatedAtUtc DESC) INCLUDE (EntityType, Action, ActorUserId);
GO
