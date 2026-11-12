import { Component, OnInit, inject, input, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Alert } from '../../../component/alert/alert';
import { PageHeader } from '../../../component/page-header/page-header';
import { MessageService } from '../../../services/message/message.service';
import { errorText } from '../../../services/rest/global-rest-service';
import { SalesUsersService } from '../../../services/sales-users/sales-users.service';
import type { SalesUser } from '../../../shared/interfaces/sales-user.interface';
import { SalesUsersForm } from '../sales-users-form/sales-users-form.component';

/** /sales-users/:id/edit — edit details, role and manager. The username never changes. */
@Component({
  selector: 'app-sales-users-edit',
  imports: [Alert, PageHeader, SalesUsersForm],
  templateUrl: './sales-users-edit.component.html',
})
export class SalesUsersEdit implements OnInit {
  /** From the route (:id). */
  readonly id = input.required<string>();
  private readonly service = inject(SalesUsersService);
  private readonly router = inject(Router);
  private readonly messages = inject(MessageService);
  readonly user = signal<SalesUser | null>(null);
  readonly error = signal<string | null>(null);

  async ngOnInit(): Promise<void> {
    try {
      this.user.set(await this.service.get(this.id()));
    } catch (err) {
      this.error.set(errorText(err, 'Unable to load this sales user.'));
    }
  }

  saved(name: string): void {
    this.messages.success(`${name} saved.`);
    this.router.navigateByUrl('/sales-users/list');
  }
}
