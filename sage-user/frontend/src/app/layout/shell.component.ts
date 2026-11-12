import { Component, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../core/auth.service';

/** Signed-in layout: navigation, header with the user's name, role and Sign out, and the current page. */
@Component({
  selector: 'app-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './shell.component.html',
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
