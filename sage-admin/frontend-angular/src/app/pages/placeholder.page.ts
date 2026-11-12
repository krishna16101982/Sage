import { Component, input } from '@angular/core';

/** Temporary body for screens that are delivered in a later sprint. The title comes from the route's data. */
@Component({
  selector: 'app-placeholder-page',
  templateUrl: './placeholder.page.html',
})
export class PlaceholderPage {
  readonly title = input('');
}

@Component({
  selector: 'app-dashboard-page',
  templateUrl: './dashboard.page.html',
})
export class DashboardPage {}
