-- 003: permission modules (e.g. Employee, Attendance). Dynamic, managed in SQL.
IF OBJECT_ID(N'dbo.Modules', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Modules
    (
        Id            BIGINT IDENTITY(1,1) NOT NULL,
        Code          VARCHAR(50)          NOT NULL,
        Name          NVARCHAR(100)        NOT NULL,
        Category      NVARCHAR(100)        NULL,
        SortOrder     INT                  NOT NULL CONSTRAINT DF_Modules_SortOrder DEFAULT 0,
        IsActive      BIT                  NOT NULL CONSTRAINT DF_Modules_IsActive DEFAULT 1,
        IsSystem      BIT                  NOT NULL CONSTRAINT DF_Modules_IsSystem DEFAULT 0,
        CreatedAtUtc  DATETIME2(3)         NOT NULL CONSTRAINT DF_Modules_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        UpdatedAtUtc  DATETIME2(3)         NULL,
        CONSTRAINT PK_Modules PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_Modules_Code UNIQUE (Code),
        CONSTRAINT CK_Modules_Code CHECK (LEN(Code) >= 2)
    );
END
GO
