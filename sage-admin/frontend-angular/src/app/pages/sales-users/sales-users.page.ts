import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { SalesUsersApi } from '../../core/admin-api';
import { errorText } from '../../core/api-client';
import { AuthService } from '../../core/auth.service';
import { CanDirective } from '../../core/can.directive';
import type { PagedResult } from '../../core/models/api';
import { SALES_ROLE_LABEL, type SalesRole, type SalesUser, type SalesUserQuery } from '../../core/models/salesUser';
import { Perm } from '../../core/permissionCodes';
import { ResetPasswordDialog } from '../../shared/reset-password.dialog';
import { AlertComponent, PaginationComponent, SortHeaderComponent, formatDateTime } from '../../shared/ui';
import { SalesUserFormDialog } from './sales-user-form.dialog';

type Dialog = { kind: 'none' } | { kind: 'form'; user?: SalesUser } | { kind: 'password'; user: SalesUser };
type SortKey = NonNullable<SalesUserQuery['sortBy']>;

/** User Panel accounts: sales persons and sales managers, managed by admins. */
@Component({
  selector: 'app-sales-users-page',
  imports: [FormsModule, CanDirective, AlertComponent, PaginationComponent, SortHeaderComponent, SalesUserFormDialog, ResetPasswordDialog],
  templateUrl: './sales-users.page.html',
})
export class SalesUsersPage implements OnInit, OnDestroy {
  protected readonly auth = inject(AuthService);
  private readonly api = inject(SalesUsersApi);
  protected readonly perm = Perm;
  protected readonly roleLabel = SALES_ROLE_LABEL;
  protected readonly formatDate = formatDateTime;

  search = '';
  role: '' | SalesRole = '';
  status: '' | 'Active' | 'Inactive' = '';
  sortBy: SortKey = 'name';
  sortDirection: 'asc' | 'desc' = 'asc';
  page = 1;
  readonly team = signal<SalesUser | null>(null);
  readonly data = signal<PagedResult<SalesUser> | null>(null);
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
      this.data.set(
        await this.api.list({
          page: this.page,
          pageSize: 15,
          search: this.search.trim() || undefined,
          role: this.role || undefined,
          status: this.status || undefined,
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

  filtersChanged(): void {
    this.page = 1;
    clearTimeout(this.timer);
    this.timer = setTimeout(() => this.load(), 250);
  }

  /** Show only this manager's team (null clears the filter). */
  showTeam(manager: SalesUser | null): void {
    this.team.set(manager);
    if (manager) this.role = '';
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

  closeDialog(): void {
    this.dialog.set({ kind: 'none' });
  }

  formUser(): SalesUser | undefined {
    const d = this.dialog();
    return d.kind === 'form' ? d.user : undefined;
  }

  passwordUser(): SalesUser | undefined {
    const d = this.dialog();
    return d.kind === 'password' ? d.user : undefined;
  }

  fullName(u: SalesUser): string {
    return `${u.firstName} ${u.lastName}`;
  }

  resetFor(u: SalesUser) {
    return (password: string, confirm: string) => this.api.resetPassword(u.id, password, confirm);
  }

  async changeStatus(u: SalesUser, next: 'Active' | 'Inactive'): Promise<void> {
    const name = this.fullName(u);
    const unlocking = next === 'Active' && u.status === 'Active';
    const question = unlocking ? `Unlock ${name}? They can sign in again straight away.`
      : next === 'Active' ? `Activate ${name}?` : `Deactivate ${name}? They will be signed out of the User Panel.`;
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
