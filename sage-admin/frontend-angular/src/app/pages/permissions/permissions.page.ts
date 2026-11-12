import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { PermissionsApi } from '../../core/admin-api';
import { errorText } from '../../core/api-client';
import { CanDirective } from '../../core/can.directive';
import type { PagedResult } from '../../core/models/api';
import type { ModuleItemDto, PermissionItem } from '../../core/models/permission';
import { Perm } from '../../core/permissionCodes';
import { AlertComponent, PaginationComponent } from '../../shared/ui';
import { ModuleFormDialog, PermissionFormDialog } from './permission-dialogs';

type Tab = 'permissions' | 'modules';
type Dialog = { kind: 'none' } | { kind: 'module'; module?: ModuleItemDto } | { kind: 'permission'; permission?: PermissionItem };

/** Permission management: modules and the permissions inside them. Everything comes from SQL via the API. */
@Component({
  selector: 'app-permissions-page',
  imports: [FormsModule, CanDirective, AlertComponent, PaginationComponent, ModuleFormDialog, PermissionFormDialog],
  templateUrl: './permissions.page.html',
})
export class PermissionsPage implements OnInit, OnDestroy {
  private readonly api = inject(PermissionsApi);
  protected readonly perm = Perm;

  readonly tab = signal<Tab>('permissions');
  readonly modules = signal<ModuleItemDto[]>([]);
  search = '';
  moduleId: number | '' = '';
  status: '' | 'true' | 'false' = '';
  page = 1;
  readonly data = signal<PagedResult<PermissionItem> | null>(null);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly dialog = signal<Dialog>({ kind: 'none' });
  private timer: ReturnType<typeof setTimeout> | undefined;

  ngOnInit(): void {
    this.loadModules();
    this.load();
  }

  ngOnDestroy(): void {
    clearTimeout(this.timer);
  }

  async loadModules(): Promise<void> {
    try {
      this.modules.set(await this.api.modules(true));
    } catch (err) {
      this.error.set(errorText(err, 'Unable to load modules.'));
    }
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
          moduleId: this.moduleId === '' ? undefined : Number(this.moduleId),
          isActive: this.status === '' ? undefined : this.status === 'true',
          sortBy: 'module',
        }),
      );
    } catch (err) {
      this.error.set(errorText(err, 'Unable to load permissions.'));
    } finally {
      this.loading.set(false);
    }
  }

  filtersChanged(): void {
    this.page = 1;
    clearTimeout(this.timer);
    this.timer = setTimeout(() => this.load(), 250);
  }

  goTo(page: number): void {
    this.page = page;
    this.load();
  }

  closeDialog(): void {
    this.dialog.set({ kind: 'none' });
  }

  savedChange(): void {
    this.closeDialog();
    this.loadModules();
    this.load();
  }

  dialogModule(): ModuleItemDto | undefined {
    const d = this.dialog();
    return d.kind === 'module' ? d.module : undefined;
  }

  dialogPermission(): PermissionItem | undefined {
    const d = this.dialog();
    return d.kind === 'permission' ? d.permission : undefined;
  }
}
