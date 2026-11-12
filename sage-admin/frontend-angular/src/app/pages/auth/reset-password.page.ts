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
  template: `
    <main class="auth-page">
      <div class="auth-card">
        <h1 class="auth-title">Reset password</h1>
        @if (!token()) {
          <app-alert kind="error">This reset link is missing its token. Request a new one.</app-alert>
        } @else {
          <form class="form" (ngSubmit)="submit()">
            @if (error()) { <app-alert kind="error">{{ error() }}</app-alert> }
            <label class="field-label">New password
              <input class="field" name="password" type="password" autocomplete="new-password" required [(ngModel)]="password" />
            </label>
            <label class="field-label">Confirm password
              <input class="field" name="confirm" type="password" autocomplete="new-password" required [(ngModel)]="confirm" />
            </label>
            <p class="hint">{{ hint }}</p>
            <button type="submit" class="btn btn-primary btn-block" [disabled]="busy()">{{ busy() ? 'Please wait...' : 'Reset password' }}</button>
          </form>
        }
        <a routerLink="/login" class="link-button text-sm">Back to sign in</a>
      </div>
    </main>
  `,
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
