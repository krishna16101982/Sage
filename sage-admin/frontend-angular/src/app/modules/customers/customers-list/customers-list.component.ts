import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Alert } from '../../../component/alert/alert';
import { Pagination } from '../../../component/pagination/pagination';
import { CanDirective } from '../../../directives/can.directive';
import { CustomersService } from '../../../services/customers/customers.service';
import { MessageService } from '../../../services/message/message.service';
import { PermissionService } from '../../../services/permission/permission.service';
import { errorText } from '../../../services/rest/global-rest-service';
import type { PagedResult } from '../../../shared/interfaces/api.interface';
import type { Customer } from '../../../shared/interfaces/customer.interface';
import { Perm } from '../../../shared/enumarations/permission-codes';
import { CustomersFilter } from './customers-filter/customers-filter.component';

/** /customers/list — every customer (added here or by sales users in the User Panel). */
@Component({
  selector: 'app-customers-list',
  imports: [RouterLink, CanDirective, Alert, Pagination, CustomersFilter],
  templateUrl: './customers-list.component.html',
})
export class CustomersList implements OnInit {
  private readonly service = inject(CustomersService);
  protected readonly perm = Perm;
  /** The sale count links to the Sales list (filtered by this customer) when the user can view sales. */
  protected readonly canSeeSales = inject(PermissionService).has(Perm.SalesView);
  search = '';
  page = 1;
  readonly data = signal<PagedResult<Customer> | null>(null);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly notice = signal<string | null>(inject(MessageService).take());

  ngOnInit(): void {
    this.load();
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      this.data.set(await this.service.list({ page: this.page, pageSize: 20, search: this.search.trim() || undefined }));
    } catch (err) {
      this.error.set(errorText(err, 'Unable to load customers.'));
    } finally {
      this.loading.set(false);
    }
  }

  onSearch(value: string): void {
    this.search = value;
    this.page = 1;
    this.load();
  }

  goTo(page: number): void {
    this.page = page;
    this.load();
  }
}
