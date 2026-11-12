import { Component, OnInit, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Alert } from '../../../component/alert/alert';
import { GroupsService } from '../../../services/groups/groups.service';
import { errorText } from '../../../services/rest/global-rest-service';
import type { GroupItem, GroupStatus } from '../../../shared/interfaces/group.interface';

/** Group form used by the Add page (no `group`) and the Edit page. The code is only editable on create. */
@Component({
  selector: 'app-groups-form',
  imports: [FormsModule, RouterLink, Alert],
  templateUrl: './groups-form.component.html',
})
export class GroupsForm implements OnInit {
  readonly group = input<GroupItem>();
  /** Emits the group's name after saving. */
  readonly saved = output<string>();
  private readonly service = inject(GroupsService);

  code = '';
  name = '';
  description = '';
  active = true;
  readonly error = signal<string | null>(null);
  readonly busy = signal(false);

  ngOnInit(): void {
    const g = this.group();
    if (g) {
      this.code = g.code;
      this.name = g.name;
      this.description = g.description ?? '';
      this.active = g.status === 'Active';
    }
  }

  async submit(): Promise<void> {
    this.error.set(null);
    this.busy.set(true);
    try {
      const g = this.group();
      const status: GroupStatus = this.active ? 'Active' : 'Inactive';
      if (g) await this.service.update(g.id, { name: this.name, description: this.description || undefined, status });
      else await this.service.create({ code: this.code, name: this.name, description: this.description || undefined });
      this.saved.emit(this.name);
    } catch (err) {
      this.error.set(errorText(err));
    } finally {
      this.busy.set(false);
    }
  }
}
