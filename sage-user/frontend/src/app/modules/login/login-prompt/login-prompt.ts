import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { Alert } from '../../../component/alert/alert';
import { AuthService } from '../../../services/auth/auth.service';
import { errorText } from '../../../services/rest/global-rest-service';

/** /login — sign in with username or email and password. */
@Component({
  selector: 'app-login-prompt',
  imports: [FormsModule, RouterLink, Alert],
  templateUrl: './login-prompt.html',
})
export class LoginPrompt {
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
