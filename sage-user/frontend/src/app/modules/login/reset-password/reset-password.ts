import { Component, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Alert } from '../../../component/alert/alert';
import { AuthService } from '../../../services/auth/auth.service';
import { errorText } from '../../../services/rest/global-rest-service';
import { PASSWORD_HINT } from '../../../shared/utils/password';

/** /reset-password?token=... — opened from the emailed link; sets a new password (the link works once). */
@Component({
  selector: 'app-reset-password',
  imports: [FormsModule, RouterLink, Alert],
  templateUrl: './reset-password.html',
})
export class ResetPassword {
  /** From the query string (?token=...). */
  readonly token = input('');
  private readonly auth = inject(AuthService);
  protected readonly hint = PASSWORD_HINT;
  password = '';
  confirm = '';
  readonly done = signal(false);
  readonly error = signal<string | null>(null);
  readonly busy = signal(false);

  async submit(): Promise<void> {
    this.error.set(null);
    if (this.password !== this.confirm) {
      this.error.set('The two passwords are not the same.');
      return;
    }
    this.busy.set(true);
    try {
      await this.auth.resetPassword(this.token(), this.password, this.confirm);
      this.done.set(true);
    } catch (err) {
      this.error.set(errorText(err));
    } finally {
      this.busy.set(false);
    }
  }
}
