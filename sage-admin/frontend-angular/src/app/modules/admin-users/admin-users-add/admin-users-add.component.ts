import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { Alert } from '../../../component/alert/alert';
import { PageHeader } from '../../../component/page-header/page-header';
import { AdminUsersService } from '../../../services/admin-users/admin-users.service';
import { MessageService } from '../../../services/message/message.service';
import { errorText } from '../../../services/rest/global-rest-service';
import type { GroupOption } from '../../../shared/interfaces/admin-user.interface';
import { PASSWORD_HINT } from '../../../shared/utils/password';

/** /users/add — create an admin user with a group and a temporary password. */
@Component({
  selector: 'app-admin-users-add',
  imports: [FormsModule, RouterLink, Alert, PageHeader],
  templateUrl: './admin-users-add.component.html',
})
export class AdminUsersAdd implements OnInit {
  private readonly service = inject(AdminUsersService);
  private readonly router = inject(Router);
  private readonly messages = inject(MessageService);
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

  ngOnInit(): void {
    this.service.groupOptions().then(
      (g) => this.groups.set(g),
      (err) => this.error.set(errorText(err)),
    );
  }

  async submit(): Promise<void> {
    this.error.set(null);
    this.busy.set(true);
    try {
      await this.service.create({
        firstName: this.firstName,
        lastName: this.lastName,
        email: this.email,
        mobileNumber: this.mobileNumber || undefined,
        groupId: this.groupId || undefined,
        username: this.username,
        password: this.password,
        confirmPassword: this.confirmPassword,
      });
      this.messages.success(`Admin user ${this.firstName} ${this.lastName} created. They must change the temporary password at first sign-in.`);
      this.router.navigateByUrl('/users/list');
    } catch (err) {
      this.error.set(errorText(err));
    } finally {
      this.busy.set(false);
    }
  }
}
