-- 017: User Panel accounts (sales persons and sales managers). Separate from AdminUsers: own login, created by admins.
-- A sales manager is also a sales person (can sell). A sales person reports to one sales manager (ManagerId).
IF OBJECT_ID(N'dbo.SalesUsers', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SalesUsers
    (
        Id                    BIGINT IDENTITY(1,1) NOT NULL,
        PublicId              UNIQUEIDENTIFIER     NOT NULL CONSTRAINT DF_SalesUsers_PublicId DEFAULT NEWID(),
        FirstName             NVARCHAR(100)        NOT NULL,
        LastName              NVARCHAR(100)        NOT NULL,
        Username              NVARCHAR(50)         NOT NULL,
        Email                 NVARCHAR(256)        NOT NULL,
        MobileNumber          NVARCHAR(20)         NULL,
        PasswordHash          NVARCHAR(255)        NOT NULL,
        PasswordUpdatedAtUtc  DATETIME2(3)         NOT NULL CONSTRAINT DF_SalesUsers_PasswordUpdatedAtUtc DEFAULT SYSUTCDATETIME(),
        MustChangePassword    BIT                  NOT NULL CONSTRAINT DF_SalesUsers_MustChangePassword DEFAULT 1,
        Role                  VARCHAR(20)          NOT NULL,
        ManagerId             BIGINT               NULL,
        Status                VARCHAR(20)          NOT NULL CONSTRAINT DF_SalesUsers_Status DEFAULT 'Active',
        FailedLoginCount      INT                  NOT NULL CONSTRAINT DF_SalesUsers_FailedLoginCount DEFAULT 0,
        LockoutEndUtc         DATETIME2(3)         NULL,
        PermissionVersion     INT                  NOT NULL CONSTRAINT DF_SalesUsers_PermissionVersion DEFAULT 1,
        LastLoginAtUtc        DATETIME2(3)         NULL,
        IsDeleted             BIT                  NOT NULL CONSTRAINT DF_SalesUsers_IsDeleted DEFAULT 0,
        CreatedAtUtc          DATETIME2(3)         NOT NULL CONSTRAINT DF_SalesUsers_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        CreatedBy             BIGINT               NULL,
        UpdatedAtUtc          DATETIME2(3)         NULL,
        UpdatedBy             BIGINT               NULL,
        RowVersion            ROWVERSION           NOT NULL,
        CONSTRAINT PK_SalesUsers PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_SalesUsers_PublicId UNIQUE (PublicId),
        CONSTRAINT FK_SalesUsers_Manager FOREIGN KEY (ManagerId) REFERENCES dbo.SalesUsers (Id),
        CONSTRAINT FK_SalesUsers_CreatedBy FOREIGN KEY (CreatedBy) REFERENCES dbo.AdminUsers (Id),
        CONSTRAINT FK_SalesUsers_UpdatedBy FOREIGN KEY (UpdatedBy) REFERENCES dbo.AdminUsers (Id),
        CONSTRAINT CK_SalesUsers_Role CHECK (Role IN ('SalesPerson','SalesManager')),
        CONSTRAINT CK_SalesUsers_Status CHECK (Status IN ('Active','Inactive','Locked')),
        CONSTRAINT CK_SalesUsers_ManagerNotSelf CHECK (ManagerId IS NULL OR ManagerId <> Id),
        -- A manager has no manager (one reporting level).
        CONSTRAINT CK_SalesUsers_ManagerHasNoManager CHECK (Role <> 'SalesManager' OR ManagerId IS NULL),
        CONSTRAINT CK_SalesUsers_FailedLoginCount CHECK (FailedLoginCount >= 0)
    );
    -- Uniqueness applies to non-deleted users only.
    CREATE UNIQUE NONCLUSTERED INDEX UX_SalesUsers_Username ON dbo.SalesUsers (Username) WHERE IsDeleted = 0;
    CREATE UNIQUE NONCLUSTERED INDEX UX_SalesUsers_Email    ON dbo.SalesUsers (Email)    WHERE IsDeleted = 0;
    CREATE NONCLUSTERED INDEX IX_SalesUsers_ManagerId ON dbo.SalesUsers (ManagerId);
END
GO
