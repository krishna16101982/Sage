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
  templateUrl: './groups.page.html',
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
