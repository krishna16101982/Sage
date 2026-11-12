import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Alert } from '../../../component/alert/alert';
import { Pagination } from '../../../component/pagination/pagination';
import { SortHeader } from '../../../component/sort-header/sort-header';
import { CanDirective } from '../../../directives/can.directive';
import { AdminUsersService } from '../../../services/admin-users/admin-users.service';
import { AuthService } from '../../../services/auth/auth.service';
import { MessageService } from '../../../services/message/message.service';
import { PermissionService } from '../../../services/permission/permission.service';
import { errorText } from '../../../services/rest/global-rest-service';
import type { AdminUserItem, AdminUserQuery } from '../../../shared/interfaces/admin-user.interface';
import type { PagedResult } from '../../../shared/interfaces/api.interface';
import { Perm } from '../../../shared/enumarations/permission-codes';
import { formatDateTime } from '../../../shared/utils/format';
import { AdminUsersFilter, type AdminUsersFilterValue } from './admin-users-filter/admin-users-filter.component';

type SortKey = NonNullable<AdminUserQuery['sortBy']>;

/** /users/list — Admin Panel accounts: search, sort, activate/deactivate/unlock; Add/Edit/Reset password open their own pages. */
@Component({
  selector: 'app-admin-users-list',
  imports: [RouterLink, CanDirective, Alert, Pagination, SortHeader, AdminUsersFilter],
  templateUrl: './admin-users-list.component.html',
})
export class AdminUsersList implements OnInit {
  private readonly service = inject(AdminUsersService);
  private readonly auth = inject(AuthService);
  protected readonly permissions = inject(PermissionService);
  protected readonly perm = Perm;
  protected readonly formatDate = formatDateTime;

  filter: AdminUsersFilterValue = { search: '', status: '' };
  sortBy: SortKey = 'name';
  sortDirection: 'asc' | 'desc' = 'asc';
  page = 1;
  readonly data = signal<PagedResult<AdminUserItem> | null>(null);
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
          status: this.filter.status || undefined,
          sortBy: this.sortBy,
          sortDirection: this.sortDirection,
        }),
      );
    } catch (err) {
      this.error.set(errorText(err, 'Unable to load admin users.'));
    } finally {
      this.loading.set(false);
    }
  }

  onFilter(value: AdminUsersFilterValue): void {
    this.filter = value;
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

  /** Only a Super Admin may change a Super Admin account (the API enforces this too). */
  canManage(u: AdminUserItem): boolean {
    return !u.isSuperAdmin || !!this.auth.user()?.isSuperAdmin;
  }

  isMe(u: AdminUserItem): boolean {
    return u.id === this.auth.user()?.id;
  }

  async changeStatus(u: AdminUserItem, next: 'Active' | 'Inactive'): Promise<void> {
    const name = `${u.firstName} ${u.lastName}`;
    const unlocking = next === 'Active' && u.status === 'Active';
    const question = unlocking ? `Unlock ${name}? They can sign in again straight away.`
      : next === 'Active' ? `Activate ${name}?` : `Deactivate ${name}? They will be signed out immediately.`;
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
