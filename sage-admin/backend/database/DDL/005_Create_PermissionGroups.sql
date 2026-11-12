-- 005: permission groups (HR, Sales Manager, ...)
IF OBJECT_ID(N'dbo.PermissionGroups', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PermissionGroups
    (
        Id            BIGINT IDENTITY(1,1) NOT NULL,
        PublicId      UNIQUEIDENTIFIER     NOT NULL CONSTRAINT DF_PermissionGroups_PublicId DEFAULT NEWID(),
        Name          NVARCHAR(100)        NOT NULL,
        Code          VARCHAR(50)          NOT NULL,
        Description   NVARCHAR(500)        NULL,
        Status        VARCHAR(20)          NOT NULL CONSTRAINT DF_PermissionGroups_Status DEFAULT 'Active',
        IsSystem      BIT                  NOT NULL CONSTRAINT DF_PermissionGroups_IsSystem DEFAULT 0,
        CreatedAtUtc  DATETIME2(3)         NOT NULL CONSTRAINT DF_PermissionGroups_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        CreatedBy     BIGINT               NULL,
        UpdatedAtUtc  DATETIME2(3)         NULL,
        UpdatedBy     BIGINT               NULL,
        CONSTRAINT PK_PermissionGroups PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_PermissionGroups_PublicId UNIQUE (PublicId),
        CONSTRAINT UQ_PermissionGroups_Name UNIQUE (Name),
        CONSTRAINT UQ_PermissionGroups_Code UNIQUE (Code),
        CONSTRAINT CK_PermissionGroups_Status CHECK (Status IN ('Active','Inactive'))
    );
END
GO
