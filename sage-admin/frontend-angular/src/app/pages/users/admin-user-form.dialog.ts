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
  template: `
    <app-modal [title]="user() ? 'Edit admin user' : 'Add admin user'" (closed)="closed.emit()">
      <form class="form" (ngSubmit)="submit()">
        @if (error()) { <app-alert kind="error">{{ error() }}</app-alert> }
        <div class="form-grid-2">
          <label class="field-label">First name <input class="field" name="firstName" required maxlength="100" [(ngModel)]="firstName" /></label>
          <label class="field-label">Last name <input class="field" name="lastName" required maxlength="100" [(ngModel)]="lastName" /></label>
        </div>
        <label class="field-label">Username
          <input class="field" name="username" required maxlength="50" autocomplete="off" [disabled]="!!user()" [(ngModel)]="username" />
        </label>
        <label class="field-label">Email <input class="field" name="email" type="email" required maxlength="256" [(ngModel)]="email" /></label>
        <label class="field-label">Mobile <input class="field" name="mobile" maxlength="20" placeholder="+91 98765 43210" [(ngModel)]="mobileNumber" /></label>

        @if (user()?.isSuperAdmin) {
          <p class="text-sm text-muted">Super Admin: has every permission regardless of group.</p>
        } @else {
          <label class="field-label">Permission group
            <select class="field" name="groupId" [(ngModel)]="groupId">
              <option value="">{{ groups() === null ? 'Loading...' : 'No group (no access yet)' }}</option>
              @if (currentInactive()) {
                <option [value]="user()!.groupId">{{ user()!.groupName }} (inactive)</option>
              }
              @for (g of groups() ?? []; track g.id) {
                <option [value]="g.id">{{ g.name }}</option>
              }
            </select>
            <span class="hint mt-1 block">The group decides which screens and actions this user can use.</span>
          </label>
        }

        @if (!user()) {
          <label class="field-label">Temporary password
            <input class="field" name="password" type="password" required autocomplete="new-password" [(ngModel)]="password" />
          </label>
          <label class="field-label">Confirm password
            <input class="field" name="confirm" type="password" required autocomplete="new-password" [(ngModel)]="confirmPassword" />
          </label>
          <p class="hint">{{ hint }} The user must change it at first sign-in.</p>
        }
        <button type="submit" class="btn btn-primary btn-block" [disabled]="busy()">
          {{ busy() ? 'Please wait...' : user() ? 'Save changes' : 'Create admin user' }}
        </button>
      </form>
    </app-modal>
  `,
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
