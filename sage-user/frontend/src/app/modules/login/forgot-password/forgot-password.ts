import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Alert } from '../../../component/alert/alert';
import { AuthService } from '../../../services/auth/auth.service';
import { errorText } from '../../../services/rest/global-rest-service';

/**
 * /forgot-password — the sales user enters their email and gets a one-time reset link. The answer is the same whether or
 * not the email has an account, so the page never reveals who works here.
 */
@Component({
  selector: 'app-forgot-password',
  imports: [FormsModule, RouterLink, Alert],
  templateUrl: './forgot-password.html',
})
export class ForgotPassword {
  private readonly auth = inject(AuthService);
  email = '';
  readonly sent = signal(false);
  readonly error = signal<string | null>(null);
  readonly busy = signal(false);

  async submit(): Promise<void> {
    this.error.set(null);
    if (!this.email.trim()) {
      this.error.set('Enter your email address.');
      return;
    }
    this.busy.set(true);
    try {
      await this.auth.forgotPassword(this.email.trim());
      this.sent.set(true);
    } catch (err) {
      this.error.set(errorText(err));
    } finally {
      this.busy.set(false);
    }
  }
}
