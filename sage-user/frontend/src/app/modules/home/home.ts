import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { BarChart, type BarPoint } from '../../component/bar-chart/bar-chart';
import { Icon } from '../../component/icon/icon';
import { AuthService } from '../../services/auth/auth.service';
import { CustomersService } from '../../services/customers/customers.service';
import { SalesService } from '../../services/sales/sales.service';
import type { Sale, SalesPage } from '../../shared/interfaces/sale.interface';
import { formatDate, monthBounds, monthName, percentChange, today } from '../../shared/utils/dates';
import { formatDateTime, formatInr } from '../../shared/utils/format';

/**
 * Landing page after sign-in: this month's figures (own; a manager also sees the team's), sales per day, quick actions
 * and the latest sales.
 */
@Component({
  selector: 'app-home',
  imports: [RouterLink, Icon, BarChart],
  templateUrl: './home.html',
})
export class Home implements OnInit {
  protected readonly auth = inject(AuthService);
  private readonly sales = inject(SalesService);
  private readonly customers = inject(CustomersService);
  protected readonly inr = formatInr;
  protected readonly date = formatDate;
  protected readonly dateTime = formatDateTime;
  readonly month = monthName();

  readonly mine = signal<SalesPage | null>(null);
  readonly mineLast = signal<SalesPage | null>(null);
  readonly team = signal<SalesPage | null>(null);
  readonly recent = signal<Sale[]>([]);
  readonly customerCount = signal<number | null>(null);
  readonly error = signal<string | null>(null);

  readonly countChange = computed(() => percentChange(this.mine()?.totalCount ?? 0, this.mineLast()?.totalCount ?? 0));
  readonly revenueChange = computed(() => percentChange(this.mine()?.totalSaleAmount ?? 0, this.mineLast()?.totalSaleAmount ?? 0));
  readonly marginPercent = computed(() => {
    const m = this.mine();
    return m && m.totalSaleAmount > 0 ? Math.round((m.totalMargin / m.totalSaleAmount) * 100) : 0;
  });

  /** My sale amount per day of this month, up to today. */
  readonly daily = computed<BarPoint[]>(() => {
    const items = this.mine()?.items ?? [];
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
    const jobs: Promise<unknown>[] = [
      this.sales.list({ scope: 'mine', soldFrom: month.from, soldTo: month.to, status: 'Active', pageSize: 200 }).then((p) => this.mine.set(p)),
      this.sales.list({ scope: 'mine', soldFrom: previous.from, soldTo: previous.to, status: 'Active', pageSize: 1 }).then((p) => this.mineLast.set(p)),
      this.sales.list({ scope: 'mine', pageSize: 5 }).then((p) => this.recent.set(p.items)),
      this.customers.list({ pageSize: 1 }).then((p) => this.customerCount.set(p.totalCount)),
    ];
    if (this.auth.isManager()) {
      jobs.push(this.sales.list({ scope: 'team', soldFrom: month.from, soldTo: month.to, status: 'Active', pageSize: 1 }).then((p) => this.team.set(p)));
    }
    const results = await Promise.allSettled(jobs);
    if (results.some((r) => r.status === 'rejected')) this.error.set('Some figures could not be loaded. Refresh the page to try again.');
  }
}
