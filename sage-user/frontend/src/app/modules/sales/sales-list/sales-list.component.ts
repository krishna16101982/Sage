import { Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { Alert } from '../../../component/alert/alert';
import { Pagination } from '../../../component/pagination/pagination';
import { SortHeader } from '../../../component/sort-header/sort-header';
import { AuthService } from '../../../services/auth/auth.service';
import { MessageService } from '../../../services/message/message.service';
import { errorText } from '../../../services/rest/global-rest-service';
import { SalesService } from '../../../services/sales/sales.service';
import { OrderFormService } from '../../../services/order-form/order-form.service';
import { TRACKING_CANCELLED, trackingStatusesOf, type TrackingStatusOption } from '../../../shared/interfaces/order-form.interface';
import type { Sale, SaleQuery, SaleScope, SalesPage, TeamMember } from '../../../shared/interfaces/sale.interface';
import { formatDate } from '../../../shared/utils/dates';
import { formatDateTime, formatInr } from '../../../shared/utils/format';
import { SalesCancelModal } from '../sales-view/sales-cancel-modal/sales-cancel-modal.component';
import { SalesFilter, type SalesFilterValue } from './sales-filter/sales-filter.component';

type SortKey = NonNullable<SaleQuery['sortBy']>;

/**
 * /sales/list (Order forms) and /sales/team (Team order forms, managers only). The scope comes from the route's data.
 * One page at a time from the database (dbo.usp_Sale_GetPaged). Each row: company, lead, product, seats, term, total with
 * GST and the tracking status, which can be changed in the row (Cancelled opens Cancel: a manager cancels, a sales person
 * their manager allowed asks for it). Actions: Edit, PDF, Duplicate (a new order filled from this one), Cancel.
 * Nothing is ever deleted.
 */
@Component({
  selector: 'app-sales-list',
  imports: [RouterLink, Alert, Pagination, SortHeader, SalesFilter, SalesCancelModal],
  templateUrl: './sales-list.component.html',
  styles: [`
    .track { border: 0; border-radius: 999px; padding: 0.15rem 1.6rem 0.15rem 0.6rem; font-size: 0.75rem; font-weight: 500; cursor: pointer; width: auto; min-width: 9rem; }
    .track-DRAFT { background: #ffedd5; color: #c2410c; }
    .track-SENT_DOCUSIGN { background: #e0f2fe; color: #0369a1; }
    .track-SIGNED { background: #dcfce7; color: #15803d; }
    .track-CANCELLED { background: #fee2e2; color: #b91c1c; }
    .order-actions { display: flex; gap: 0.75rem; white-space: nowrap; }
  `],
})
export class SalesList implements OnInit {
  /** From the route data: 'mine' or 'team'. */
  readonly scope = input<SaleScope>('mine');
  private readonly service = inject(SalesService);
  private readonly orderForms = inject(OrderFormService);
  private readonly router = inject(Router);
  private readonly auth = inject(AuthService);
  readonly trackingStatuses = signal<TrackingStatusOption[]>([]);
  /** The row whose tracking status is being saved. */
  readonly savingTracking = signal<string | null>(null);

  /** The statuses a row can be moved to: Cancelled only for someone who may cancel (or ask to cancel) it. */
  trackingChoices(s: Sale): TrackingStatusOption[] {
    return this.trackingStatuses().filter((t) => t.id !== TRACKING_CANCELLED || !!this.cancelMode(s));
  }

  /**
   * A row's tracking status changed. Cancelled opens Cancel (with a reason) and the box shows the current status until
   * the order is really cancelled; the others are saved at once (the box goes back if saving fails).
   */
  async trackingChanged(s: Sale, box: HTMLSelectElement): Promise<void> {
    const id = Number(box.value);
    const restore = () => (box.value = String(s.trackingStatusId));
    this.error.set(null);
    this.notice.set(null);
    if (id === TRACKING_CANCELLED) {
      restore();
      const mode = this.cancelMode(s);
      if (mode) this.cancelling.set({ sale: s, mode });
      return;
    }
    this.savingTracking.set(s.id);
    try {
      const updated = await this.orderForms.setTracking(s.id, id);
      this.data.update((d) => (d ? { ...d, items: d.items.map((x) => (x.id === s.id ? updated : x)) } : d));
      this.notice.set(`${s.saleNumber}: ${updated.trackingName}.`);
    } catch (err) {
      restore();
      this.error.set(errorText(err));
    } finally {
      this.savingTracking.set(null);
    }
  }

  /** "12 months", "12 + 2 months". */
  termText(s: Sale): string {
    const paid = (s.durationUnit === 'Year' ? 12 : s.durationUnit === 'Quarter' ? 3 : 1) * s.durationCount;
    return s.freeMonths > 0 ? `${paid} + ${s.freeMonths} months` : `${paid} months`;
  }

  async pdf(s: Sale): Promise<void> {
    this.error.set(null);
    try {
      await this.orderForms.previewPdf(s.id);
    } catch (err) {
      this.error.set(errorText(err, 'The order form PDF could not be opened.'));
    }
  }

  duplicate(s: Sale): void {
    this.router.navigate(['/sales/order-form'], { queryParams: { copyFrom: s.id } });
  }
  /** The row whose Cancel dialog is open, and whether it cancels (manager) or asks the manager (sales person). */
  readonly cancelling = signal<{ sale: Sale; mode: 'cancel' | 'request' } | null>(null);

  canEdit(s: Sale): boolean {
    return s.hasOrderForm && s.status === 'Active' && !s.hasPendingCancelRequest;
  }

  /** 'cancel' for a manager, 'request' for a sales person their manager allowed (own sale, no request waiting), else null. */
  cancelMode(s: Sale): 'cancel' | 'request' | null {
    const user = this.auth.user();
    if (s.status !== 'Active' || !user) return null;
    if (user.isManager) return 'cancel';
    return s.sellerId === user.id && user.canRequestCancellation && !s.hasPendingCancelRequest ? 'request' : null;
  }

  async cancelled(message: string): Promise<void> {
    this.cancelling.set(null);
    this.notice.set(message);
    await this.load();
  }
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
    this.orderForms.options().then((o) => this.trackingStatuses.set(trackingStatusesOf(o)), () => this.trackingStatuses.set(trackingStatusesOf(null)));
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
