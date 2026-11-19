import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { Alert } from '../../../component/alert/alert';
import { PageHeader } from '../../../component/page-header/page-header';
import { MessageService } from '../../../services/message/message.service';
import { errorText } from '../../../services/rest/global-rest-service';
import { SalesService } from '../../../services/sales/sales.service';

/**
 * /team/add (sales managers) — add a sales person who reports to you. You set a temporary password; they must change
 * it the first time they sign in. Admins see the new account (and who added it) in the Admin Panel.
 */
@Component({
  selector: 'app-team-member-add',
  imports: [FormsModule, RouterLink, Alert, PageHeader],
  templateUrl: './team-member-add.component.html',
})
export class TeamMemberAdd {
  private readonly service = inject(SalesService);
  private readonly router = inject(Router);
  private readonly messages = inject(MessageService);
  firstName = '';
  lastName = '';
  username = '';
  email = '';
  mobileNumber = '';
  password = '';
  confirmPassword = '';
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);

  async submit(): Promise<void> {
    this.error.set(null);
    if (this.password !== this.confirmPassword) {
      this.error.set('The passwords do not match.');
      return;
    }
    this.busy.set(true);
    try {
      const member = await this.service.addTeamMember({
        firstName: this.firstName.trim(),
        lastName: this.lastName.trim(),
        username: this.username.trim(),
        email: this.email.trim(),
        mobileNumber: this.mobileNumber.trim() || undefined,
        password: this.password,
        confirmPassword: this.confirmPassword,
      });
      this.messages.success(`${member.name} added to your team. Share the username and temporary password with them.`);
      this.router.navigateByUrl('/team');
    } catch (err) {
      this.error.set(errorText(err));
    } finally {
      this.busy.set(false);
    }
  }
}
