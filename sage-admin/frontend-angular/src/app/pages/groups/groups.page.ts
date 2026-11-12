import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { GroupsApi } from '../../core/admin-api';
import { errorText } from '../../core/api-client';
import { CanDirective } from '../../core/can.directive';
import type { PagedResult } from '../../core/models/api';
import type { GroupItem, GroupQuery, GroupStatus } from '../../core/models/group';
import { Perm } from '../../core/permissionCodes';
import { AlertComponent, PaginationComponent, SortHeaderComponent } from '../../shared/ui';
import { DeleteGroupDialog, GroupFormDialog } from './group-dialogs';
import { GroupPermissionsDialog } from './group-permissions.dialog';

type Dialog =
  | { kind: 'none' }
  | { kind: 'form'; group?: GroupItem }
  | { kind: 'permissions'; group: GroupItem }
  | { kind: 'delete'; group: GroupItem };
type SortKey = NonNullable<GroupQuery['sortBy']>;

/** Permission groups: list, search, create, edit, delete and the permission matrix. */
@Component({
  selector: 'app-groups-page',
  imports: [FormsModule, CanDirective, AlertComponent, PaginationComponent, SortHeaderComponent, GroupFormDialog, DeleteGroupDialog, GroupPermissionsDialog],
  template: `
    <div class="stack">
      <h1 class="page-title">Permission Groups</h1>

      <div class="toolbar">
        <input class="field field-inline w-64" aria-label="Search groups" placeholder="Search name, code or description"
               [(ngModel)]="search" (ngModelChange)="filtersChanged()" />
        <select class="field field-inline" aria-label="Filter by status" [(ngModel)]="status" (ngModelChange)="filtersChanged()">
          <option value="">Any status</option>
          <option value="Active">Active</option>
          <option value="Inactive">Inactive</option>
        </select>
        <div class="toolbar-spacer"></div>
        <button *appCan="perm.GroupAdd" type="button" class="btn btn-primary" (click)="dialog.set({ kind: 'form' })">Add group</button>
      </div>

      @if (error()) { <app-alert kind="error">{{ error() }}</app-alert> }

      <div class="table-wrap">
        <table class="data-table">
          <thead>
            <tr>
              <th appSort [active]="sortBy === 'name'" [direction]="sortDirection" (sort)="sortOn('name', $event)">Name</th>
              <th appSort [active]="sortBy === 'code'" [direction]="sortDirection" (sort)="sortOn('code', $event)">Code</th>
              <th appSort [active]="sortBy === 'userCount'" [direction]="sortDirection" (sort)="sortOn('userCount', $event)">Users</th>
              <th>Permissions</th>
              <th appSort [active]="sortBy === 'status'" [direction]="sortDirection" (sort)="sortOn('status', $event)">Status</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            @for (g of data()?.items ?? []; track g.id) {
              <tr>
                <td>
                  {{ g.name }}
                  @if (g.description) { <div class="hint">{{ g.description }}</div> }
                </td>
                <td class="code">{{ g.code }}</td>
                <td>{{ g.userCount }}</td>
                <td>{{ g.permissionCount }}</td>
                <td>
                  {{ g.status }}
                  @if (g.isSystem) { <span class="badge">system</span> }
                </td>
                <td class="actions">
                  <button type="button" class="link-button" (click)="dialog.set({ kind: 'permissions', group: g })">Permissions</button>
                  <button *appCan="perm.GroupEdit" type="button" class="link-button" (click)="dialog.set({ kind: 'form', group: g })">Edit</button>
                  @if (!g.isSystem) {
                    <button *appCan="perm.GroupDelete" type="button" class="link-button danger" (click)="dialog.set({ kind: 'delete', group: g })">Delete</button>
                  }
                </td>
              </tr>
            } @empty {
              <tr><td colspan="6" class="table-empty">{{ loading() ? 'Loading...' : 'No groups match.' }}</td></tr>
            }
          </tbody>
        </table>
      </div>

      @if (data(); as d) {
        <app-pagination [page]="d.page" [totalPages]="d.totalPages" [totalCount]="d.totalCount" (pageChange)="goTo($event)" />
      }
    </div>

    @switch (dialog().kind) {
      @case ('form') { <app-group-form-dialog [group]="dialogGroup()" (closed)="closeDialog()" (saved)="closeDialog(); load()" /> }
      @case ('permissions') { <app-group-permissions-dialog [group]="dialogGroup()!" (closed)="closeDialog()" (saved)="load()" /> }
      @case ('delete') { <app-delete-group-dialog [group]="dialogGroup()!" (closed)="closeDialog()" (deleted)="closeDialog(); load()" /> }
    }
  `,
})
export class GroupsPage implements OnInit, OnDestroy {
  private readonly api = inject(GroupsApi);
  protected readonly perm = Perm;

  search = '';
  status: '' | GroupStatus = '';
  sortBy: SortKey = 'name';
  sortDirection: 'asc' | 'desc' = 'asc';
  page = 1;
  readonly data = signal<PagedResult<GroupItem> | null>(null);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
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
      this.error.set(errorText(err, 'Unable to load groups.'));
    } finally {
      this.loading.set(false);
    }
  }

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

  dialogGroup(): GroupItem | undefined {
    const d = this.dialog();
    return d.kind === 'none' ? undefined : d.group;
  }
}
