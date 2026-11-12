import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { errorText } from '../../../services/rest/global-rest-service';
import { AuthService } from '../../../services/auth/auth.service';
import { Alert } from '../../../component/alert/alert';

@Component({
  selector: 'app-forgot-password',
  imports: [FormsModule, RouterLink, Alert],
  templateUrl: './forgot-password.html',
})
export class ForgotPassword {
  private readonly auth = inject(AuthService);
  email = '';
  readonly message = signal<string | null>(null);
  readonly error = signal<string | null>(null);
  readonly busy = signal(false);

  async submit(): Promise<void> {
    this.error.set(null);
    this.message.set(null);
    this.busy.set(true);
    try {
      await this.auth.forgotPassword(this.email);
      this.message.set('If the email belongs to an active account, a reset link has been sent.');
    } catch (err) {
      this.error.set(errorText(err));
    } finally {
      this.busy.set(false);
    }
  }
}
