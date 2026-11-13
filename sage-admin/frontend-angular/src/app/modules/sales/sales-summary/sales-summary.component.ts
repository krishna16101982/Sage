import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Alert } from '../../../component/alert/alert';
import { errorText } from '../../../services/rest/global-rest-service';
import { SalesService } from '../../../services/sales/sales.service';
import type { SalesSummaryRow } from '../../../shared/interfaces/sale.interface';
import { firstOfMonth, today } from '../../../shared/utils/dates';
import { formatInr } from '../../../shared/utils/format';
import { SalesTabs } from '../sales-tabs/sales-tabs.component';

/**
 * /sales/summary — totals per sales person, or per manager (their own sales plus their team's), for a date range.
 * Amounts cover active sales; cancelled sales are counted separately. Each row links to its sales.
 */
@Component({
  selector: 'app-sales-summary',
  imports: [FormsModule, RouterLink, Alert, SalesTabs],
  templateUrl: './sales-summary.component.html',
})
export class SalesSummary implements OnInit {
  private readonly service = inject(SalesService);
  protected readonly inr = formatInr;

  readonly groupBy = signal<'seller' | 'manager'>('seller');
  soldFrom = firstOfMonth();
  soldTo = today();
  readonly rows = signal<SalesSummaryRow[] | null>(null);
  readonly error = signal<string | null>(null);
  readonly loading = signal(false);
  readonly exporting = signal(false);

  readonly totals = computed(() => {
    const rows = this.rows() ?? [];
    const sum = (f: (r: SalesSummaryRow) => number) => rows.reduce((t, r) => t + f(r), 0);
    return {
      saleCount: sum((r) => r.saleCount),
      cancelledCount: sum((r) => r.cancelledCount),
      listAmount: sum((r) => r.listAmount),
      discountAmount: sum((r) => r.discountAmount),
      saleAmount: sum((r) => r.saleAmount),
      margin: sum((r) => r.margin),
    };
  });

  ngOnInit(): void {
    this.load();
  }

  setGroup(group: 'seller' | 'manager'): void {
    this.groupBy.set(group);
    this.load();
  }

  /** Whole period, no dates. */
  allTime(): void {
    this.soldFrom = '';
    this.soldTo = '';
    this.load();
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      this.rows.set(await this.service.summary({ groupBy: this.groupBy(), soldFrom: this.soldFrom || undefined, soldTo: this.soldTo || undefined }));
    } catch (err) {
      this.error.set(errorText(err, 'Unable to load the summary.'));
    } finally {
      this.loading.set(false);
    }
  }

  /** Downloads this summary (same grouping and dates) as a CSV file with a total row. */
  async export(): Promise<void> {
    this.exporting.set(true);
    this.error.set(null);
    try {
      await this.service.exportSummary({ groupBy: this.groupBy(), soldFrom: this.soldFrom || undefined, soldTo: this.soldTo || undefined });
    } catch (err) {
      this.error.set(errorText(err, 'Unable to export the summary.'));
    } finally {
      this.exporting.set(false);
    }
  }

  /** Query string for "View sales" on a row: the same person (or team) and dates. */
  salesQuery(row: SalesSummaryRow): Record<string, string> {
    const q: Record<string, string> = this.groupBy() === 'manager' ? { managerId: row.id } : { sellerId: row.id };
    if (this.soldFrom) q['soldFrom'] = this.soldFrom;
    if (this.soldTo) q['soldTo'] = this.soldTo;
    return q;
  }
}
