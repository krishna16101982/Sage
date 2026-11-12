-- 001: script tracking table. Idempotent (also bootstrapped by Deploy-Database.ps1).
IF OBJECT_ID(N'dbo.SchemaVersions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SchemaVersions
    (
        Id            INT IDENTITY(1,1) NOT NULL,
        ScriptName    NVARCHAR(260)     NOT NULL,
        Checksum      CHAR(64)          NOT NULL,
        AppliedAtUtc  DATETIME2(3)      NOT NULL CONSTRAINT DF_SchemaVersions_AppliedAtUtc DEFAULT SYSUTCDATETIME(),
        AppliedBy     NVARCHAR(128)     NOT NULL CONSTRAINT DF_SchemaVersions_AppliedBy DEFAULT SUSER_SNAME(),
        CONSTRAINT PK_SchemaVersions PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_SchemaVersions_ScriptName UNIQUE (ScriptName)
    );
END
GO
