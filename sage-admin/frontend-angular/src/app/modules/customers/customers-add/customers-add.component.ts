import { Component, inject } from '@angular/core';
import { Router } from '@angular/router';
import { PageHeader } from '../../../component/page-header/page-header';
import { MessageService } from '../../../services/message/message.service';
import type { Customer } from '../../../shared/interfaces/customer.interface';
import { CustomersForm } from '../customers-form/customers-form.component';

/** /customers/add — add a customer. */
@Component({
  selector: 'app-customers-add',
  imports: [PageHeader, CustomersForm],
  templateUrl: './customers-add.component.html',
})
export class CustomersAdd {
  private readonly router = inject(Router);
  private readonly messages = inject(MessageService);

  saved(customer: Customer): void {
    this.messages.success(`Customer ${customer.name} added.`);
    this.router.navigateByUrl('/customers/list');
  }
}
