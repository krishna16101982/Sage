import { Component, inject } from '@angular/core';
import { AuthService } from '../core/auth.service';

/** Landing page after sign-in. The sale page and sales lists arrive in Sprint 9. */
@Component({
  selector: 'app-home-page',
  templateUrl: './home.page.html',
})
export class HomePage {
  protected readonly auth = inject(AuthService);
}
