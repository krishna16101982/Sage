import { Component, inject } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from '../../../services/auth/auth.service';

/** Top bar: signed-in user's name and Sign out. */
@Component({
  selector: 'app-primary-header',
  templateUrl: './primary-header.html',
})
export class PrimaryHeader {
  protected readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  async logout(): Promise<void> {
    await this.auth.logout();
    this.router.navigateByUrl('/login');
  }
}
