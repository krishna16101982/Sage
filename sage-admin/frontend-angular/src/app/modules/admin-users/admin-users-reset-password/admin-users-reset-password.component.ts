import { Component, OnInit, inject, input, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Alert } from '../../../component/alert/alert';
import { PageHeader } from '../../../component/page-header/page-header';
import { ResetPasswordForm } from '../../../component/reset-password-form/reset-password-form';
import { AdminUsersService } from '../../../services/admin-users/admin-users.service';
import { MessageService } from '../../../services/message/message.service';
import { errorText } from '../../../services/rest/global-rest-service';
import type { AdminUserItem } from '../../../shared/interfaces/admin-user.interface';

/** /users/:id/reset-password — set a temporary password for another admin user. */
@Component({
  selector: 'app-admin-users-reset-password',
  imports: [Alert, PageHeader, ResetPasswordForm],
  templateUrl: './admin-users-reset-password.component.html',
})
export class AdminUsersResetPassword implements OnInit {
  /** From the route (:id). */
  readonly id = input.required<string>();
  private readonly service = inject(AdminUsersService);
  private readonly router = inject(Router);
  private readonly messages = inject(MessageService);
  readonly user = signal<AdminUserItem | null>(null);
  readonly error = signal<string | null>(null);

  /** Passed to the shared form. */
  protected readonly reset = (password: string, confirm: string) => this.service.resetPassword(this.id(), password, confirm);

  async ngOnInit(): Promise<void> {
    try {
      this.user.set(await this.service.get(this.id()));
    } catch (err) {
      this.error.set(errorText(err, 'Unable to load this admin user.'));
    }
  }

  done(): void {
    const u = this.user();
    this.messages.success(`Temporary password set for ${u?.firstName} ${u?.lastName}. They must change it at next sign-in.`);
    this.router.navigateByUrl('/users/list');
  }
}
