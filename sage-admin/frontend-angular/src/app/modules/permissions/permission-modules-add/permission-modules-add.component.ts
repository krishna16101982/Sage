import { Component, inject } from '@angular/core';
import { Router } from '@angular/router';
import { PageHeader } from '../../../component/page-header/page-header';
import { MessageService } from '../../../services/message/message.service';
import { PermissionModulesForm } from '../permission-modules-form/permission-modules-form.component';

/** /permissions/modules/add — create a permission module. */
@Component({
  selector: 'app-permission-modules-add',
  imports: [PageHeader, PermissionModulesForm],
  templateUrl: './permission-modules-add.component.html',
})
export class PermissionModulesAdd {
  private readonly router = inject(Router);
  private readonly messages = inject(MessageService);

  saved(name: string): void {
    this.messages.success(`Module ${name} created.`);
    this.router.navigateByUrl('/permissions/modules');
  }
}
