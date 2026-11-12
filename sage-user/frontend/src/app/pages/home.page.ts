import { Component, inject } from '@angular/core';
import { AuthService } from '../core/auth.service';

/** Landing page after sign-in. The sale page and sales lists arrive in Sprint 9. */
@Component({
  selector: 'app-home-page',
  template: `
    @if (auth.user(); as u) {
      <div class="stack max-w-sm" style="max-width: 40rem">
        <h1 class="page-title">Welcome, {{ u.firstName }}</h1>
        <div class="card">
          <div class="card-body stack-sm text-sm">
            <div><span class="text-muted">Role:</span> {{ u.isManager ? 'Sales manager' : 'Sales person' }}</div>
            @if (u.managerName) { <div><span class="text-muted">Your manager:</span> {{ u.managerName }}</div> }
            <div><span class="text-muted">Username:</span> <span class="code">{{ u.username }}</span></div>
            <div><span class="text-muted">Email:</span> {{ u.email }}</div>
          </div>
        </div>
        <p class="text-sm text-muted">
          Recording sales{{ u.isManager ? ' and viewing your team\\'s sales' : '' }} is coming in the next release.
        </p>
      </div>
    }
  `,
})
export class HomePage {
  protected readonly auth = inject(AuthService);
}
