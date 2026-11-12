/**
 * Codes the UI uses to show or hide screens and buttons. This is NOT the permission catalogue: the catalogue
 * (modules and permissions) lives in SQL and is loaded from the API. The backend enforces every code independently.
 */
export const Perm = {
  DashboardView: "Dashboard.View",
  AdminUserView: "AdminUser.View",
  AdminUserAdd: "AdminUser.Add",
  AdminUserEdit: "AdminUser.Edit",
  GroupView: "Group.View",
  GroupAdd: "Group.Add",
  GroupEdit: "Group.Edit",
  GroupDelete: "Group.Delete",
  GroupAssignPermissions: "Group.AssignPermissions",
  PermissionView: "Permission.View",
  PermissionAdd: "Permission.Add",
  PermissionEdit: "Permission.Edit",
  AuditLogView: "AuditLog.View",
  SettingView: "Setting.View",
  SettingEdit: "Setting.Edit",
} as const;
