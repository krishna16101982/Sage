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

        /// <summary>Stored procedures (backend/database/StoredProcedures). Use these names, never build a procedure name from input.</summary>
        public static class StoredProcedures
        {
            public const string AdminUserRecordFailedLogin = "dbo.usp_AdminUser_RecordFailedLogin";
            public const string AdminUserRecordSuccessfulLogin = "dbo.usp_AdminUser_RecordSuccessfulLogin";
            public const string AdminUserSetPassword = "dbo.usp_AdminUser_SetPassword";
            public const string AdminUserGetEffectivePermissions = "dbo.usp_AdminUser_GetEffectivePermissions";
            public const string RefreshTokenRotate = "dbo.usp_RefreshToken_Rotate";
            public const string PasswordResetTokenCreate = "dbo.usp_PasswordResetToken_Create";
            public const string ModuleCreate = "dbo.usp_Module_Create";
            public const string ModuleUpdate = "dbo.usp_Module_Update";
            public const string PermissionCreate = "dbo.usp_Permission_Create";
            public const string PermissionUpdate = "dbo.usp_Permission_Update";
            public const string PermissionGetPaged = "dbo.usp_Permission_GetPaged";
            public const string GroupGetPaged = "dbo.usp_Group_GetPaged";
            public const string GroupCreate = "dbo.usp_Group_Create";
            public const string GroupUpdate = "dbo.usp_Group_Update";
            public const string GroupDelete = "dbo.usp_Group_Delete";
            public const string GroupSetPermissions = "dbo.usp_Group_SetPermissions";
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
