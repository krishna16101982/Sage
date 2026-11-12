import { Component, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../core/auth.service';

/** Signed-in layout: navigation, header with the user's name, role and Sign out, and the current page. */
@Component({
  selector: 'app-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  template: `
    <div class="app-layout">
      <nav aria-label="Main" class="sidebar">
        <div class="sidebar-title">Sage Sales</div>
        <ul class="stack-xs">
          @for (item of items; track item.href) {
            <li>
              <a class="sidebar-link" [routerLink]="item.href" routerLinkActive #rla="routerLinkActive" [attr.aria-current]="rla.isActive ? 'page' : null">{{ item.label }}</a>
            </li>
          }
        </ul>
      </nav>
      <div class="app-main">
        <header class="app-header">
          <span>
            {{ auth.user()?.firstName }} {{ auth.user()?.lastName }}
            <span class="badge">{{ auth.isManager() ? 'Sales manager' : 'Sales person' }}</span>
          </span>
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
  /** Sales screens (new sale, my sales, team sales) are added in Sprint 9. */
  protected readonly items = [
    { href: '/home', label: 'Home' },
    { href: '/change-password', label: 'Change password' },
  ];

  async signOut(): Promise<void> {
    await this.auth.logout();
    this.router.navigateByUrl('/login');
  }
}
