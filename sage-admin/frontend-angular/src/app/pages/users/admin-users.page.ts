import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AdminUsersApi } from '../../core/admin-api';
import { errorText } from '../../core/api-client';
import { AuthService } from '../../core/auth.service';
import { CanDirective } from '../../core/can.directive';
import type { PagedResult } from '../../core/models/api';
import type { AdminUserItem, AdminUserQuery } from '../../core/models/adminUser';
import { Perm } from '../../core/permissionCodes';
import { ResetPasswordDialog } from '../../shared/reset-password.dialog';
import { AlertComponent, PaginationComponent, SortHeaderComponent, formatDateTime } from '../../shared/ui';
import { AdminUserFormDialog } from './admin-user-form.dialog';

type Dialog = { kind: 'none' } | { kind: 'form'; user?: AdminUserItem } | { kind: 'password'; user: AdminUserItem };
type SortKey = NonNullable<AdminUserQuery['sortBy']>;

/** Admin Panel accounts: create, edit (group), activate/deactivate, unlock, temporary password. */
@Component({
  selector: 'app-admin-users-page',
  imports: [FormsModule, CanDirective, AlertComponent, PaginationComponent, SortHeaderComponent, AdminUserFormDialog, ResetPasswordDialog],
  templateUrl: './admin-users.page.html',
})
export class AdminUsersPage implements OnInit, OnDestroy {
  protected readonly auth = inject(AuthService);
  private readonly api = inject(AdminUsersApi);
  protected readonly perm = Perm;
  protected readonly formatDate = formatDateTime;

  search = '';
  status: '' | 'Active' | 'Inactive' = '';
  sortBy: SortKey = 'name';
  sortDirection: 'asc' | 'desc' = 'asc';
  page = 1;
  readonly data = signal<PagedResult<AdminUserItem> | null>(null);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly notice = signal<string | null>(null);
  readonly dialog = signal<Dialog>({ kind: 'none' });
  private timer: ReturnType<typeof setTimeout> | undefined;

  ngOnInit(): void {
    this.load();
  }

  ngOnDestroy(): void {
    clearTimeout(this.timer);
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      this.data.set(await this.api.list({ page: this.page, pageSize: 15, search: this.search.trim() || undefined, status: this.status || undefined, sortBy: this.sortBy, sortDirection: this.sortDirection }));
    } catch (err) {
      this.error.set(errorText(err, 'Unable to load admin users.'));
    } finally {
      this.loading.set(false);
    }
  }

  /** Typing in the search box waits a moment before reloading. */
  filtersChanged(): void {
    this.page = 1;
    clearTimeout(this.timer);
    this.timer = setTimeout(() => this.load(), 250);
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

  closeDialog(): void {
    this.dialog.set({ kind: 'none' });
  }

  formUser(): AdminUserItem | undefined {
    const d = this.dialog();
    return d.kind === 'form' ? d.user : undefined;
  }

  passwordUser(): AdminUserItem | undefined {
    const d = this.dialog();
    return d.kind === 'password' ? d.user : undefined;
  }

  fullName(u: AdminUserItem): string {
    return `${u.firstName} ${u.lastName}`;
  }

  resetFor(u: AdminUserItem) {
    return (password: string, confirm: string) => this.api.resetPassword(u.id, password, confirm);
  }

  /** Only a Super Admin may change a Super Admin account (the API enforces this too). */
  canManage(u: AdminUserItem): boolean {
    return !u.isSuperAdmin || !!this.auth.user()?.isSuperAdmin;
  }

  isMe(u: AdminUserItem): boolean {
    return u.id === this.auth.user()?.id;
  }

  async changeStatus(u: AdminUserItem, next: 'Active' | 'Inactive'): Promise<void> {
    const name = this.fullName(u);
    const unlocking = next === 'Active' && u.status === 'Active';
    const question = unlocking ? `Unlock ${name}? They can sign in again straight away.`
      : next === 'Active' ? `Activate ${name}?` : `Deactivate ${name}? They will be signed out immediately.`;
    if (!window.confirm(question)) return;
    this.error.set(null);
    try {
      await this.api.setStatus(u.id, next);
      this.notice.set(`${name} ${unlocking ? 'unlocked' : next === 'Active' ? 'activated' : 'deactivated'}.`);
      this.load();
    } catch (err) {
      this.error.set(errorText(err, 'Unable to change the status.'));
    }
  }
}
