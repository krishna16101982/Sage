import { Component, OnInit, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Alert } from '../../../component/alert/alert';
import { CustomersService } from '../../../services/customers/customers.service';
import { errorText } from '../../../services/rest/global-rest-service';
import type { Customer } from '../../../shared/interfaces/customer.interface';

/**
 * Company form.
 *   Add (no `customer`): the Add company page (Cancel is a link back to the list) and the order form's / lead's
 *   "Add new company" dialog (Cancel emits `cancelled`).
 *   Edit (`customer` given): contact, GSTIN and address can change; the company name is read-only.
 *   View (`customer` and `readOnly`): every field read-only.
 */
@Component({
  selector: 'app-customers-form',
  imports: [FormsModule, RouterLink, Alert],
  templateUrl: './customers-form.component.html',
})
export class CustomersForm implements OnInit {
  /** Where Cancel goes; when not set, Cancel emits `cancelled` instead. */
  readonly cancelLink = input<string>();
  /** The company to edit or view; empty = add a new one. */
  readonly customer = input<Customer | null>(null);
  readonly readOnly = input(false);
  /** Emits the created or saved company. */
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

  ngOnInit(): void {
    const c = this.customer();
    if (!c) return;
    this.name = c.name;
    this.contactPerson = c.contactPerson ?? '';
    this.email = c.email ?? '';
    this.phone = c.phone ?? '';
    this.gstin = c.gstin ?? '';
    this.address = c.address ?? '';
    this.city = c.city ?? '';
    this.state = c.state ?? '';
    this.country = c.country || 'India';
  }

  async submit(): Promise<void> {
    if (this.readOnly()) return;
    this.error.set(null);
    this.busy.set(true);
    const body = {
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
      const existing = this.customer();
      this.saved.emit(existing ? await this.service.update(existing.id, body) : await this.service.create(body));
    } catch (err) {
      this.error.set(errorText(err));
    } finally {
      this.busy.set(false);
    }
  }
}
