import { Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Alert } from '../../../component/alert/alert';
import { Pagination } from '../../../component/pagination/pagination';
import { SortHeader } from '../../../component/sort-header/sort-header';
import { MessageService } from '../../../services/message/message.service';
import { errorText } from '../../../services/rest/global-rest-service';
import { SalesService } from '../../../services/sales/sales.service';
import type { SaleQuery, SaleScope, SalesPage, TeamMember } from '../../../shared/interfaces/sale.interface';
import { formatDate } from '../../../shared/utils/dates';
import { formatDateTime, formatInr } from '../../../shared/utils/format';
import { SalesFilter, type SalesFilterValue } from './sales-filter/sales-filter.component';

type SortKey = NonNullable<SaleQuery['sortBy']>;

/**
 * /sales/list (My sales) and /sales/team (Team sales, managers only). The scope comes from the route's data.
 * Totals above the table cover every matching ACTIVE sale, not just this page.
 */
@Component({
  selector: 'app-sales-list',
  imports: [RouterLink, Alert, Pagination, SortHeader, SalesFilter],
  templateUrl: './sales-list.component.html',
})
export class SalesList implements OnInit {
  /** From the route data: 'mine' or 'team'. */
  readonly scope = input<SaleScope>('mine');
  private readonly service = inject(SalesService);
  protected readonly inr = formatInr;
  protected readonly date = formatDate;
  protected readonly dateTime = formatDateTime;

  readonly isTeam = computed(() => this.scope() === 'team');
  filter: SalesFilterValue = { search: '', status: '', soldFrom: '', soldTo: '', sellerId: '' };
  page = 1;
  readonly sortBy = signal<SortKey>('soldAt');
  readonly sortDirection = signal<'asc' | 'desc'>('desc');
  readonly members = signal<TeamMember[]>([]);
  readonly data = signal<SalesPage | null>(null);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly notice = signal<string | null>(inject(MessageService).take());

  ngOnInit(): void {
    if (this.isTeam()) {
      this.service.teamMembers().then(
        (m) => this.members.set(m),
        (err) => this.error.set(errorText(err, 'Unable to load your team.')),
      );
    }
    this.load();
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      this.data.set(
        await this.service.list({
          scope: this.scope(),
          sellerId: this.isTeam() && this.filter.sellerId ? this.filter.sellerId : undefined,
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
