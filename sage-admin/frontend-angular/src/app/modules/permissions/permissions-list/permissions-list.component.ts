import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Alert } from '../../../component/alert/alert';
import { Pagination } from '../../../component/pagination/pagination';
import { CanDirective } from '../../../directives/can.directive';
import { MessageService } from '../../../services/message/message.service';
import { PermissionsService } from '../../../services/permissions/permissions.service';
import { errorText } from '../../../services/rest/global-rest-service';
import type { PagedResult } from '../../../shared/interfaces/api.interface';
import type { ModuleItemDto, PermissionItem } from '../../../shared/interfaces/permission.interface';
import { Perm } from '../../../shared/enumarations/permission-codes';
import { PermissionsTabs } from '../permissions-tabs/permissions-tabs.component';
import { PermissionsFilter, type PermissionsFilterValue } from './permissions-filter/permissions-filter.component';

/** /permissions/list — every permission (Module.Action), filterable; Add/Edit open their own pages. */
@Component({
  selector: 'app-permissions-list',
  imports: [RouterLink, CanDirective, Alert, Pagination, PermissionsTabs, PermissionsFilter],
  templateUrl: './permissions-list.component.html',
})
export class PermissionsList implements OnInit {
  private readonly service = inject(PermissionsService);
  protected readonly perm = Perm;

  filter: PermissionsFilterValue = { search: '', moduleId: '', status: '' };
  page = 1;
  readonly modules = signal<ModuleItemDto[]>([]);
  readonly data = signal<PagedResult<PermissionItem> | null>(null);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly notice = signal<string | null>(inject(MessageService).take());

  ngOnInit(): void {
    this.service.modules(true).then(
      (m) => this.modules.set(m),
      (err) => this.error.set(errorText(err, 'Unable to load modules.')),
    );
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
          moduleId: this.filter.moduleId === '' ? undefined : this.filter.moduleId,
          isActive: this.filter.status === '' ? undefined : this.filter.status === 'true',
          sortBy: 'module',
        }),
      );
    } catch (err) {
      this.error.set(errorText(err, 'Unable to load permissions.'));
    } finally {
      this.loading.set(false);
    }
  }

  onFilter(value: PermissionsFilterValue): void {
    this.filter = value;
    this.page = 1;
    this.load();
  }

  goTo(page: number): void {
    this.page = page;
    this.load();
  }
}
