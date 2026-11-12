import { Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { Alert } from '../../../component/alert/alert';
import { PageHeader } from '../../../component/page-header/page-header';
import { AdminUsersService } from '../../../services/admin-users/admin-users.service';
import { MessageService } from '../../../services/message/message.service';
import { errorText } from '../../../services/rest/global-rest-service';
import type { AdminUserItem, GroupOption } from '../../../shared/interfaces/admin-user.interface';

/** /users/:id/edit — edit an admin user's details and group. The username never changes. */
@Component({
  selector: 'app-admin-users-edit',
  imports: [FormsModule, RouterLink, Alert, PageHeader],
  templateUrl: './admin-users-edit.component.html',
})
export class AdminUsersEdit implements OnInit {
  /** From the route (:id). */
  readonly id = input.required<string>();
  private readonly service = inject(AdminUsersService);
  private readonly router = inject(Router);
  private readonly messages = inject(MessageService);

  readonly user = signal<AdminUserItem | null>(null);
  firstName = '';
  lastName = '';
  email = '';
  mobileNumber = '';
  groupId = '';
  readonly groups = signal<GroupOption[] | null>(null);
  readonly error = signal<string | null>(null);
  readonly busy = signal(false);

  /** Keep the user's current group selectable even if it has since been deactivated, so it is not silently dropped. */
  readonly currentInactive = computed(() => {
    const u = this.user();
    return !!u?.groupId && !u.groupIsActive && !(this.groups() ?? []).some((g) => g.id === u.groupId);
  });

  async ngOnInit(): Promise<void> {
    try {
      const [u, groups] = await Promise.all([this.service.get(this.id()), this.service.groupOptions()]);
      this.firstName = u.firstName;
      this.lastName = u.lastName;
      this.email = u.email;
      this.mobileNumber = u.mobileNumber ?? '';
      this.groupId = u.groupId ?? '';
      this.groups.set(groups);
      this.user.set(u);
    } catch (err) {
      this.error.set(errorText(err, 'Unable to load this admin user.'));
    }
  }

  async submit(): Promise<void> {
    this.error.set(null);
    this.busy.set(true);
    try {
      await this.service.update(this.id(), {
        firstName: this.firstName,
        lastName: this.lastName,
        email: this.email,
        mobileNumber: this.mobileNumber || undefined,
        groupId: this.groupId || undefined,
      });
      this.messages.success(`${this.firstName} ${this.lastName} saved.`);
      this.router.navigateByUrl('/users/list');
    } catch (err) {
      this.error.set(errorText(err));
    } finally {
      this.busy.set(false);
    }
  }
}
