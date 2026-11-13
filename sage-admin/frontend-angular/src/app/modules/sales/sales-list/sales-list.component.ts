import { Component, OnInit, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Alert } from '../../../component/alert/alert';
import { Pagination } from '../../../component/pagination/pagination';
import { SortHeader } from '../../../component/sort-header/sort-header';
import { errorText } from '../../../services/rest/global-rest-service';
import { SalesService } from '../../../services/sales/sales.service';
import type { SaleQuery, SalesPage, SellerOption } from '../../../shared/interfaces/sale.interface';
import { formatDate } from '../../../shared/utils/dates';
import { formatDateTime, formatInr } from '../../../shared/utils/format';
import { SalesTabs } from '../sales-tabs/sales-tabs.component';
import { SalesFilter, type SalesFilterValue } from './sales-filter/sales-filter.component';

type SortKey = NonNullable<SaleQuery['sortBy']>;

/**
 * /sales/list — every sale recorded in the User Panel, by sales person or manager-wise. Totals above the table cover every
 * matching ACTIVE sale. Query parameters (?sellerId=, ?managerId=, ?customerId=, ?soldFrom=, ?soldTo=) preset the filters.
 */
@Component({
  selector: 'app-sales-list',
  imports: [RouterLink, Alert, Pagination, SortHeader, SalesTabs, SalesFilter],
  templateUrl: './sales-list.component.html',
})
export class SalesList implements OnInit {
  // From the query string (links from Summary and Customers).
  readonly sellerId = input<string>();
  readonly managerId = input<string>();
  readonly customerId = input<string>();
  readonly soldFrom = input<string>();
  readonly soldTo = input<string>();

  private readonly service = inject(SalesService);
  protected readonly inr = formatInr;
  protected readonly date = formatDate;
  protected readonly dateTime = formatDateTime;

  filter: SalesFilterValue = { search: '', sellerId: '', managerId: '', status: '', soldFrom: '', soldTo: '' };
  page = 1;
  readonly sortBy = signal<SortKey>('soldAt');
  readonly sortDirection = signal<'asc' | 'desc'>('desc');
  readonly sellers = signal<SellerOption[]>([]);
  readonly data = signal<SalesPage | null>(null);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);

  ngOnInit(): void {
    this.filter = {
      ...this.filter,
      sellerId: this.sellerId() ?? '',
      managerId: this.managerId() ?? '',
      soldFrom: this.soldFrom() ?? '',
      soldTo: this.soldTo() ?? '',
    };
    this.service.sellers().then(
      (s) => this.sellers.set(s),
      (err) => this.error.set(errorText(err, 'Unable to load sales persons.')),
    );
    this.load();
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      this.data.set(
        await this.service.list({
          sellerId: this.filter.sellerId || undefined,
          managerId: this.filter.managerId || undefined,
          customerId: this.customerId() || undefined,
          search: this.filter.search.trim() || undefined,
          status: this.filter.status || undefined,
          soldFrom: this.filter.soldFrom || undefined,
          soldTo: this.filter.soldTo || undefined,
          sortBy: this.sortBy(),
          sortDirection: this.sortDirection(),
          page: this.page,
          pageSize: 20,
        }),
      );
    } catch (err) {
      this.error.set(errorText(err, 'Unable to load sales.'));
    } finally {
      this.loading.set(false);
    }
  }

  onFilter(value: SalesFilterValue): void {
    this.filter = value;
    this.page = 1;
    this.load();
  }

  sort(key: SortKey, direction: 'asc' | 'desc'): void {
    this.sortBy.set(key);
    this.sortDirection.set(direction);
    this.page = 1;
    this.load();
  }

  goTo(page: number): void {
    this.page = page;
    this.load();
  }
}
