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
  template: `
    <app-modal [title]="user() ? 'Edit sales user' : 'Add sales user'" (closed)="closed.emit()">
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

        <fieldset class="stack-sm text-sm">
          <legend>Role</legend>
          <div class="row">
            @for (r of roles; track r) {
              <label class="checkbox-label">
                <input type="radio" name="role" [value]="r" [ngModel]="role()" (ngModelChange)="role.set($event)" /> {{ roleLabel[r] }}
              </label>
            }
          </div>
          <p class="hint">A sales manager can also sell, and sees their team's sales.</p>
        </fieldset>
        @if (demotingManagerWithTeam()) {
          <app-alert kind="error">This manager has {{ user()!.teamSize }} sales person(s). Move them to another manager before changing the role.</app-alert>
        }

        @if (role() === 'SalesPerson') {
          <label class="field-label">Sales manager
            <select class="field" name="managerId" required [(ngModel)]="managerId">
              <option value="">{{ managers() === null ? 'Loading...' : managerOptions().length === 0 ? 'No active sales managers yet' : 'Select a manager' }}</option>
              @if (currentMissing()) {
                <option [value]="user()!.managerId">{{ user()!.managerName }} (inactive)</option>
              }
              @for (m of managerOptions(); track m.id) {
                <option [value]="m.id">{{ m.name }} ({{ m.username }}) · team {{ m.teamSize }}</option>
              }
            </select>
          </label>
        }

        @if (!user()) {
          <label class="field-label">Temporary password
            <input class="field" name="password" type="password" required autocomplete="new-password" [(ngModel)]="password" />
          </label>
          <label class="field-label">Confirm password
            <input class="field" name="confirm" type="password" required autocomplete="new-password" [(ngModel)]="confirmPassword" />
          </label>
          <p class="hint">{{ hint }} The user must change it at first sign-in to the User Panel.</p>
        }
        <button type="submit" class="btn btn-primary btn-block" [disabled]="busy()">
          {{ busy() ? 'Please wait...' : user() ? 'Save changes' : 'Create sales user' }}
        </button>
      </form>
    </app-modal>
  `,
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
