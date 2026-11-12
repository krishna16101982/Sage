-- 004: permissions. Code = Module.Action, e.g. Employee.View
IF OBJECT_ID(N'dbo.Permissions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Permissions
    (
        Id            BIGINT IDENTITY(1,1) NOT NULL,
        ModuleId      BIGINT               NOT NULL,
        Action        VARCHAR(50)          NOT NULL,
        Code          VARCHAR(101)         NOT NULL,
        Name          NVARCHAR(150)        NOT NULL,
        Description   NVARCHAR(500)        NULL,
        IsActive      BIT                  NOT NULL CONSTRAINT DF_Permissions_IsActive DEFAULT 1,
        IsSystem      BIT                  NOT NULL CONSTRAINT DF_Permissions_IsSystem DEFAULT 0,
        CreatedAtUtc  DATETIME2(3)         NOT NULL CONSTRAINT DF_Permissions_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        UpdatedAtUtc  DATETIME2(3)         NULL,
        CONSTRAINT PK_Permissions PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_Permissions_Modules FOREIGN KEY (ModuleId) REFERENCES dbo.Modules (Id),
        CONSTRAINT UQ_Permissions_Code UNIQUE (Code),
        CONSTRAINT UQ_Permissions_Module_Action UNIQUE (ModuleId, Action),
        CONSTRAINT CK_Permissions_Code CHECK (Code LIKE '%.%')
    );
END
GO
