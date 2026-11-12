import { Component, inject, input, output, signal } from '@angular/core';
import { Alert } from '../../../../component/alert/alert';
import { Modal } from '../../../../component/modal/modal';
import { GroupsService } from '../../../../services/groups/groups.service';
import { errorText } from '../../../../services/rest/global-rest-service';
import type { GroupItem } from '../../../../shared/interfaces/group.interface';

/** Confirmation before deleting a group. The API refuses (409) while users are assigned; the message is shown as is. */
@Component({
  selector: 'app-groups-delete-modal',
  imports: [Modal, Alert],
  templateUrl: './groups-delete-modal.component.html',
})
export class GroupsDeleteModal {
  readonly group = input.required<GroupItem>();
  readonly closed = output<void>();
  readonly deleted = output<void>();
  private readonly service = inject(GroupsService);
  readonly error = signal<string | null>(null);
  readonly busy = signal(false);

  async confirm(): Promise<void> {
    this.error.set(null);
    this.busy.set(true);
    try {
      await this.service.delete(this.group().id);
      this.deleted.emit();
    } catch (err) {
      this.error.set(errorText(err));
    } finally {
      this.busy.set(false);
    }
  }
}
