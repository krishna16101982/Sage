import { Component, OnInit, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Alert } from '../../../component/alert/alert';
import { PermissionsService } from '../../../services/permissions/permissions.service';
import { errorText } from '../../../services/rest/global-rest-service';
import type { ModuleItemDto } from '../../../shared/interfaces/permission.interface';

/** Module form used by the Add page (no `module`) and the Edit page. The code is fixed after creation: permission codes derive from it. */
@Component({
  selector: 'app-permission-modules-form',
  imports: [FormsModule, RouterLink, Alert],
  templateUrl: './permission-modules-form.component.html',
})
export class PermissionModulesForm implements OnInit {
  readonly module = input<ModuleItemDto>();
  /** Emits the module name after saving. */
  readonly saved = output<string>();
  private readonly service = inject(PermissionsService);

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
      if (m) await this.service.updateModule(m.id, { name: this.name, category: this.category || undefined, sortOrder: order, isActive: this.active });
      else await this.service.createModule({ code: this.code, name: this.name, category: this.category || undefined, sortOrder: order });
      this.saved.emit(this.name);
    } catch (err) {
      this.error.set(errorText(err));
    } finally {
      this.busy.set(false);
    }
  }
}
