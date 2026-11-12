import { Component, OnInit, computed, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { PermissionsApi } from '../../core/admin-api';
import { errorText } from '../../core/api-client';
import type { ModuleItemDto, PermissionItem } from '../../core/models/permission';
import { AlertComponent, ModalComponent } from '../../shared/ui';

/** Create (no `module`) or edit a module. The code is only editable on create: permission codes derive from it. */
@Component({
  selector: 'app-module-form-dialog',
  imports: [FormsModule, ModalComponent, AlertComponent],
  template: `
    <app-modal [title]="module() ? 'Edit module' : 'Add module'" (closed)="closed.emit()">
      <form class="form" (ngSubmit)="submit()">
        @if (error()) { <app-alert kind="error">{{ error() }}</app-alert> }
        <label class="field-label">Code <input class="field" name="code" required placeholder="e.g. Inventory" [disabled]="!!module()" [(ngModel)]="code" /></label>
        <label class="field-label">Name <input class="field" name="name" required maxlength="100" [(ngModel)]="name" /></label>
        <label class="field-label">Category <input class="field" name="category" maxlength="100" placeholder="e.g. HR, Sales" [(ngModel)]="category" /></label>
        <label class="field-label">Sort order <input class="field" name="sortOrder" type="number" [(ngModel)]="sortOrder" /></label>
        @if (module(); as m) {
          <label class="checkbox-label">
            <input type="checkbox" name="active" [disabled]="m.isSystem" [(ngModel)]="active" /> Active
            @if (m.isSystem) { <span class="hint">(system modules cannot be deactivated)</span> }
          </label>
        }
        <button type="submit" class="btn btn-primary btn-block" [disabled]="busy()">{{ busy() ? 'Please wait...' : module() ? 'Save changes' : 'Create module' }}</button>
      </form>
    </app-modal>
  `,
})
export class ModuleFormDialog implements OnInit {
  readonly module = input<ModuleItemDto>();
  readonly closed = output<void>();
  readonly saved = output<void>();
  private readonly api = inject(PermissionsApi);

  code = '';
  name = '';
  category = '';
  sortOrder = 0;
  active = true;
  readonly error = signal<string | null>(null);
  readonly busy = signal(false);

  ngOnInit(): void {
    const m = this.module();
    if (m) {
      this.code = m.code;
      this.name = m.name;
      this.category = m.category ?? '';
      this.sortOrder = m.sortOrder;
      this.active = m.isActive;
    }
  }

  async submit(): Promise<void> {
    this.error.set(null);
    this.busy.set(true);
    try {
      const m = this.module();
      const order = Number(this.sortOrder) || 0;
      if (m) await this.api.updateModule(m.id, { name: this.name, category: this.category || undefined, sortOrder: order, isActive: this.active });
      else await this.api.createModule({ code: this.code, name: this.name, category: this.category || undefined, sortOrder: order });
      this.saved.emit();
    } catch (err) {
      this.error.set(errorText(err));
    } finally {
      this.busy.set(false);
    }
  }
}

/** Create (no `permission`) or edit a permission. Module and action are fixed after creation (they form the code). */
@Component({
  selector: 'app-permission-form-dialog',
  imports: [FormsModule, ModalComponent, AlertComponent],
  template: `
    <app-modal [title]="permission() ? 'Edit ' + permission()!.code : 'Add permission'" (closed)="closed.emit()">
      <form class="form" (ngSubmit)="submit()">
        @if (error()) { <app-alert kind="error">{{ error() }}</app-alert> }
        @if (!permission()) {
          <label class="field-label">Module
            <select class="field" name="moduleId" [ngModel]="moduleId()" (ngModelChange)="moduleId.set(+$event)">
              @for (m of activeModules(); track m.id) { <option [value]="m.id">{{ m.name }} ({{ m.code }})</option> }
            </select>
          </label>
          <label class="field-label">Action
            <input class="field" name="action" required placeholder="e.g. View, Add, Approve" [ngModel]="action()" (ngModelChange)="action.set($event)" />
          </label>
          <p class="hint">Code will be <strong>{{ selectedModule()?.code ?? 'Module' }}.{{ action() || 'Action' }}</strong></p>
        }
        <label class="field-label">Name <input class="field" name="name" required maxlength="150" [(ngModel)]="name" /></label>
        <label class="field-label">Description <input class="field" name="description" maxlength="500" [(ngModel)]="description" /></label>
        @if (permission(); as p) {
          <label class="checkbox-label">
            <input type="checkbox" name="active" [disabled]="p.isSystem" [(ngModel)]="active" /> Active
            @if (p.isSystem) { <span class="hint">(system permissions cannot be deactivated)</span> }
          </label>
        }
        <button type="submit" class="btn btn-primary btn-block" [disabled]="busy()">{{ busy() ? 'Please wait...' : permission() ? 'Save changes' : 'Create permission' }}</button>
      </form>
    </app-modal>
  `,
})
export class PermissionFormDialog implements OnInit {
  readonly permission = input<PermissionItem>();
  readonly modules = input.required<ModuleItemDto[]>();
  readonly closed = output<void>();
  readonly saved = output<void>();
  private readonly api = inject(PermissionsApi);

  readonly activeModules = computed(() => this.modules().filter((m) => m.isActive));
  readonly moduleId = signal(0);
  readonly action = signal('');
  readonly selectedModule = computed(() => this.modules().find((m) => m.id === this.moduleId()));
  name = '';
  description = '';
  active = true;
  readonly error = signal<string | null>(null);
  readonly busy = signal(false);

  ngOnInit(): void {
    const p = this.permission();
    this.moduleId.set(p?.moduleId ?? this.activeModules()[0]?.id ?? 0);
    if (p) {
      this.action.set(p.action);
      this.name = p.name;
      this.description = p.description ?? '';
      this.active = p.isActive;
    }
  }

  async submit(): Promise<void> {
    this.error.set(null);
    this.busy.set(true);
    try {
      const p = this.permission();
      if (p) await this.api.updatePermission(p.id, { name: this.name, description: this.description || undefined, isActive: this.active });
      else await this.api.createPermission({ moduleId: this.moduleId(), action: this.action(), name: this.name, description: this.description || undefined });
      this.saved.emit();
    } catch (err) {
      this.error.set(errorText(err));
    } finally {
      this.busy.set(false);
    }
  }
}
