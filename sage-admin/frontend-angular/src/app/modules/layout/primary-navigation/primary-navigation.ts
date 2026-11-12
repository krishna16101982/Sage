import { Component, computed, inject } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { AuthService } from '../../../services/auth/auth.service';
import { PermissionService } from '../../../services/permission/permission.service';
import { Perm } from '../../../shared/enumarations/permission-codes';

/** Menu items. Each is shown only when the user holds its permission (the API decides the permissions). */
const ITEMS = [
  { href: '/dashboard', label: 'Dashboard', permission: Perm.DashboardView },
  { href: '/users', label: 'Admin Users', permission: Perm.AdminUserView },
  { href: '/groups', label: 'Groups', permission: Perm.GroupView },
  { href: '/permissions', label: 'Permissions', permission: Perm.PermissionView },
  { href: '/products', label: 'Products', permission: Perm.ProductView },
  { href: '/sales-users', label: 'Sales Users', permission: Perm.SalesUserView },
  { href: '/audit-logs', label: 'Audit Logs', permission: Perm.AuditLogView },
  { href: '/settings', label: 'Settings', permission: Perm.SettingView },
];

/** Left-hand navigation. */
@Component({
  selector: 'app-primary-navigation',
  imports: [RouterLink, RouterLinkActive],
  templateUrl: './primary-navigation.html',
})
export class PrimaryNavigation {
  private readonly auth = inject(AuthService);
  private readonly permissions = inject(PermissionService);
  protected readonly items = computed(() => (this.auth.user() ? ITEMS.filter((i) => this.permissions.has(i.permission)) : []));
}
