import { Component, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { errorText } from '../../core/api-client';
import { AuthService } from '../../core/auth.service';
import { AlertComponent, PASSWORD_HINT } from '../../shared/ui';

/** Opened from the emailed link: /reset-password?token=... */
@Component({
  selector: 'app-reset-password-page',
  imports: [FormsModule, RouterLink, AlertComponent],
  templateUrl: './reset-password.page.html',
})
export class ResetPasswordPage {
  /** From the query string (?token=...). */
  readonly token = input('');
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  protected readonly hint = PASSWORD_HINT;
  password = '';
  confirm = '';
  readonly error = signal<string | null>(null);
  readonly busy = signal(false);

  async submit(): Promise<void> {
    this.error.set(null);
    this.busy.set(true);
    try {
      await this.auth.resetPassword(this.token(), this.password, this.confirm);
      this.router.navigateByUrl('/login');
    } catch (err) {
      this.error.set(errorText(err));
    } finally {
      this.busy.set(false);
    }
  }
}
