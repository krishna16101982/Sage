import { Component, OnInit, inject, input, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Alert } from '../../../component/alert/alert';
import { PageHeader } from '../../../component/page-header/page-header';
import { ResetPasswordForm } from '../../../component/reset-password-form/reset-password-form';
import { MessageService } from '../../../services/message/message.service';
import { errorText } from '../../../services/rest/global-rest-service';
import { SalesUsersService } from '../../../services/sales-users/sales-users.service';
import type { SalesUser } from '../../../shared/interfaces/sales-user.interface';

/** /sales-users/:id/reset-password — set a temporary password; their User Panel sessions end. */
@Component({
  selector: 'app-sales-users-reset-password',
  imports: [Alert, PageHeader, ResetPasswordForm],
  templateUrl: './sales-users-reset-password.component.html',
})
export class SalesUsersResetPassword implements OnInit {
  /** From the route (:id). */
  readonly id = input.required<string>();
  private readonly service = inject(SalesUsersService);
  private readonly router = inject(Router);
  private readonly messages = inject(MessageService);
  readonly user = signal<SalesUser | null>(null);
  readonly error = signal<string | null>(null);

  /** Passed to the shared form. */
  protected readonly reset = (password: string, confirm: string) => this.service.resetPassword(this.id(), password, confirm);

  async ngOnInit(): Promise<void> {
    try {
      this.user.set(await this.service.get(this.id()));
    } catch (err) {
      this.error.set(errorText(err, 'Unable to load this sales user.'));
    }
  }

  done(): void {
    const u = this.user();
    this.messages.success(`Temporary password set for ${u?.firstName} ${u?.lastName}. They must change it at next sign-in.`);
    this.router.navigateByUrl('/sales-users/list');
  }
}
