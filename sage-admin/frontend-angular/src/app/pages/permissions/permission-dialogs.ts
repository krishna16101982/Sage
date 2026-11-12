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
  templateUrl: './module-form.dialog.html',
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
  templateUrl: './permission-form.dialog.html',
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
