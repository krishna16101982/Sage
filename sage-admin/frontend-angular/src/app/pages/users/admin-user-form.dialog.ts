import { Component, OnInit, computed, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AdminUsersApi } from '../../core/admin-api';
import { errorText } from '../../core/api-client';
import type { AdminUserItem, GroupOption } from '../../core/models/adminUser';
import { AlertComponent, ModalComponent, PASSWORD_HINT } from '../../shared/ui';

/** Create (no `user`) or edit an admin user. The group decides their permissions; no group means no access yet. */
@Component({
  selector: 'app-admin-user-form-dialog',
  imports: [FormsModule, ModalComponent, AlertComponent],
  templateUrl: './admin-user-form.dialog.html',
})
export class AdminUserFormDialog implements OnInit {
  readonly user = input<AdminUserItem>();
  readonly closed = output<void>();
  readonly saved = output<void>();
  private readonly api = inject(AdminUsersApi);
  protected readonly hint = PASSWORD_HINT;

  firstName = '';
  lastName = '';
  username = '';
  email = '';
  mobileNumber = '';
  groupId = '';
  password = '';
  confirmPassword = '';
  readonly groups = signal<GroupOption[] | null>(null);
  readonly error = signal<string | null>(null);
  readonly busy = signal(false);

  /** Keep the user's current group selectable even if it has since been deactivated, so it is not silently dropped. */
  readonly currentInactive = computed(() => {
    const u = this.user();
    return !!u?.groupId && !u.groupIsActive && !(this.groups() ?? []).some((g) => g.id === u.groupId);
  });

  ngOnInit(): void {
    const u = this.user();
    if (u) {
      this.firstName = u.firstName;
      this.lastName = u.lastName;
      this.username = u.username;
      this.email = u.email;
      this.mobileNumber = u.mobileNumber ?? '';
      this.groupId = u.groupId ?? '';
    }
    this.api.groupOptions().then(
      (g) => this.groups.set(g),
      (err) => this.error.set(errorText(err)),
    );
  }

  async submit(): Promise<void> {
    this.error.set(null);
    this.busy.set(true);
    try {
      const data = { firstName: this.firstName, lastName: this.lastName, email: this.email, mobileNumber: this.mobileNumber || undefined, groupId: this.groupId || undefined };
      const u = this.user();
      if (u) await this.api.update(u.id, data);
      else await this.api.create({ ...data, username: this.username, password: this.password, confirmPassword: this.confirmPassword });
      this.saved.emit();
    } catch (err) {
      this.error.set(errorText(err));
    } finally {
      this.busy.set(false);
    }
  }
}
