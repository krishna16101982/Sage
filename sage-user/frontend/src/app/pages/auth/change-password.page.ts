import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { errorText } from '../../core/api-client';
import { AuthService } from '../../core/auth.service';
import { AlertComponent, PASSWORD_HINT } from '../../shared/ui';

@Component({
  selector: 'app-change-password-page',
  imports: [FormsModule, AlertComponent],
  template: `
    <div class="stack max-w-sm">
      <h1 class="page-title">Change password</h1>
      @if (auth.user()?.mustChangePassword) {
        <app-alert kind="success">You must choose a new password before continuing.</app-alert>
      }
      <form class="form" (ngSubmit)="submit()">
        @if (error()) { <app-alert kind="error">{{ error() }}</app-alert> }
        <label class="field-label">Current password
          <input class="field" name="current" type="password" autocomplete="current-password" required [(ngModel)]="current" />
        </label>
        <label class="field-label">New password
          <input class="field" name="password" type="password" autocomplete="new-password" required [(ngModel)]="password" />
        </label>
        <label class="field-label">Confirm new password
          <input class="field" name="confirm" type="password" autocomplete="new-password" required [(ngModel)]="confirm" />
        </label>
        <p class="hint">{{ hint }}</p>
        <button type="submit" class="btn btn-primary btn-block" [disabled]="busy()">{{ busy() ? 'Please wait...' : 'Change password' }}</button>
      </form>
    </div>
  `,
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
