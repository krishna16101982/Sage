import { Component, OnInit, computed, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Alert } from '../../../component/alert/alert';
import { PermissionsService } from '../../../services/permissions/permissions.service';
import { errorText } from '../../../services/rest/global-rest-service';
import type { ModuleItemDto, PermissionItem } from '../../../shared/interfaces/permission.interface';

/** Permission form used by the Add page (no `permission`) and the Edit page. Module and action are fixed after creation. */
@Component({
  selector: 'app-permissions-form',
  imports: [FormsModule, RouterLink, Alert],
  templateUrl: './permissions-form.component.html',
})
export class PermissionsForm implements OnInit {
  readonly permission = input<PermissionItem>();
  readonly modules = input.required<ModuleItemDto[]>();
  /** Emits the permission code after saving. */
  readonly saved = output<string>();
  private readonly service = inject(PermissionsService);

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
      if (p) await this.service.updatePermission(p.id, { name: this.name, description: this.description || undefined, isActive: this.active });
      else await this.service.createPermission({ moduleId: this.moduleId(), action: this.action(), name: this.name, description: this.description || undefined });
      this.saved.emit(p?.code ?? `${this.selectedModule()?.code}.${this.action()}`);
    } catch (err) {
      this.error.set(errorText(err));
    } finally {
      this.busy.set(false);
    }
  }
}
