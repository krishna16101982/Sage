import { Component, OnInit, inject, signal, viewChild } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Alert } from '../../../component/alert/alert';
import { Pagination } from '../../../component/pagination/pagination';
import { SortHeader } from '../../../component/sort-header/sort-header';
import { CanDirective } from '../../../directives/can.directive';
import { MessageService } from '../../../services/message/message.service';
import { PermissionService } from '../../../services/permission/permission.service';
import { errorText } from '../../../services/rest/global-rest-service';
import { SalesUsersService } from '../../../services/sales-users/sales-users.service';
import type { PagedResult } from '../../../shared/interfaces/api.interface';
import { SALES_ROLE_LABEL, type SalesUser, type SalesUserQuery } from '../../../shared/interfaces/sales-user.interface';
import { Perm } from '../../../shared/enumarations/permission-codes';
import { formatDateTime } from '../../../shared/utils/format';
import { SalesUsersFilter, type SalesUsersFilterValue } from './sales-users-filter/sales-users-filter.component';

type SortKey = NonNullable<SalesUserQuery['sortBy']>;

/** /sales-users/list — User Panel accounts; "Team: N" shows a manager's team. Add/Edit/Reset password open their own pages. */
@Component({
  selector: 'app-sales-users-list',
  imports: [RouterLink, CanDirective, Alert, Pagination, SortHeader, SalesUsersFilter],
  templateUrl: './sales-users-list.component.html',
})
export class SalesUsersList implements OnInit {
  private readonly service = inject(SalesUsersService);
  protected readonly permissions = inject(PermissionService);
  protected readonly perm = Perm;
  protected readonly roleLabel = SALES_ROLE_LABEL;
  protected readonly formatDate = formatDateTime;
  private readonly filterBox = viewChild(SalesUsersFilter);

  filter: SalesUsersFilterValue = { search: '', role: '', status: '' };
  sortBy: SortKey = 'name';
  sortDirection: 'asc' | 'desc' = 'asc';
  page = 1;
  readonly team = signal<SalesUser | null>(null);
  readonly data = signal<PagedResult<SalesUser> | null>(null);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly notice = signal<string | null>(inject(MessageService).take());

  ngOnInit(): void {
    this.load();
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      this.data.set(
        await this.service.list({
          page: this.page,
          pageSize: 15,
          search: this.filter.search.trim() || undefined,
          role: this.filter.role || undefined,
          status: this.filter.status || undefined,
          managerId: this.team()?.id,
          sortBy: this.sortBy,
          sortDirection: this.sortDirection,
        }),
      );
    } catch (err) {
      this.error.set(errorText(err, 'Unable to load sales users.'));
    } finally {
      this.loading.set(false);
    }
  }

  onFilter(value: SalesUsersFilterValue): void {
    this.filter = value;
    this.page = 1;
    this.load();
  }

  /** Show only this manager's team (null clears it). */
  showTeam(manager: SalesUser | null): void {
    this.team.set(manager);
    if (manager) {
      this.filter = { ...this.filter, role: '' };
      this.filterBox()?.clearRole();
    }
    this.page = 1;
    this.load();
  }

  sortOn(key: SortKey, direction: 'asc' | 'desc'): void {
    this.sortBy = key;
    this.sortDirection = direction;
    this.page = 1;
    this.load();
  }

  goTo(page: number): void {
    this.page = page;
    this.load();
  }

  async changeStatus(u: SalesUser, next: 'Active' | 'Inactive'): Promise<void> {
    const name = `${u.firstName} ${u.lastName}`;
    const unlocking = next === 'Active' && u.status === 'Active';
    const question = unlocking ? `Unlock ${name}? They can sign in again straight away.`
      : next === 'Active' ? `Activate ${name}?` : `Deactivate ${name}? They will be signed out of the User Panel.`;
    if (!window.confirm(question)) return;
    this.error.set(null);
    try {
      await this.service.setStatus(u.id, next);
      this.notice.set(`${name} ${unlocking ? 'unlocked' : next === 'Active' ? 'activated' : 'deactivated'}.`);
      this.load();
    } catch (err) {
      this.error.set(errorText(err, 'Unable to change the status.'));
    }
  }
}
