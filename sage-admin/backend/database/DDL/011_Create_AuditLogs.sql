-- 011: append-only audit trail
IF OBJECT_ID(N'dbo.AuditLogs', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AuditLogs
    (
        Id            BIGINT IDENTITY(1,1) NOT NULL,
        ActorUserId   BIGINT         NULL,
        Action        VARCHAR(100)   NOT NULL,
        EntityType    VARCHAR(100)   NOT NULL,
        EntityId      VARCHAR(100)   NULL,
        OldValues     NVARCHAR(MAX)  NULL,
        NewValues     NVARCHAR(MAX)  NULL,
        IpAddress     VARCHAR(45)    NULL,
        CreatedAtUtc  DATETIME2(3)   NOT NULL CONSTRAINT DF_AuditLogs_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_AuditLogs PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_AuditLogs_AdminUsers FOREIGN KEY (ActorUserId) REFERENCES dbo.AdminUsers (Id),
        CONSTRAINT CK_AuditLogs_OldValues_Json CHECK (OldValues IS NULL OR ISJSON(OldValues) = 1),
        CONSTRAINT CK_AuditLogs_NewValues_Json CHECK (NewValues IS NULL OR ISJSON(NewValues) = 1)
    );
END
GO
