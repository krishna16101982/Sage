-- 006: group <-> permission assignments
IF OBJECT_ID(N'dbo.GroupPermissions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.GroupPermissions
    (
        GroupId       BIGINT       NOT NULL,
        PermissionId  BIGINT       NOT NULL,
        GrantedAtUtc  DATETIME2(3) NOT NULL CONSTRAINT DF_GroupPermissions_GrantedAtUtc DEFAULT SYSUTCDATETIME(),
        GrantedBy     BIGINT       NULL,
        CONSTRAINT PK_GroupPermissions PRIMARY KEY CLUSTERED (GroupId, PermissionId),
        CONSTRAINT FK_GroupPermissions_PermissionGroups FOREIGN KEY (GroupId) REFERENCES dbo.PermissionGroups (Id),
        CONSTRAINT FK_GroupPermissions_Permissions FOREIGN KEY (PermissionId) REFERENCES dbo.Permissions (Id)
    );
END
GO
