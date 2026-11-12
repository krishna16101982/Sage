import { Component, OnInit, inject, input, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Alert } from '../../../component/alert/alert';
import { PageHeader } from '../../../component/page-header/page-header';
import { MessageService } from '../../../services/message/message.service';
import { PermissionsService } from '../../../services/permissions/permissions.service';
import { errorText } from '../../../services/rest/global-rest-service';
import type { ModuleItemDto } from '../../../shared/interfaces/permission.interface';
import { PermissionModulesForm } from '../permission-modules-form/permission-modules-form.component';

/** /permissions/modules/:id/edit — edit a module's name, category, order and active flag. */
@Component({
  selector: 'app-permission-modules-edit',
  imports: [Alert, PageHeader, PermissionModulesForm],
  templateUrl: './permission-modules-edit.component.html',
})
export class PermissionModulesEdit implements OnInit {
  /** From the route (:id). */
  readonly id = input.required<string>();
  private readonly service = inject(PermissionsService);
  private readonly router = inject(Router);
  private readonly messages = inject(MessageService);
  readonly module = signal<ModuleItemDto | null>(null);
  readonly error = signal<string | null>(null);

  async ngOnInit(): Promise<void> {
    try {
      const m = await this.service.findModule(Number(this.id()));
      if (m) this.module.set(m);
      else this.error.set('Module not found.');
    } catch (err) {
      this.error.set(errorText(err, 'Unable to load the module.'));
    }
  }

  saved(name: string): void {
    this.messages.success(`Module ${name} saved.`);
    this.router.navigateByUrl('/permissions/modules');
  }
}
