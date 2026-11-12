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
  template: `
    <div class="app-layout">
      <nav aria-label="Main" class="sidebar">
        <div class="sidebar-title">Sage Admin</div>
        <ul class="stack-xs">
          @for (item of items(); track item.href) {
            <li>
              <a class="sidebar-link" [routerLink]="item.href" routerLinkActive #rla="routerLinkActive" [attr.aria-current]="rla.isActive ? 'page' : null">{{ item.label }}</a>
            </li>
          }
        </ul>
      </nav>
      <div class="app-main">
        <header class="app-header">
          <span>{{ auth.user()?.firstName }} {{ auth.user()?.lastName }}</span>
          <button type="button" class="link-button" (click)="signOut()">Sign out</button>
        </header>
        <main class="app-content"><router-outlet /></main>
      </div>
    </div>
  `,
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
