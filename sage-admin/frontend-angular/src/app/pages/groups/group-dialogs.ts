import { Component, OnInit, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { GroupsApi } from '../../core/admin-api';
import { errorText } from '../../core/api-client';
import type { GroupItem, GroupStatus } from '../../core/models/group';
import { AlertComponent, ModalComponent } from '../../shared/ui';

/** Create (no `group`) or edit a group. The code is only editable on create. */
@Component({
  selector: 'app-group-form-dialog',
  imports: [FormsModule, ModalComponent, AlertComponent],
  template: `
    <app-modal [title]="group() ? 'Edit group' : 'Add group'" (closed)="closed.emit()">
      <form class="form" (ngSubmit)="submit()">
        @if (error()) { <app-alert kind="error">{{ error() }}</app-alert> }
        <label class="field-label">Code
          <input class="field" name="code" required maxlength="50" placeholder="e.g. HR_Manager" [disabled]="!!group()" [(ngModel)]="code" />
        </label>
        <label class="field-label">Name <input class="field" name="name" required maxlength="100" placeholder="e.g. HR Manager" [(ngModel)]="name" /></label>
        <label class="field-label">Description
          <textarea class="field" name="description" rows="3" maxlength="500" [(ngModel)]="description"></textarea>
        </label>
        @if (group(); as g) {
          <label class="checkbox-label">
            <input type="checkbox" name="active" [disabled]="g.isSystem" [(ngModel)]="active" /> Active
            @if (g.isSystem) {
              <span class="hint">(system groups cannot be deactivated)</span>
            } @else if (g.userCount > 0) {
              <span class="hint">(an inactive group grants nothing to its {{ g.userCount }} user(s))</span>
            }
          </label>
        }
        <button type="submit" class="btn btn-primary btn-block" [disabled]="busy()">
          {{ busy() ? 'Please wait...' : group() ? 'Save changes' : 'Create group' }}
        </button>
      </form>
    </app-modal>
  `,
})
export class GroupFormDialog implements OnInit {
  readonly group = input<GroupItem>();
  readonly closed = output<void>();
  readonly saved = output<void>();
  private readonly api = inject(GroupsApi);

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
      if (g) await this.api.update(g.id, { name: this.name, description: this.description || undefined, status });
      else await this.api.create({ code: this.code, name: this.name, description: this.description || undefined });
      this.saved.emit();
    } catch (err) {
      this.error.set(errorText(err));
    } finally {
      this.busy.set(false);
    }
  }
}

/** Confirms and deletes a group. The API refuses (409) while users are assigned; the message is shown as is. */
@Component({
  selector: 'app-delete-group-dialog',
  imports: [ModalComponent, AlertComponent],
  template: `
    <app-modal title="Delete group" (closed)="closed.emit()">
      <div class="stack text-sm">
        @if (error()) { <app-alert kind="error">{{ error() }}</app-alert> }
        @if (group().userCount > 0) {
          <p><strong>{{ group().name }}</strong> has {{ group().userCount }} user(s) assigned. Move them to another group before deleting it.</p>
        } @else {
          <p>Delete <strong>{{ group().name }}</strong> and its {{ group().permissionCount }} permission grant(s)? This cannot be undone.</p>
        }
        <div class="modal-footer">
          <button type="button" class="btn btn-secondary" (click)="closed.emit()">Cancel</button>
          <button type="button" class="btn btn-danger" [disabled]="busy() || group().userCount > 0" (click)="confirm()">
            {{ busy() ? 'Deleting...' : 'Delete' }}
          </button>
        </div>
      </div>
    </app-modal>
  `,
})
export class DeleteGroupDialog {
  readonly group = input.required<GroupItem>();
  readonly closed = output<void>();
  readonly deleted = output<void>();
  private readonly api = inject(GroupsApi);
  readonly error = signal<string | null>(null);
  readonly busy = signal(false);

  async confirm(): Promise<void> {
    this.error.set(null);
    this.busy.set(true);
    try {
      await this.api.delete(this.group().id);
      this.deleted.emit();
    } catch (err) {
      this.error.set(errorText(err));
    } finally {
      this.busy.set(false);
    }
  }
}
