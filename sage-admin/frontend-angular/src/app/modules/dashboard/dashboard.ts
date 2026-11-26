import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Icon } from '../../component/icon/icon';
import { Modal } from '../../component/modal/modal';
import { AuthService } from '../../services/auth/auth.service';
import { LeadsService } from '../../services/leads/leads.service';
import { PermissionService } from '../../services/permission/permission.service';
import { errorText } from '../../services/rest/global-rest-service';
import { SalesService } from '../../services/sales/sales.service';
import { Perm } from '../../shared/enumarations/permission-codes';
import type { LeadSummary, LeadSummaryItem } from '../../shared/interfaces/lead-summary.interface';
import type { SellerOption } from '../../shared/interfaces/sale.interface';
import { formatInr } from '../../shared/utils/format';

/** One slice of the status pie: an SVG path, its colour slot and its share. */
interface Slice {
  item: LeadSummaryItem;
  path: string;
  slot: number;
  percent: number;
}

/**
 * /dashboard — the lead pipeline at a glance, the same as the User Panel's Home: leads per status, conversion rate, won
 * revenue (monthly and annualized, with a per-product breakdown), status / source / aging charts and Self Working vs
 * Passed. Every figure comes from one database call (dbo.usp_Lead_Summary): all leads, or one sales person's (the leads
 * they created, generated or are assigned to). Needs Sales.View.
 */
@Component({
  selector: 'app-dashboard',
  imports: [RouterLink, Icon, Modal],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.css',
})
export class Dashboard implements OnInit {
  protected readonly auth = inject(AuthService);
  private readonly leads = inject(LeadsService);
  private readonly sales = inject(SalesService);
  private readonly permissions = inject(PermissionService);
  protected readonly inr = formatInr;

  readonly canSales = this.permissions.has(Perm.SalesView);
  readonly canAddCustomer = this.permissions.has(Perm.CustomerAdd);

  /** Sales users for the filter; '' = all leads. */
  readonly sellers = signal<SellerOption[]>([]);
  readonly sellerId = signal('');
  readonly summary = signal<LeadSummary | null>(null);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly showProducts = signal(false);

  readonly sellerName = computed(() => this.sellers().find((s) => s.id === this.sellerId())?.name ?? null);

  /** Colour slot per status code (validated categorical palette; legend + counts carry identity too). */
  private readonly statusSlot: Record<string, number> = { OPEN: 1, WARM: 2, WON: 3, LOST: 4, HOLD: 5, DISQUALIFIED: 6 };
  slotOf(code: string | null): number {
    return this.statusSlot[code ?? ''] ?? 5;
  }

  /** Pie slices of the status breakdown (statuses with leads only), starting at 12 o'clock, clockwise. */
  readonly slices = computed<Slice[]>(() => {
    const items = (this.summary()?.statuses ?? []).filter((x) => x.total > 0);
    const total = items.reduce((sum, x) => sum + x.total, 0);
    if (!total) return [];
    const r = 80;
    const point = (angle: number) => `${(100 + r * Math.sin(angle)).toFixed(2)} ${(100 - r * Math.cos(angle)).toFixed(2)}`;
    let start = 0;
    return items.map((item) => {
      const sweep = (item.total / total) * Math.PI * 2;
      const end = start + sweep;
      const path = items.length === 1
        ? `M 100 20 A ${r} ${r} 0 1 1 99.99 20 Z`
        : `M 100 100 L ${point(start)} A ${r} ${r} 0 ${sweep > Math.PI ? 1 : 0} 1 ${point(end)} Z`;
      start = end;
      return { item, path, slot: this.slotOf(item.code), percent: Math.round((item.total / total) * 1000) / 10 };
    });
  });

  /** Longest bar of each chart = 100%. */
  readonly maxSource = computed(() => Math.max(1, ...(this.summary()?.sources ?? []).map((x) => x.total)));
  readonly maxAging = computed(() => Math.max(1, ...(this.summary()?.aging ?? []).map((x) => x.total)));
  /** Column heights in % of the plot. */
  agingHeight(n: number): number {
    return n > 0 ? Math.max(2, (n / this.maxAging()) * 100) : 0;
  }

  winRate(item: LeadSummaryItem): number {
    return item.total > 0 ? Math.round((item.won / item.total) * 1000) / 10 : 0;
  }

  /** "17 leads (35.4%)". */
  share(item: LeadSummaryItem): string {
    const total = this.summary()?.totalLeads ?? 0;
    const pct = total > 0 ? Math.round((item.total / total) * 1000) / 10 : 0;
    return `${item.total} lead${item.total === 1 ? '' : 's'} (${pct}%)`;
  }

  async ngOnInit(): Promise<void> {
    if (!this.canSales) return;
    this.sales.sellers().then((s) => this.sellers.set(s), () => this.sellers.set([]));
    await this.load();
  }

  async setSeller(id: string): Promise<void> {
    if (this.sellerId() === id) return;
    this.sellerId.set(id);
    await this.load();
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      this.summary.set(await this.leads.summary(this.sellerId() || undefined));
    } catch (err) {
      this.error.set(errorText(err, 'Unable to load the dashboard.'));
    } finally {
      this.loading.set(false);
    }
  }
}
