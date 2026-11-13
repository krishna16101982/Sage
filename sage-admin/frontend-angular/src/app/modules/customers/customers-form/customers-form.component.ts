import { Component, OnInit, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Alert } from '../../../component/alert/alert';
import { CustomersService } from '../../../services/customers/customers.service';
import { errorText } from '../../../services/rest/global-rest-service';
import type { Customer, CustomerInput } from '../../../shared/interfaces/customer.interface';

/** Customer form used by the Add page (no `customer`) and the Edit page. */
@Component({
  selector: 'app-customers-form',
  imports: [FormsModule, RouterLink, Alert],
  templateUrl: './customers-form.component.html',
})
export class CustomersForm implements OnInit {
  readonly customer = input<Customer>();
  /** Emits the saved customer. */
  readonly saved = output<Customer>();
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

  ngOnInit(): void {
    const c = this.customer();
    if (c) {
      this.name = c.name;
      this.contactPerson = c.contactPerson ?? '';
      this.email = c.email ?? '';
      this.phone = c.phone ?? '';
      this.gstin = c.gstin ?? '';
      this.address = c.address ?? '';
      this.city = c.city ?? '';
      this.state = c.state ?? '';
      this.country = c.country;
    }
  }

  async submit(): Promise<void> {
    this.error.set(null);
    this.busy.set(true);
    const data: CustomerInput = {
      name: this.name,
      contactPerson: this.contactPerson || undefined,
      email: this.email || undefined,
      phone: this.phone || undefined,
      gstin: this.gstin || undefined,
      address: this.address || undefined,
      city: this.city || undefined,
      state: this.state || undefined,
      country: this.country || undefined,
    };
    try {
      const c = this.customer();
      this.saved.emit(c ? await this.service.update(c.id, data) : await this.service.create(data));
    } catch (err) {
      this.error.set(errorText(err));
    } finally {
      this.busy.set(false);
    }
  }
}
