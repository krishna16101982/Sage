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
  template: `
    <div class="stack">
      <h1 class="page-title">Permissions</h1>
      @if (error()) { <app-alert kind="error">{{ error() }}</app-alert> }

      <div role="tablist" class="tabs">
        <button role="tab" class="tab" [attr.aria-selected]="tab() === 'permissions'" (click)="tab.set('permissions')">Permissions</button>
        <button role="tab" class="tab" [attr.aria-selected]="tab() === 'modules'" (click)="tab.set('modules')">Modules</button>
      </div>

      @if (tab() === 'permissions') {
        <div class="toolbar">
          <input class="field field-inline w-64" aria-label="Search permissions" placeholder="Search code, name or module"
                 [(ngModel)]="search" (ngModelChange)="filtersChanged()" />
          <select class="field field-inline" aria-label="Filter by module" [(ngModel)]="moduleId" (ngModelChange)="filtersChanged()">
            <option value="">All modules</option>
            @for (m of modules(); track m.id) { <option [value]="m.id">{{ m.name }}</option> }
          </select>
          <select class="field field-inline" aria-label="Filter by status" [(ngModel)]="status" (ngModelChange)="filtersChanged()">
            <option value="">Any status</option>
            <option value="true">Active</option>
            <option value="false">Inactive</option>
          </select>
          <div class="toolbar-spacer"></div>
          <button *appCan="perm.PermissionAdd" type="button" class="btn btn-primary" (click)="dialog.set({ kind: 'permission' })">Add permission</button>
        </div>

        <div class="table-wrap">
          <table class="data-table">
            <thead><tr><th>Code</th><th>Name</th><th>Module</th><th>Status</th><th></th></tr></thead>
            <tbody>
              @for (p of data()?.items ?? []; track p.id) {
                <tr>
                  <td class="code">{{ p.code }}</td>
                  <td>{{ p.name }} @if (p.description) { <div class="hint">{{ p.description }}</div> }</td>
                  <td>{{ p.moduleName }}</td>
                  <td>{{ p.isActive ? 'Active' : 'Inactive' }} @if (p.isSystem) { <span class="badge">system</span> }</td>
                  <td class="actions">
                    <button *appCan="perm.PermissionEdit" type="button" class="link-button" (click)="dialog.set({ kind: 'permission', permission: p })">Edit</button>
                  </td>
                </tr>
              } @empty {
                <tr><td colspan="5" class="table-empty">{{ loading() ? 'Loading...' : 'No permissions match.' }}</td></tr>
              }
            </tbody>
          </table>
        </div>
        @if (data(); as d) {
          <app-pagination [page]="d.page" [totalPages]="d.totalPages" [totalCount]="d.totalCount" (pageChange)="goTo($event)" />
        }
      } @else {
        <div class="toolbar">
          <div class="toolbar-spacer"></div>
          <button *appCan="perm.PermissionAdd" type="button" class="btn btn-primary" (click)="dialog.set({ kind: 'module' })">Add module</button>
        </div>
        <div class="table-wrap">
          <table class="data-table">
            <thead><tr><th>Code</th><th>Name</th><th>Category</th><th>Order</th><th>Permissions</th><th>Status</th><th></th></tr></thead>
            <tbody>
              @for (m of modules(); track m.id) {
                <tr>
                  <td class="code">{{ m.code }}</td>
                  <td>{{ m.name }}</td>
                  <td>{{ m.category ?? '-' }}</td>
                  <td>{{ m.sortOrder }}</td>
                  <td>{{ m.permissionCount }}</td>
                  <td>{{ m.isActive ? 'Active' : 'Inactive' }} @if (m.isSystem) { <span class="badge">system</span> }</td>
                  <td class="actions">
                    <button *appCan="perm.PermissionEdit" type="button" class="link-button" (click)="dialog.set({ kind: 'module', module: m })">Edit</button>
                  </td>
                </tr>
              } @empty {
                <tr><td colspan="7" class="table-empty">No modules.</td></tr>
              }
            </tbody>
          </table>
        </div>
      }
    </div>

    @switch (dialog().kind) {
      @case ('module') { <app-module-form-dialog [module]="dialogModule()" (closed)="closeDialog()" (saved)="savedChange()" /> }
      @case ('permission') { <app-permission-form-dialog [permission]="dialogPermission()" [modules]="modules()" (closed)="closeDialog()" (saved)="savedChange()" /> }
    }
  `,
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
