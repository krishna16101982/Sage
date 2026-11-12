import { Component, inject } from '@angular/core';
import { AuthService } from '../../services/auth/auth.service';

/** Landing page after sign-in. The sale page and sales lists arrive in Sprint 9. */
@Component({
  selector: 'app-home',
  templateUrl: './home.html',
})
export class Home {
  protected readonly auth = inject(AuthService);
}
