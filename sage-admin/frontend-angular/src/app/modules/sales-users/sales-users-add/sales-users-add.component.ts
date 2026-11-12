import { Component, inject } from '@angular/core';
import { Router } from '@angular/router';
import { PageHeader } from '../../../component/page-header/page-header';
import { MessageService } from '../../../services/message/message.service';
import { SalesUsersForm } from '../sales-users-form/sales-users-form.component';

/** /sales-users/add — create a sales person (with a manager) or a sales manager, with a temporary password. */
@Component({
  selector: 'app-sales-users-add',
  imports: [PageHeader, SalesUsersForm],
  templateUrl: './sales-users-add.component.html',
})
export class SalesUsersAdd {
  private readonly router = inject(Router);
  private readonly messages = inject(MessageService);

  saved(name: string): void {
    this.messages.success(`Sales user ${name} created. They must change the temporary password at first sign-in.`);
    this.router.navigateByUrl('/sales-users/list');
  }
}
