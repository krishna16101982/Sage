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
  template: `
    <div class="stack">
      <h1 class="page-title">Sales Users</h1>
      <p class="text-sm text-muted">Accounts for the User Panel. Sales persons report to a sales manager; managers can also sell.</p>

      <div class="toolbar">
        <input class="field field-inline w-72" aria-label="Search sales users" placeholder="Search name, username, email, mobile"
               [(ngModel)]="search" (ngModelChange)="filtersChanged()" />
        <select class="field field-inline" aria-label="Filter by role" [(ngModel)]="role" (ngModelChange)="filtersChanged()">
          <option value="">Any role</option>
          <option value="SalesPerson">Sales persons</option>
          <option value="SalesManager">Sales managers</option>
        </select>
        <select class="field field-inline" aria-label="Filter by status" [(ngModel)]="status" (ngModelChange)="filtersChanged()">
          <option value="">Any status</option>
          <option value="Active">Active</option>
          <option value="Inactive">Inactive</option>
        </select>
        @if (team(); as t) {
          <span class="chip">Team of {{ t.firstName }} {{ t.lastName }}
            <button type="button" class="modal-close" aria-label="Clear team filter" (click)="showTeam(null)">×</button>
          </span>
        }
        <div class="toolbar-spacer"></div>
        <button *appCan="perm.SalesUserAdd" type="button" class="btn btn-primary" (click)="dialog.set({ kind: 'form' })">Add sales user</button>
      </div>

      @if (error()) { <app-alert kind="error">{{ error() }}</app-alert> }
      @if (notice()) { <app-alert kind="success">{{ notice() }}</app-alert> }

      <div class="table-wrap">
        <table class="data-table">
          <thead>
            <tr>
              <th appSort [active]="sortBy === 'name'" [direction]="sortDirection" (sort)="sortOn('name', $event)">Name</th>
              <th appSort [active]="sortBy === 'username'" [direction]="sortDirection" (sort)="sortOn('username', $event)">Username</th>
              <th appSort [active]="sortBy === 'role'" [direction]="sortDirection" (sort)="sortOn('role', $event)">Role</th>
              <th appSort [active]="sortBy === 'manager'" [direction]="sortDirection" (sort)="sortOn('manager', $event)">Manager / Team</th>
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
                  <div class="hint">{{ u.email }}{{ u.mobileNumber ? ' · ' + u.mobileNumber : '' }}</div>
                </td>
                <td class="code">{{ u.username }}</td>
                <td>{{ roleLabel[u.role] }}</td>
                <td>
                  @if (u.role === 'SalesManager') {
                    <button type="button" class="link-button" [disabled]="u.teamSize === 0" (click)="showTeam(u)">Team: {{ u.teamSize }}</button>
                  } @else {
                    {{ u.managerName ?? '-' }}
                  }
                </td>
                <td>
                  {{ u.status }}
                  @if (u.isLockedOut) { <span class="badge badge-danger">locked out</span> }
                  @if (u.mustChangePassword) { <div class="hint">must change password</div> }
                </td>
                <td class="date">{{ formatDate(u.lastLoginAtUtc) }}</td>
                <td class="actions">
                  @if (auth.can(perm.SalesUserEdit)) {
                    <button type="button" class="link-button" (click)="dialog.set({ kind: 'form', user: u })">Edit</button>
                  }
                  @if (auth.can(perm.SalesUserResetPassword)) {
                    <button type="button" class="link-button" (click)="dialog.set({ kind: 'password', user: u })">Reset password</button>
                  }
                  @if (auth.can(perm.SalesUserChangeStatus) && u.status === 'Active' && u.isLockedOut) {
                    <button type="button" class="link-button" (click)="changeStatus(u, 'Active')">Unlock</button>
                  }
                  @if (auth.can(perm.SalesUserChangeStatus)) {
                    <button type="button" class="link-button" [class.danger]="u.status === 'Active'"
                            (click)="changeStatus(u, u.status === 'Active' ? 'Inactive' : 'Active')">
                      {{ u.status === 'Active' ? 'Deactivate' : 'Activate' }}
                    </button>
                  }
                </td>
              </tr>
            } @empty {
              <tr><td colspan="7" class="table-empty">
                {{ loading() ? 'Loading...' : 'No sales users match. Create a sales manager first, then the sales persons who report to them.' }}
              </td></tr>
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
        <app-sales-user-form-dialog [user]="formUser()" (closed)="closeDialog()" (saved)="closeDialog(); load()" />
      }
      @case ('password') {
        <app-reset-password-dialog [name]="fullName(passwordUser()!)" [reset]="resetFor(passwordUser()!)"
                                   (closed)="closeDialog()" (done)="notice.set($event); closeDialog(); load()" />
      }
    }
  `,
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
