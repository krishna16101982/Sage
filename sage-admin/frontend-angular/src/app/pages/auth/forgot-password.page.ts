import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { errorText } from '../../core/api-client';
import { AuthService } from '../../core/auth.service';
import { AlertComponent } from '../../shared/ui';

@Component({
  selector: 'app-forgot-password-page',
  imports: [FormsModule, RouterLink, AlertComponent],
  template: `
    <main class="auth-page">
      <div class="auth-card">
        <h1 class="auth-title">Forgot password</h1>
        <form class="form" (ngSubmit)="submit()">
          @if (message()) { <app-alert kind="success">{{ message() }}</app-alert> }
          @if (error()) { <app-alert kind="error">{{ error() }}</app-alert> }
          <label class="field-label">Email
            <input class="field" name="email" type="email" autocomplete="email" required [(ngModel)]="email" />
          </label>
          <button type="submit" class="btn btn-primary btn-block" [disabled]="busy()">{{ busy() ? 'Please wait...' : 'Send reset link' }}</button>
        </form>
        <a routerLink="/login" class="link-button text-sm">Back to sign in</a>
      </div>
    </main>
  `,
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
