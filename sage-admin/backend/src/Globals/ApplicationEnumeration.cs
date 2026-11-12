namespace SageAdminApi.Globals
{
    public class ApplicationEnumeration
    {
        public enum ObjectType
        {
            AdminUser,
            PermissionGroup,
            Permission,
            Module,
            AuditLog,
            Setting,
            Dashboard,
            ProductCategory,
            Product,
            ProductPlan
        }

        public enum AuditAction
        {
            Create,
            Update,
            Delete,
            StatusChange,
            AssignPermissions,
            AssignGroup,
            Login,
            LoginFailed,
            Logout,
            PasswordChange,
            PasswordReset
        }
    }
}
