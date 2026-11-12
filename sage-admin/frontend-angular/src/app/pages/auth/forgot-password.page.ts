import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { errorText } from '../../core/api-client';
import { AuthService } from '../../core/auth.service';
import { AlertComponent } from '../../shared/ui';

@Component({
  selector: 'app-forgot-password-page',
  imports: [FormsModule, RouterLink, AlertComponent],
  templateUrl: './forgot-password.page.html',
})
export class ForgotPasswordPage {
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
