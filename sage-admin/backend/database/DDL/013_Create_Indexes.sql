-- 013: performance indexes (non-unique). Later changes go in new numbered ALTER scripts.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Permissions_ModuleId' AND object_id = OBJECT_ID(N'dbo.Permissions'))
    CREATE NONCLUSTERED INDEX IX_Permissions_ModuleId ON dbo.Permissions (ModuleId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_GroupPermissions_PermissionId' AND object_id = OBJECT_ID(N'dbo.GroupPermissions'))
    CREATE NONCLUSTERED INDEX IX_GroupPermissions_PermissionId ON dbo.GroupPermissions (PermissionId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AdminUserGroups_GroupId' AND object_id = OBJECT_ID(N'dbo.AdminUserGroups'))
    CREATE NONCLUSTERED INDEX IX_AdminUserGroups_GroupId ON dbo.AdminUserGroups (GroupId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_RefreshTokens_UserId_Family' AND object_id = OBJECT_ID(N'dbo.RefreshTokens'))
    CREATE NONCLUSTERED INDEX IX_RefreshTokens_UserId_Family ON dbo.RefreshTokens (UserId, FamilyId) INCLUDE (RevokedAtUtc, ExpiresAtUtc);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PasswordResetTokens_UserId' AND object_id = OBJECT_ID(N'dbo.PasswordResetTokens'))
    CREATE NONCLUSTERED INDEX IX_PasswordResetTokens_UserId ON dbo.PasswordResetTokens (UserId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_LoginHistories_UserId_AttemptedAtUtc' AND object_id = OBJECT_ID(N'dbo.LoginHistories'))
    CREATE NONCLUSTERED INDEX IX_LoginHistories_UserId_AttemptedAtUtc ON dbo.LoginHistories (UserId, AttemptedAtUtc DESC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_LoginHistories_AttemptedAtUtc' AND object_id = OBJECT_ID(N'dbo.LoginHistories'))
    CREATE NONCLUSTERED INDEX IX_LoginHistories_AttemptedAtUtc ON dbo.LoginHistories (AttemptedAtUtc DESC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AuditLogs_CreatedAtUtc' AND object_id = OBJECT_ID(N'dbo.AuditLogs'))
    CREATE NONCLUSTERED INDEX IX_AuditLogs_CreatedAtUtc ON dbo.AuditLogs (CreatedAtUtc DESC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AuditLogs_Entity' AND object_id = OBJECT_ID(N'dbo.AuditLogs'))
    CREATE NONCLUSTERED INDEX IX_AuditLogs_Entity ON dbo.AuditLogs (EntityType, EntityId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AuditLogs_ActorUserId' AND object_id = OBJECT_ID(N'dbo.AuditLogs'))
    CREATE NONCLUSTERED INDEX IX_AuditLogs_ActorUserId ON dbo.AuditLogs (ActorUserId);
GO
