import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { BarChart, type BarPoint } from '../../component/bar-chart/bar-chart';
import { Icon } from '../../component/icon/icon';
import { AuthService } from '../../services/auth/auth.service';
import { CustomersService } from '../../services/customers/customers.service';
import { PermissionService } from '../../services/permission/permission.service';
import { SalesService } from '../../services/sales/sales.service';
import { Perm } from '../../shared/enumarations/permission-codes';
import type { Sale, SalesPage, SalesSummaryRow } from '../../shared/interfaces/sale.interface';
import { formatDate, monthBounds, monthName, percentChange, today } from '../../shared/utils/dates';
import { formatDateTime, formatInr } from '../../shared/utils/format';

/**
 * /dashboard — this month at a glance: sales, revenue, margin, customers, sales per day, top sales persons and the
 * latest sales. Each block is shown only when the user may see that data (Sales.View, Customer.View).
 */
@Component({
  selector: 'app-dashboard',
  imports: [RouterLink, Icon, BarChart],
  templateUrl: './dashboard.html',
})
export class Dashboard implements OnInit {
  protected readonly auth = inject(AuthService);
  private readonly sales = inject(SalesService);
  private readonly customers = inject(CustomersService);
  private readonly permissions = inject(PermissionService);
  protected readonly inr = formatInr;
  protected readonly date = formatDate;
  protected readonly dateTime = formatDateTime;
  protected readonly perm = Perm;

  readonly canSales = this.permissions.has(Perm.SalesView);
  readonly canCustomers = this.permissions.has(Perm.CustomerView);
  readonly canAddCustomer = this.permissions.has(Perm.CustomerAdd);
  /** Colours for the top sales persons, in rank order. */
  protected readonly accents = ['accent-teal', 'accent-blue', 'accent-orange', 'accent-purple', 'accent-green'];
  readonly month = monthName();

  readonly thisMonth = signal<SalesPage | null>(null);
  readonly lastMonth = signal<SalesPage | null>(null);
  readonly topSellers = signal<SalesSummaryRow[]>([]);
  readonly recent = signal<Sale[]>([]);
  readonly customerCount = signal<number | null>(null);
  readonly error = signal<string | null>(null);

  readonly countChange = computed(() => percentChange(this.thisMonth()?.totalCount ?? 0, this.lastMonth()?.totalCount ?? 0));
  readonly revenueChange = computed(() => percentChange(this.thisMonth()?.totalSaleAmount ?? 0, this.lastMonth()?.totalSaleAmount ?? 0));
  readonly marginPercent = computed(() => {
    const m = this.thisMonth();
    return m && m.totalSaleAmount > 0 ? Math.round((m.totalMargin / m.totalSaleAmount) * 100) : 0;
  });
  readonly topMax = computed(() => Math.max(1, ...this.topSellers().map((r) => r.saleAmount)));

  /** Sale amount per day of this month, up to today (from the month's sales, at most 200). */
  readonly daily = computed<BarPoint[]>(() => {
    const items = this.thisMonth()?.items ?? [];
    const days = Number(today().slice(8, 10));
    const sums = new Array<number>(days).fill(0);
    const counts = new Array<number>(days).fill(0);
    for (const s of items) {
      const d = new Date(s.createdAtUtc).getDate() - 1;
      if (d >= 0 && d < days) {
        sums[d] += s.saleAmount;
        counts[d]++;
      }
    }
    const monthShort = new Date().toLocaleDateString('en-IN', { month: 'short' });
    return sums.map((v, i) => ({
      label: i === 0 || (i + 1) % 5 === 0 ? String(i + 1) : '',
      value: v,
      title: `${i + 1} ${monthShort}: ${formatInr(v)} (${counts[i]} sale${counts[i] === 1 ? '' : 's'})`,
    }));
  });

  async ngOnInit(): Promise<void> {
    const month = monthBounds(0);
    const previous = monthBounds(-1);
    const jobs: Promise<unknown>[] = [];
    if (this.canSales) {
      jobs.push(
        this.sales.list({ soldFrom: month.from, soldTo: month.to, status: 'Active', pageSize: 200 }).then((p) => this.thisMonth.set(p)),
        this.sales.list({ soldFrom: previous.from, soldTo: previous.to, status: 'Active', pageSize: 1 }).then((p) => this.lastMonth.set(p)),
        this.sales.summary({ groupBy: 'seller', soldFrom: month.from, soldTo: month.to })
          .then((rows) => this.topSellers.set(rows.filter((r) => r.saleAmount > 0).slice(0, 5))),
        this.sales.list({ pageSize: 6 }).then((p) => this.recent.set(p.items)),
      );
    }
    if (this.canCustomers) jobs.push(this.customers.list({ pageSize: 1 }).then((p) => this.customerCount.set(p.totalCount)));
    const results = await Promise.allSettled(jobs);
    if (results.some((r) => r.status === 'rejected')) this.error.set('Some figures could not be loaded. Refresh the page to try again.');
  }

  initialsOf(name: string): string {
    const parts = name.trim().split(/\s+/);
    return ((parts[0]?.[0] ?? '') + (parts[1]?.[0] ?? '')).toUpperCase();
  }
}
