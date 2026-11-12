import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { errorText } from '../../core/api-client';
import { AuthService } from '../../core/auth.service';
import { AlertComponent } from '../../shared/ui';

@Component({
  selector: 'app-login-page',
  imports: [FormsModule, AlertComponent],
  template: `
    <main class="auth-page">
      <div class="auth-card">
        <h1 class="auth-title">Sage Sales</h1>
        <form class="form" aria-label="Sign in" (ngSubmit)="submit()">
          @if (error()) { <app-alert kind="error">{{ error() }}</app-alert> }
          <label class="field-label">Username or email
            <input class="field" name="username" autocomplete="username" required [(ngModel)]="username" />
          </label>
          <label class="field-label">Password
            <input class="field" name="password" type="password" autocomplete="current-password" required [(ngModel)]="password" />
          </label>
          <button type="submit" class="btn btn-primary btn-block" [disabled]="busy()">{{ busy() ? 'Please wait...' : 'Sign in' }}</button>
        </form>
        <p class="hint">Forgot your password? Ask your administrator to reset it.</p>
      </div>
    </main>
  `,
})
export class LoginPage {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  username = '';
  password = '';
  readonly error = signal<string | null>(null);
  readonly busy = signal(false);

  async submit(): Promise<void> {
    this.error.set(null);
    this.busy.set(true);
    try {
      const user = await this.auth.login(this.username, this.password);
      this.router.navigateByUrl(user.mustChangePassword ? '/change-password' : '/home');
    } catch (err) {
      this.error.set(errorText(err, 'Unable to sign in. Please try again.'));
    } finally {
      this.busy.set(false);
    }
  }
}
