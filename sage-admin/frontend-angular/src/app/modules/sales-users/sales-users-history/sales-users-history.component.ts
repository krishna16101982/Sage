import { Component, OnInit, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Alert } from '../../../component/alert/alert';
import { errorText } from '../../../services/rest/global-rest-service';
import { SalesUsersService } from '../../../services/sales-users/sales-users.service';
import type { SalesUserHistoryEntry } from '../../../shared/interfaces/sales-user.interface';
import { formatDateTime } from '../../../shared/utils/format';

/**
 * History of sales users: created, activated / deactivated, manager and role changes, edits and cancellation-request
 * permission, made in the Admin Panel or by the sales manager in the User Panel.
 * With salesUserId: one person (on their edit page). Without: everyone (/sales-users/history, latest 500 changes).
 */
@Component({
  selector: 'app-sales-users-history',
  imports: [RouterLink, Alert],
  templateUrl: './sales-users-history.component.html',
})
export class SalesUsersHistory implements OnInit {
  readonly salesUserId = input<string | null>(null);
  private readonly service = inject(SalesUsersService);
  protected readonly dateTime = formatDateTime;
  readonly entries = signal<SalesUserHistoryEntry[] | null>(null);
  readonly error = signal<string | null>(null);

  async ngOnInit(): Promise<void> {
    try {
      this.entries.set(await this.service.history(this.salesUserId()));
    } catch (err) {
      this.error.set(errorText(err, 'Unable to load the history.'));
    }
  }
}
