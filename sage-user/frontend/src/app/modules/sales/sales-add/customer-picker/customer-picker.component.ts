import { Component, OnDestroy, inject, model, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Modal } from '../../../../component/modal/modal';
import { CustomersService } from '../../../../services/customers/customers.service';
import { errorText } from '../../../../services/rest/global-rest-service';
import type { Customer } from '../../../../shared/interfaces/customer.interface';
import { CustomersForm } from '../../../customers/customers-form/customers-form.component';

/** Customer field of the sale page: search existing customers, or add a new one in a dialog. Two-way: [(customer)]. */
@Component({
  selector: 'app-customer-picker',
  imports: [FormsModule, Modal, CustomersForm],
  templateUrl: './customer-picker.component.html',
})
export class CustomerPicker implements OnDestroy {
  readonly customer = model<Customer | null>(null);
  private readonly service = inject(CustomersService);

  search = '';
  readonly results = signal<Customer[] | null>(null);
  readonly searching = signal(false);
  readonly error = signal<string | null>(null);
  readonly adding = signal(false);
  private timer: ReturnType<typeof setTimeout> | undefined;

  changed(): void {
    clearTimeout(this.timer);
    const term = this.search.trim();
    if (term.length < 2) {
      this.results.set(null);
      return;
    }
    this.timer = setTimeout(() => this.find(term), 250);
  }

  private async find(term: string): Promise<void> {
    this.searching.set(true);
    this.error.set(null);
    try {
      this.results.set((await this.service.list({ search: term, pageSize: 8 })).items);
    } catch (err) {
      this.error.set(errorText(err, 'Unable to search customers.'));
    } finally {
      this.searching.set(false);
    }
  }

  choose(c: Customer): void {
    this.customer.set(c);
    this.search = '';
    this.results.set(null);
  }

  added(c: Customer): void {
    this.adding.set(false);
    this.choose(c);
  }

  ngOnDestroy(): void {
    clearTimeout(this.timer);
  }
}
