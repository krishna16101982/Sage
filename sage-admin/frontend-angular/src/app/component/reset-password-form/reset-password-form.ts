import { Component, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { errorText } from '../../services/rest/global-rest-service';
import { PASSWORD_HINT } from '../../shared/utils/password';
import { Alert } from '../alert/alert';

/** Temporary-password form used by the admin-users and sales-users "Reset password" pages. */
@Component({
  selector: 'app-reset-password-form',
  imports: [FormsModule, RouterLink, Alert],
  templateUrl: './reset-password-form.html',
})
export class ResetPasswordForm {
  /** Calls the API (admin users or sales users). */
  readonly reset = input.required<(password: string, confirm: string) => Promise<unknown>>();
  /** Where Cancel goes. */
  readonly cancelLink = input.required<string>();
  /** Emitted after the password was set. */
  readonly done = output<void>();

  protected readonly hint = PASSWORD_HINT;
  password = '';
  confirm = '';
  readonly error = signal<string | null>(null);
  readonly busy = signal(false);

  async submit(): Promise<void> {
    this.error.set(null);
    this.busy.set(true);
    try {
      await this.reset()(this.password, this.confirm);
      this.done.emit();
    } catch (err) {
      this.error.set(errorText(err));
    } finally {
      this.busy.set(false);
    }
  }
}
