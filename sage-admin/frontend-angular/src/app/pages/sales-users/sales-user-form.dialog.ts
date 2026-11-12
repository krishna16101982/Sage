import { Component, OnInit, computed, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { SalesUsersApi } from '../../core/admin-api';
import { errorText } from '../../core/api-client';
import { SALES_ROLE_LABEL, type SalesManagerOption, type SalesRole, type SalesUser } from '../../core/models/salesUser';
import { AlertComponent, ModalComponent, PASSWORD_HINT } from '../../shared/ui';

/** Create (no `user`) or edit a sales user. A sales person needs an active sales manager; a manager has none. */
@Component({
  selector: 'app-sales-user-form-dialog',
  imports: [FormsModule, ModalComponent, AlertComponent],
  templateUrl: './sales-user-form.dialog.html',
})
export class SalesUserFormDialog implements OnInit {
  readonly user = input<SalesUser>();
  readonly closed = output<void>();
  readonly saved = output<void>();
  private readonly api = inject(SalesUsersApi);
  protected readonly hint = PASSWORD_HINT;
  protected readonly roles: SalesRole[] = ['SalesPerson', 'SalesManager'];
  protected readonly roleLabel = SALES_ROLE_LABEL;

  firstName = '';
  lastName = '';
  username = '';
  email = '';
  mobileNumber = '';
  managerId = '';
  password = '';
  confirmPassword = '';
  readonly role = signal<SalesRole>('SalesPerson');
  readonly managers = signal<SalesManagerOption[] | null>(null);
  readonly error = signal<string | null>(null);
  readonly busy = signal(false);

  /** A user cannot report to themselves. */
  readonly managerOptions = computed(() => (this.managers() ?? []).filter((m) => m.id !== this.user()?.id));
  /** Keep the current (possibly now inactive) manager visible so it is not silently changed. */
  readonly currentMissing = computed(() => {
    const u = this.user();
    return !!u?.managerId && !this.managerOptions().some((m) => m.id === u.managerId);
  });
  readonly demotingManagerWithTeam = computed(() => {
    const u = this.user();
    return !!u && u.role === 'SalesManager' && this.role() === 'SalesPerson' && u.teamSize > 0;
  });

  ngOnInit(): void {
    const u = this.user();
    if (u) {
      this.firstName = u.firstName;
      this.lastName = u.lastName;
      this.username = u.username;
      this.email = u.email;
      this.mobileNumber = u.mobileNumber ?? '';
      this.managerId = u.managerId ?? '';
      this.role.set(u.role);
    }
    this.api.managers().then(
      (m) => this.managers.set(m),
      (err) => this.error.set(errorText(err)),
    );
  }

  async submit(): Promise<void> {
    this.error.set(null);
    this.busy.set(true);
    try {
      const role = this.role();
      const data = {
        firstName: this.firstName,
        lastName: this.lastName,
        email: this.email,
        mobileNumber: this.mobileNumber || undefined,
        role,
        managerId: role === 'SalesPerson' && this.managerId ? this.managerId : undefined,
      };
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
