import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { errorText } from '../../core/api-client';
import { AuthService } from '../../core/auth.service';
import { AlertComponent } from '../../shared/ui';

@Component({
  selector: 'app-login-page',
  imports: [FormsModule, AlertComponent],
  templateUrl: './login.page.html',
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
