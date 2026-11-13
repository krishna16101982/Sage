import { Component, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Alert } from '../../../component/alert/alert';
import { CustomersService } from '../../../services/customers/customers.service';
import { errorText } from '../../../services/rest/global-rest-service';
import type { Customer } from '../../../shared/interfaces/customer.interface';

/**
 * New-customer form. Used by the Add customer page (Cancel is a link back to the list) and by the sale page's
 * "Add new customer" dialog (Cancel emits `cancelled`).
 */
@Component({
  selector: 'app-customers-form',
  imports: [FormsModule, RouterLink, Alert],
  templateUrl: './customers-form.component.html',
})
export class CustomersForm {
  /** Where Cancel goes; when not set, Cancel emits `cancelled` instead. */
  readonly cancelLink = input<string>();
  /** Emits the created customer. */
  readonly saved = output<Customer>();
  readonly cancelled = output<void>();
  private readonly service = inject(CustomersService);

  name = '';
  contactPerson = '';
  email = '';
  phone = '';
  gstin = '';
  address = '';
  city = '';
  state = '';
  country = 'India';
  readonly error = signal<string | null>(null);
  readonly busy = signal(false);

  async submit(): Promise<void> {
    this.error.set(null);
    this.busy.set(true);
    try {
      const customer = await this.service.create({
        name: this.name,
        contactPerson: this.contactPerson || undefined,
        email: this.email || undefined,
        phone: this.phone || undefined,
        gstin: this.gstin || undefined,
        address: this.address || undefined,
        city: this.city || undefined,
        state: this.state || undefined,
        country: this.country || undefined,
      });
      this.saved.emit(customer);
    } catch (err) {
      this.error.set(errorText(err));
    } finally {
      this.busy.set(false);
    }
  }
}
