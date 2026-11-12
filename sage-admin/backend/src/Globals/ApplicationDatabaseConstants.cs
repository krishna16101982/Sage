namespace SageAdminApi.Globals
{
    public class ApplicationDatabaseConstants
    {
        public static class TableNames
        {
            public const string AdminUsers = "dbo.AdminUsers";
            public const string AdminUserGroups = "dbo.AdminUserGroups";
            public const string PermissionGroups = "dbo.PermissionGroups";
            public const string GroupPermissions = "dbo.GroupPermissions";
            public const string Permissions = "dbo.Permissions";
            public const string Modules = "dbo.Modules";
            public const string RefreshTokens = "dbo.RefreshTokens";
            public const string PasswordResetTokens = "dbo.PasswordResetTokens";
            public const string LoginHistories = "dbo.LoginHistories";
            public const string AuditLogs = "dbo.AuditLogs";
            public const string Settings = "dbo.Settings";
        }

        public static class ViewNames
        {
            public const string AdminUserEffectivePermissions = "dbo.vw_AdminUserEffectivePermissions";
        }

        public static class ColumnNames
        {
            public const string Id = "Id";
            public const string PublicId = "PublicId";
            public const string Code = "Code";
            public const string Name = "Name";
            public const string Description = "Description";
            public const string Status = "Status";
            public const string Username = "Username";
            public const string Email = "Email";
            public const string PermissionVersion = "PermissionVersion";
            public const string CreatedAtUtc = "CreatedAtUtc";
        }

        public static class Status
        {
            public const string Active = "Active";
            public const string Inactive = "Inactive";
            public const string Locked = "Locked";
        }
    }
}
