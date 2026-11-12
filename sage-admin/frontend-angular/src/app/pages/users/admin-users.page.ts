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
  template: `
    <div class="stack">
      <h1 class="page-title">Admin Users</h1>
      <p class="text-sm text-muted">Accounts for this Admin Panel. A user's permission group decides what they can see and do.</p>

      <div class="toolbar">
        <input class="field field-inline w-72" aria-label="Search admin users" placeholder="Search name, username, email, mobile"
               [(ngModel)]="search" (ngModelChange)="filtersChanged()" />
        <select class="field field-inline" aria-label="Filter by status" [(ngModel)]="status" (ngModelChange)="filtersChanged()">
          <option value="">Any status</option>
          <option value="Active">Active</option>
          <option value="Inactive">Inactive</option>
        </select>
        <div class="toolbar-spacer"></div>
        <button *appCan="perm.AdminUserAdd" type="button" class="btn btn-primary" (click)="dialog.set({ kind: 'form' })">Add admin user</button>
      </div>

      @if (error()) { <app-alert kind="error">{{ error() }}</app-alert> }
      @if (notice()) { <app-alert kind="success">{{ notice() }}</app-alert> }

      <div class="table-wrap">
        <table class="data-table">
          <thead>
            <tr>
              <th appSort [active]="sortBy === 'name'" [direction]="sortDirection" (sort)="sortOn('name', $event)">Name</th>
              <th appSort [active]="sortBy === 'username'" [direction]="sortDirection" (sort)="sortOn('username', $event)">Username</th>
              <th appSort [active]="sortBy === 'group'" [direction]="sortDirection" (sort)="sortOn('group', $event)">Group</th>
              <th appSort [active]="sortBy === 'status'" [direction]="sortDirection" (sort)="sortOn('status', $event)">Status</th>
              <th appSort [active]="sortBy === 'lastLoginAtUtc'" [direction]="sortDirection" (sort)="sortOn('lastLoginAtUtc', $event)">Last sign-in</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            @for (u of data()?.items ?? []; track u.id) {
              <tr>
                <td>
                  {{ u.firstName }} {{ u.lastName }}
                  @if (isMe(u)) { <span class="badge">(you)</span> }
                  <div class="hint">{{ u.email }}{{ u.mobileNumber ? ' · ' + u.mobileNumber : '' }}</div>
                </td>
                <td class="code">{{ u.username }}</td>
                <td>
                  @if (u.isSuperAdmin) {
                    <span class="badge badge-warning">Super Admin</span>
                  } @else if (u.groupName) {
                    {{ u.groupName }}
                    @if (!u.groupIsActive) { <span class="badge badge-danger">group inactive</span> }
                  } @else {
                    <span class="text-muted">No group</span>
                  }
                </td>
                <td>
                  {{ u.status }}
                  @if (u.isLockedOut) { <span class="badge badge-danger">locked out</span> }
                  @if (u.mustChangePassword) { <div class="hint">must change password</div> }
                </td>
                <td class="date">{{ formatDate(u.lastLoginAtUtc) }}</td>
                <td class="actions">
                  @if (canManage(u)) {
                    @if (auth.can(perm.AdminUserEdit)) {
                      <button type="button" class="link-button" (click)="dialog.set({ kind: 'form', user: u })">Edit</button>
                    }
                    @if (auth.can(perm.AdminUserResetPassword) && !isMe(u)) {
                      <button type="button" class="link-button" (click)="dialog.set({ kind: 'password', user: u })">Reset password</button>
                    }
                    @if (auth.can(perm.AdminUserChangeStatus) && u.status === 'Active' && u.isLockedOut) {
                      <button type="button" class="link-button" (click)="changeStatus(u, 'Active')">Unlock</button>
                    }
                    @if (auth.can(perm.AdminUserChangeStatus) && !isMe(u)) {
                      <button type="button" class="link-button" [class.danger]="u.status === 'Active'"
                              (click)="changeStatus(u, u.status === 'Active' ? 'Inactive' : 'Active')">
                        {{ u.status === 'Active' ? 'Deactivate' : 'Activate' }}
                      </button>
                    }
                  }
                </td>
              </tr>
            } @empty {
              <tr><td colspan="6" class="table-empty">{{ loading() ? 'Loading...' : 'No admin users match.' }}</td></tr>
            }
          </tbody>
        </table>
      </div>

      @if (data(); as d) {
        <app-pagination [page]="d.page" [totalPages]="d.totalPages" [totalCount]="d.totalCount" (pageChange)="goTo($event)" />
      }
    </div>

    @switch (dialog().kind) {
      @case ('form') {
        <app-admin-user-form-dialog [user]="formUser()" (closed)="closeDialog()" (saved)="closeDialog(); load()" />
      }
      @case ('password') {
        <app-reset-password-dialog [name]="fullName(passwordUser()!)" [reset]="resetFor(passwordUser()!)"
                                   (closed)="closeDialog()" (done)="notice.set($event); closeDialog(); load()" />
      }
    }
  `,
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
