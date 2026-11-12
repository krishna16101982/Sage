import { Component, input } from '@angular/core';

/** Temporary body for screens that are delivered in a later sprint. The title comes from the route's data. */
@Component({
  selector: 'app-placeholder-page',
  template: `
    <div class="stack-sm">
      <h1 class="page-title">{{ title() }}</h1>
      <p class="text-muted text-sm">This screen is coming in a later sprint.</p>
    </div>
  `,
})
export class PlaceholderPage {
  readonly title = input('');
}

@Component({
  selector: 'app-dashboard-page',
  template: `<h1 class="page-title">Dashboard</h1>`,
})
export class DashboardPage {}
