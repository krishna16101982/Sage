import { Component, OnInit, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Alert } from '../../../component/alert/alert';
import { PageHeader } from '../../../component/page-header/page-header';
import { MessageService } from '../../../services/message/message.service';
import { PermissionsService } from '../../../services/permissions/permissions.service';
import { errorText } from '../../../services/rest/global-rest-service';
import type { ModuleItemDto } from '../../../shared/interfaces/permission.interface';
import { PermissionsForm } from '../permissions-form/permissions-form.component';

/** /permissions/add — create a permission (code = Module.Action) in an active module. */
@Component({
  selector: 'app-permissions-add',
  imports: [Alert, PageHeader, PermissionsForm],
  templateUrl: './permissions-add.component.html',
})
export class PermissionsAdd implements OnInit {
  private readonly service = inject(PermissionsService);
  private readonly router = inject(Router);
  private readonly messages = inject(MessageService);
  readonly modules = signal<ModuleItemDto[] | null>(null);
  readonly error = signal<string | null>(null);

  async ngOnInit(): Promise<void> {
    try {
      this.modules.set(await this.service.modules(true));
    } catch (err) {
      this.error.set(errorText(err, 'Unable to load modules.'));
    }
  }

  saved(code: string): void {
    this.messages.success(`Permission ${code} created.`);
    this.router.navigateByUrl('/permissions/list');
  }
}
