-- 007: user <-> group assignments. Schema allows many groups per user; exactly one primary.
IF OBJECT_ID(N'dbo.AdminUserGroups', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AdminUserGroups
    (
        UserId        BIGINT       NOT NULL,
        GroupId       BIGINT       NOT NULL,
        IsPrimary     BIT          NOT NULL CONSTRAINT DF_AdminUserGroups_IsPrimary DEFAULT 1,
        AssignedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_AdminUserGroups_AssignedAtUtc DEFAULT SYSUTCDATETIME(),
        AssignedBy    BIGINT       NULL,
        CONSTRAINT PK_AdminUserGroups PRIMARY KEY CLUSTERED (UserId, GroupId),
        CONSTRAINT FK_AdminUserGroups_AdminUsers FOREIGN KEY (UserId) REFERENCES dbo.AdminUsers (Id),
        CONSTRAINT FK_AdminUserGroups_PermissionGroups FOREIGN KEY (GroupId) REFERENCES dbo.PermissionGroups (Id)
    );
    CREATE UNIQUE NONCLUSTERED INDEX UX_AdminUserGroups_OnePrimary ON dbo.AdminUserGroups (UserId) WHERE IsPrimary = 1;
END
GO
