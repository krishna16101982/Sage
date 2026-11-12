import { Component, OnInit, inject, input, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Alert } from '../../../component/alert/alert';
import { PageHeader } from '../../../component/page-header/page-header';
import { GroupsService } from '../../../services/groups/groups.service';
import { MessageService } from '../../../services/message/message.service';
import { errorText } from '../../../services/rest/global-rest-service';
import type { GroupItem } from '../../../shared/interfaces/group.interface';
import { GroupsForm } from '../groups-form/groups-form.component';

/** /groups/:id/edit — edit a group's name, description and status. The code never changes. */
@Component({
  selector: 'app-groups-edit',
  imports: [Alert, PageHeader, GroupsForm],
  templateUrl: './groups-edit.component.html',
})
export class GroupsEdit implements OnInit {
  /** From the route (:id). */
  readonly id = input.required<string>();
  private readonly service = inject(GroupsService);
  private readonly router = inject(Router);
  private readonly messages = inject(MessageService);
  readonly group = signal<GroupItem | null>(null);
  readonly error = signal<string | null>(null);

  async ngOnInit(): Promise<void> {
    try {
      this.group.set(await this.service.get(this.id()));
    } catch (err) {
      this.error.set(errorText(err, 'Unable to load this group.'));
    }
  }

  saved(name: string): void {
    this.messages.success(`Group ${name} saved.`);
    this.router.navigateByUrl('/groups/list');
  }
}
