import { Component, computed, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../core/auth.service';

/** Navigation items. Visibility is decided by the permissions the API returns for the user. */
const ITEMS = [
  { href: '/dashboard', label: 'Dashboard', permission: 'Dashboard.View' },
  { href: '/users', label: 'Admin Users', permission: 'AdminUser.View' },
  { href: '/groups', label: 'Groups', permission: 'Group.View' },
  { href: '/permissions', label: 'Permissions', permission: 'Permission.View' },
  { href: '/products', label: 'Products', permission: 'Product.View' },
  { href: '/sales-users', label: 'Sales Users', permission: 'SalesUser.View' },
  { href: '/audit-logs', label: 'Audit Logs', permission: 'AuditLog.View' },
  { href: '/settings', label: 'Settings', permission: 'Setting.View' },
];

/** Signed-in layout: sidebar, header with the user's name and Sign out, and the current page. */
@Component({
  selector: 'app-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './shell.component.html',
})
export class ShellComponent {
  protected readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  protected readonly items = computed(() => (this.auth.user() ? ITEMS.filter((i) => this.auth.can(i.permission)) : []));

  async signOut(): Promise<void> {
    await this.auth.logout();
    this.router.navigateByUrl('/login');
  }
}
