import { Component, OnInit, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Alert } from '../../../component/alert/alert';
import { Pagination } from '../../../component/pagination/pagination';
import { errorText } from '../../../services/rest/global-rest-service';
import { SalesUsersService } from '../../../services/sales-users/sales-users.service';
import type { PagedResult } from '../../../shared/interfaces/api.interface';
import type { SalesUserHistoryEntry } from '../../../shared/interfaces/sales-user.interface';
import { formatDateTime } from '../../../shared/utils/format';

/**
 * History of sales users: created, activated / deactivated, manager and role changes, edits and cancellation-request
 * permission, made in the Admin Panel or by the sales manager in the User Panel.
 * With salesUserId: one person (on their edit page). Without: everyone (/sales-users/history).
 * One page at a time from the database (dbo.usp_SalesUserHistory_GetPaged).
 */
@Component({
  selector: 'app-sales-users-history',
  imports: [RouterLink, Alert, Pagination],
  templateUrl: './sales-users-history.component.html',
})
export class SalesUsersHistory implements OnInit {
  readonly salesUserId = input<string | null>(null);
  private readonly service = inject(SalesUsersService);
  protected readonly dateTime = formatDateTime;
  readonly data = signal<PagedResult<SalesUserHistoryEntry> | null>(null);
  readonly error = signal<string | null>(null);
  page = 1;

  async ngOnInit(): Promise<void> {
    await this.load();
  }

  async goTo(page: number): Promise<void> {
    this.page = page;
    await this.load();
  }

  private async load(): Promise<void> {
    try {
      this.data.set(await this.service.history(this.salesUserId(), this.page, 25));
    } catch (err) {
      this.error.set(errorText(err, 'Unable to load the history.'));
    }
  }
}
