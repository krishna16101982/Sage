import { Component, OnInit, inject, input, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Alert } from '../../../component/alert/alert';
import { PageHeader } from '../../../component/page-header/page-header';
import { CustomersService } from '../../../services/customers/customers.service';
import { MessageService } from '../../../services/message/message.service';
import { errorText } from '../../../services/rest/global-rest-service';
import type { Customer } from '../../../shared/interfaces/customer.interface';
import { CustomersForm } from '../customers-form/customers-form.component';

/** /customers/:id/edit — edit a customer's details (past sales keep pointing at the customer). */
@Component({
  selector: 'app-customers-edit',
  imports: [Alert, PageHeader, CustomersForm],
  templateUrl: './customers-edit.component.html',
})
export class CustomersEdit implements OnInit {
  /** From the route (:id). */
  readonly id = input.required<string>();
  private readonly service = inject(CustomersService);
  private readonly router = inject(Router);
  private readonly messages = inject(MessageService);
  readonly customer = signal<Customer | null>(null);
  readonly error = signal<string | null>(null);

  async ngOnInit(): Promise<void> {
    try {
      this.customer.set(await this.service.get(this.id()));
    } catch (err) {
      this.error.set(errorText(err, 'Customer not found.'));
    }
  }

  saved(customer: Customer): void {
    this.messages.success(`Customer ${customer.name} saved.`);
    this.router.navigateByUrl('/customers/list');
  }
}
