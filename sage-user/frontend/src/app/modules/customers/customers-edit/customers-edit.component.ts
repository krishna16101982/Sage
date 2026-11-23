import { Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Alert } from '../../../component/alert/alert';
import { PageHeader } from '../../../component/page-header/page-header';
import { CustomersService } from '../../../services/customers/customers.service';
import { MessageService } from '../../../services/message/message.service';
import { errorText } from '../../../services/rest/global-rest-service';
import type { Customer } from '../../../shared/interfaces/customer.interface';
import { CustomersForm } from '../customers-form/customers-form.component';

/** /customers/:id (view) and /customers/:id/edit — one company; its name is read-only. The mode comes from the route data. */
@Component({
  selector: 'app-customers-edit',
  imports: [Alert, PageHeader, CustomersForm],
  template: `
    <div class="stack max-w-3xl">
      <app-page-header [title]="title()" backLink="/customers/list" />
      @if (error()) { <app-alert kind="error">{{ error() }}</app-alert> }
      @if (!customer() && !error()) { <p class="text-sm text-muted">Loading...</p> }
      @if (customer(); as c) {
        <section class="card"><div class="card-body">
          <app-customers-form [customer]="c" [readOnly]="mode() === 'view'" cancelLink="/customers/list" (saved)="saved($event)" />
        </div></section>
      }
    </div>
  `,
})
export class CustomersEdit implements OnInit {
  /** From the route (:id) and its data. */
  readonly id = input.required<string>();
  readonly mode = input<'view' | 'edit'>('view');
  private readonly service = inject(CustomersService);
  private readonly router = inject(Router);
  private readonly messages = inject(MessageService);
  readonly customer = signal<Customer | null>(null);
  readonly error = signal<string | null>(null);
  readonly title = computed(() => `${this.mode() === 'edit' ? 'Edit company' : 'Company'}${this.customer() ? ' · ' + this.customer()!.name : ''}`);

  async ngOnInit(): Promise<void> {
    try {
      this.customer.set(await this.service.get(this.id()));
    } catch (err) {
      this.error.set(errorText(err, 'Company not found.'));
    }
  }

  saved(customer: Customer): void {
    this.messages.success(`Company ${customer.name} saved.`);
    this.router.navigateByUrl('/customers/list');
  }
}
