namespace SageAdminApi.Globals
{
    /// <summary>
    /// Permission codes (Module.Action) that endpoints require. The permission catalogue itself is dynamic and lives in SQL
    /// (dbo.Modules / dbo.Permissions); these constants only name the codes the API enforces. Must match SeedData.
    /// </summary>
    public class PermissionConstants
    {
        //Dashboard
        internal const string DashboardView = "Dashboard.View";

        //Admin users
        internal const string AdminUserView = "AdminUser.View";
        internal const string AdminUserAdd = "AdminUser.Add";
        internal const string AdminUserEdit = "AdminUser.Edit";
        internal const string AdminUserChangeStatus = "AdminUser.ChangeStatus";
        internal const string AdminUserResetPassword = "AdminUser.ResetPassword";

        //Groups
        internal const string GroupView = "Group.View";
        internal const string GroupAdd = "Group.Add";
        internal const string GroupEdit = "Group.Edit";
        internal const string GroupDelete = "Group.Delete";
        internal const string GroupAssignPermissions = "Group.AssignPermissions";

        //Permissions
        internal const string PermissionView = "Permission.View";
        internal const string PermissionAdd = "Permission.Add";
        internal const string PermissionEdit = "Permission.Edit";

        //Product catalog (categories, products, plans / price list)
        internal const string ProductView = "Product.View";
        internal const string ProductAdd = "Product.Add";
        internal const string ProductEdit = "Product.Edit";

        //Audit logs
        internal const string AuditLogView = "AuditLog.View";

        //Settings
        internal const string SettingView = "Setting.View";
        internal const string SettingEdit = "Setting.Edit";
    }
}
