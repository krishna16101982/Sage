import { Component, inject } from '@angular/core';
import { Router } from '@angular/router';
import { PageHeader } from '../../../component/page-header/page-header';
import { MessageService } from '../../../services/message/message.service';
import { GroupsForm } from '../groups-form/groups-form.component';

/** /groups/add — create a group (active, no permissions yet). */
@Component({
  selector: 'app-groups-add',
  imports: [PageHeader, GroupsForm],
  templateUrl: './groups-add.component.html',
})
export class GroupsAdd {
  private readonly router = inject(Router);
  private readonly messages = inject(MessageService);

  saved(name: string): void {
    this.messages.success(`Group ${name} created. Open its Permissions to choose what it can do.`);
    this.router.navigateByUrl('/groups/list');
  }
}
