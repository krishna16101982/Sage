import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Alert } from '../../../component/alert/alert';
import { Pagination } from '../../../component/pagination/pagination';
import { SortHeader } from '../../../component/sort-header/sort-header';
import { CanDirective } from '../../../directives/can.directive';
import { GroupsService } from '../../../services/groups/groups.service';
import { MessageService } from '../../../services/message/message.service';
import { errorText } from '../../../services/rest/global-rest-service';
import type { PagedResult } from '../../../shared/interfaces/api.interface';
import type { GroupItem, GroupQuery } from '../../../shared/interfaces/group.interface';
import { Perm } from '../../../shared/enumarations/permission-codes';
import { GroupsDeleteModal } from './groups-delete-modal/groups-delete-modal.component';
import { GroupsFilter, type GroupsFilterValue } from './groups-filter/groups-filter.component';

type SortKey = NonNullable<GroupQuery['sortBy']>;

/** /groups/list — permission groups. Add/Edit/Permissions open their own pages; Delete asks for confirmation here. */
@Component({
  selector: 'app-groups-list',
  imports: [RouterLink, CanDirective, Alert, Pagination, SortHeader, GroupsFilter, GroupsDeleteModal],
  templateUrl: './groups-list.component.html',
})
export class GroupsList implements OnInit {
  private readonly service = inject(GroupsService);
  protected readonly perm = Perm;

  filter: GroupsFilterValue = { search: '', status: '' };
  sortBy: SortKey = 'name';
  sortDirection: 'asc' | 'desc' = 'asc';
  page = 1;
  readonly data = signal<PagedResult<GroupItem> | null>(null);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly notice = signal<string | null>(inject(MessageService).take());
  /** The group waiting for delete confirmation. */
  readonly deleting = signal<GroupItem | null>(null);

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
      this.error.set(errorText(err, 'Unable to load groups.'));
    } finally {
      this.loading.set(false);
    }
  }

  onFilter(value: GroupsFilterValue): void {
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

  deleted(): void {
    this.notice.set(`Group ${this.deleting()?.name} deleted.`);
    this.deleting.set(null);
    this.load();
  }
}
