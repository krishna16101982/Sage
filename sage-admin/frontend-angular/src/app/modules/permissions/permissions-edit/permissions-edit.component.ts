import { Component, OnInit, inject, input, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Alert } from '../../../component/alert/alert';
import { PageHeader } from '../../../component/page-header/page-header';
import { MessageService } from '../../../services/message/message.service';
import { PermissionsService } from '../../../services/permissions/permissions.service';
import { errorText } from '../../../services/rest/global-rest-service';
import type { ModuleItemDto, PermissionItem } from '../../../shared/interfaces/permission.interface';
import { PermissionsForm } from '../permissions-form/permissions-form.component';

/** /permissions/:id/edit — edit a permission's name, description and active flag. */
@Component({
  selector: 'app-permissions-edit',
  imports: [Alert, PageHeader, PermissionsForm],
  templateUrl: './permissions-edit.component.html',
})
export class PermissionsEdit implements OnInit {
  /** From the route (:id). */
  readonly id = input.required<string>();
  private readonly service = inject(PermissionsService);
  private readonly router = inject(Router);
  private readonly messages = inject(MessageService);
  readonly modules = signal<ModuleItemDto[]>([]);
  readonly permission = signal<PermissionItem | null>(null);
  readonly error = signal<string | null>(null);

  async ngOnInit(): Promise<void> {
    try {
      const modules = await this.service.modules(true);
      const p = modules.flatMap((m) => m.permissions).find((x) => x.id === Number(this.id()));
      if (!p) throw new Error();
      this.modules.set(modules);
      this.permission.set(p);
    } catch (err) {
      this.error.set(errorText(err, 'Permission not found.'));
    }
  }

  saved(code: string): void {
    this.messages.success(`Permission ${code} saved.`);
    this.router.navigateByUrl('/permissions/list');
  }
}
