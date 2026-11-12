import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Alert } from '../../../component/alert/alert';
import { CanDirective } from '../../../directives/can.directive';
import { MessageService } from '../../../services/message/message.service';
import { PermissionsService } from '../../../services/permissions/permissions.service';
import { errorText } from '../../../services/rest/global-rest-service';
import type { ModuleItemDto } from '../../../shared/interfaces/permission.interface';
import { Perm } from '../../../shared/enumarations/permission-codes';
import { PermissionsTabs } from '../permissions-tabs/permissions-tabs.component';

/** /permissions/modules — the modules permissions are grouped under. */
@Component({
  selector: 'app-permission-modules-list',
  imports: [RouterLink, CanDirective, Alert, PermissionsTabs],
  templateUrl: './permission-modules-list.component.html',
})
export class PermissionModulesList implements OnInit {
  private readonly service = inject(PermissionsService);
  protected readonly perm = Perm;
  readonly modules = signal<ModuleItemDto[] | null>(null);
  readonly error = signal<string | null>(null);
  readonly notice = signal<string | null>(inject(MessageService).take());

  async ngOnInit(): Promise<void> {
    try {
      this.modules.set(await this.service.modules(true));
    } catch (err) {
      this.error.set(errorText(err, 'Unable to load modules.'));
    }
  }
}
