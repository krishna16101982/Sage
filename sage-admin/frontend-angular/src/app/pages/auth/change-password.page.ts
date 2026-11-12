import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { errorText } from '../../core/api-client';
import { AuthService } from '../../core/auth.service';
import { AlertComponent, PASSWORD_HINT } from '../../shared/ui';

@Component({
  selector: 'app-change-password-page',
  imports: [FormsModule, AlertComponent],
  templateUrl: './change-password.page.html',
})
export class ChangePasswordPage {
  protected readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  protected readonly hint = PASSWORD_HINT;
  current = '';
  password = '';
  confirm = '';
  readonly error = signal<string | null>(null);
  readonly busy = signal(false);

  async submit(): Promise<void> {
    this.error.set(null);
    this.busy.set(true);
    try {
      await this.auth.changePassword(this.current, this.password, this.confirm);
      this.router.navigateByUrl('/login'); // every session was revoked: sign in again with the new password
    } catch (err) {
      this.error.set(errorText(err));
    } finally {
      this.busy.set(false);
    }
  }
}
