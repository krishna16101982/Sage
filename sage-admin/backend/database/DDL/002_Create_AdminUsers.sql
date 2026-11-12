-- 002: administrators. PermissionVersion is bumped whenever a user's effective access changes.
IF OBJECT_ID(N'dbo.AdminUsers', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AdminUsers
    (
        Id                    BIGINT IDENTITY(1,1) NOT NULL,
        PublicId              UNIQUEIDENTIFIER     NOT NULL CONSTRAINT DF_AdminUsers_PublicId DEFAULT NEWID(),
        FirstName             NVARCHAR(100)        NOT NULL,
        LastName              NVARCHAR(100)        NOT NULL,
        Username              NVARCHAR(50)         NOT NULL,
        Email                 NVARCHAR(256)        NOT NULL,
        MobileNumber          NVARCHAR(20)         NULL,
        PasswordHash          NVARCHAR(255)        NOT NULL,
        PasswordUpdatedAtUtc  DATETIME2(3)         NOT NULL CONSTRAINT DF_AdminUsers_PasswordUpdatedAtUtc DEFAULT SYSUTCDATETIME(),
        MustChangePassword    BIT                  NOT NULL CONSTRAINT DF_AdminUsers_MustChangePassword DEFAULT 1,
        IsSuperAdmin          BIT                  NOT NULL CONSTRAINT DF_AdminUsers_IsSuperAdmin DEFAULT 0,
        Status                VARCHAR(20)          NOT NULL CONSTRAINT DF_AdminUsers_Status DEFAULT 'Active',
        FailedLoginCount      INT                  NOT NULL CONSTRAINT DF_AdminUsers_FailedLoginCount DEFAULT 0,
        LockoutEndUtc         DATETIME2(3)         NULL,
        PermissionVersion     INT                  NOT NULL CONSTRAINT DF_AdminUsers_PermissionVersion DEFAULT 1,
        LastLoginAtUtc        DATETIME2(3)         NULL,
        IsDeleted             BIT                  NOT NULL CONSTRAINT DF_AdminUsers_IsDeleted DEFAULT 0,
        CreatedAtUtc          DATETIME2(3)         NOT NULL CONSTRAINT DF_AdminUsers_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        CreatedBy             BIGINT               NULL,
        UpdatedAtUtc          DATETIME2(3)         NULL,
        UpdatedBy             BIGINT               NULL,
        RowVersion            ROWVERSION           NOT NULL,
        CONSTRAINT PK_AdminUsers PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_AdminUsers_PublicId UNIQUE (PublicId),
        CONSTRAINT CK_AdminUsers_Status CHECK (Status IN ('Active','Inactive','Locked')),
        CONSTRAINT CK_AdminUsers_FailedLoginCount CHECK (FailedLoginCount >= 0)
    );
    -- Uniqueness applies to non-deleted users only, so a deleted user's username/email can be reused.
    CREATE UNIQUE NONCLUSTERED INDEX UX_AdminUsers_Username ON dbo.AdminUsers (Username) WHERE IsDeleted = 0;
    CREATE UNIQUE NONCLUSTERED INDEX UX_AdminUsers_Email    ON dbo.AdminUsers (Email)    WHERE IsDeleted = 0;
END
GO
