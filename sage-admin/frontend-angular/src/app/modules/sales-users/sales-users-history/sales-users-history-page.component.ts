import { Component } from '@angular/core';
import { PageHeader } from '../../../component/page-header/page-header';
import { SalesUsersHistory } from './sales-users-history.component';

/** /sales-users/history — every sales user's status, manager and role changes (latest 500). */
@Component({
  selector: 'app-sales-users-history-page',
  imports: [PageHeader, SalesUsersHistory],
  template: `
    <div class="stack">
      <app-page-header title="Sales users history" backLink="/sales-users/list" />
      <p class="text-sm text-muted">
        Who activated or deactivated a manager or sales person, changed a sales person's manager or role, or added a sales person
        (in the Admin Panel, or a manager in the User Panel), and when.
      </p>
      <app-sales-users-history />
    </div>
  `,
})
export class SalesUsersHistoryPage {}
