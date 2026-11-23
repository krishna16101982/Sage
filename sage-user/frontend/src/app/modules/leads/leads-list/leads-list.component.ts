import { Component, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { Alert } from '../../../component/alert/alert';
import { Modal } from '../../../component/modal/modal';
import { Pagination } from '../../../component/pagination/pagination';
import { LeadsService } from '../../../services/leads/leads.service';
import { errorText } from '../../../services/rest/global-rest-service';
import type { PagedResult } from '../../../shared/interfaces/api.interface';
import type { Lead, LeadAging, LeadOptions } from '../../../shared/interfaces/lead.interface';
import { formatInr } from '../../../shared/utils/format';
import { LeadDialog } from '../lead-dialog/lead-dialog.component';

/** Filters of the list (sent to the API; the database pages and filters). */
interface LeadFilters {
  search: string;
  statusId: number | null;
  aging: LeadAging | null;
  assignedId: string | null;
  generatedById: string | null;
  productId: number | null;
  from: string;
  to: string;
}

const PAGE_SIZE = 25;

/**
 * /leads — Lead Records. Every sales user sees every lead. Filters, sorting and paging happen in the database
 * (dbo.usp_Lead_GetPaged); the page only shows one page at a time. Rows offer View, Edit (creator and their manager),
 * New Order (Won leads with no order yet) and Delete (managers).
 */
@Component({
  selector: 'app-leads-list',
  imports: [FormsModule, RouterLink, Alert, Modal, Pagination, LeadDialog],
  templateUrl: './leads-list.component.html',
  styles: [`
    .lead-filters { display: flex; flex-wrap: wrap; gap: 0.5rem; align-items: center; }
    .lead-filters .field { width: auto; min-width: 9rem; }
    .lead-filters .lead-search { min-width: 16rem; flex: 1 1 16rem; }
    .pill { display: inline-flex; align-items: center; gap: 0.3rem; padding: 0.1rem 0.5rem; border-radius: 999px; font-size: 0.75rem; font-weight: 500; white-space: nowrap; }
    .pill::before { content: ''; width: 0.4rem; height: 0.4rem; border-radius: 50%; background: currentColor; }
    .pill-OPEN { background: #eef2ff; color: #4f46e5; }
    .pill-WARM { background: #fef3c7; color: #b45309; }
    .pill-WON { background: #ecfdf5; color: #047857; }
    .pill-LOST { background: #fef2f2; color: #dc2626; }
    .pill-HOLD { background: #f1f5f9; color: #475569; }
    .pill-DISQUALIFIED { background: #f1f5f9; color: #334155; }
    .aging-lt7 { color: #047857; }
    .aging-7to30 { color: var(--color-text-muted); }
    .aging-31to60 { color: #c2410c; }
    .aging-gt60 { color: #dc2626; font-weight: 500; }
    .lead-actions { display: flex; gap: 0.75rem; white-space: nowrap; }
  `],
})
export class LeadsList implements OnInit, OnDestroy {
  private readonly service = inject(LeadsService);
  private readonly router = inject(Router);
  protected readonly inr = formatInr;

  readonly options = signal<LeadOptions | null>(null);
  readonly data = signal<PagedResult<Lead> | null>(null);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly notice = signal<string | null>(null);
  filters: LeadFilters = { search: '', statusId: null, aging: null, assignedId: null, generatedById: null, productId: null, from: '', to: '' };
  page = 1;
  private searchTimer: ReturnType<typeof setTimeout> | undefined;

  /** The open dialog: a new lead, or one lead in view / edit. */
  readonly dialog = signal<{ mode: 'new' | 'view' | 'edit'; lead: Lead | null } | null>(null);
  readonly deleting = signal<Lead | null>(null);
  readonly deleteBusy = signal(false);
  readonly deleteError = signal<string | null>(null);

  protected readonly agingChoices: { value: LeadAging; label: string }[] = [
    { value: 'lt7', label: '< 7 days' }, { value: '7to30', label: '7-30 days' }, { value: '31to60', label: '31-60 days' }, { value: 'gt60', label: '> 60 days' },
  ];
  /** Everyone who may appear on a lead (inactive people stay in the filters). */
  readonly people = computed(() => this.options()?.salesUsers ?? []);

  async ngOnInit(): Promise<void> {
    this.service.options().then((o) => this.options.set(o), (err) => this.error.set(errorText(err, 'Unable to load the lead lists.')));
    await this.load();
  }

  ngOnDestroy(): void {
    clearTimeout(this.searchTimer);
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    const f = this.filters;
    try {
      this.data.set(await this.service.list({
        search: f.search.trim() || undefined,
        statusId: f.statusId ?? undefined,
        aging: f.aging ?? undefined,
        assignedId: f.assignedId ?? undefined,
        generatedById: f.generatedById ?? undefined,
        productId: f.productId ?? undefined,
        from: f.from || undefined,
        to: f.to || undefined,
        page: this.page,
        pageSize: PAGE_SIZE,
      }));
    } catch (err) {
      this.error.set(errorText(err, 'Unable to load leads.'));
    } finally {
      this.loading.set(false);
    }
  }

  /** Any filter changed: back to page 1 (typing in search waits a moment). */
  filterChanged(debounce = false): void {
    clearTimeout(this.searchTimer);
    const run = () => { this.page = 1; this.load(); };
    if (debounce) this.searchTimer = setTimeout(run, 300); else run();
  }

  goTo(page: number): void {
    this.page = page;
    this.load();
  }

  /** Row number across pages. */
  rowNumber(index: number): number {
    return ((this.data()?.page ?? 1) - 1) * (this.data()?.pageSize ?? PAGE_SIZE) + index + 1;
  }

  agingBucket(days: number): LeadAging {
    return days < 7 ? 'lt7' : days <= 30 ? '7to30' : days <= 60 ? '31to60' : 'gt60';
  }

  agingText(days: number): string {
    return this.agingChoices.find((c) => c.value === this.agingBucket(days))!.label;
  }

  open(mode: 'new' | 'view' | 'edit', lead: Lead | null = null): void {
    this.notice.set(null);
    this.dialog.set({ mode, lead });
  }

  async saved(event: { lead: Lead; message: string }): Promise<void> {
    this.dialog.set(null);
    this.notice.set(event.message);
    await this.load();
  }

  newOrder(lead: Lead): void {
    this.router.navigate(['/sales/order-form'], { queryParams: { leadId: lead.id } });
  }

  async confirmDelete(): Promise<void> {
    const lead = this.deleting();
    if (!lead) return;
    this.deleteBusy.set(true);
    this.deleteError.set(null);
    try {
      await this.service.remove(lead.id);
      this.deleting.set(null);
      this.notice.set(`Lead ${lead.leadNumber} deleted.`);
      await this.load();
    } catch (err) {
      this.deleteError.set(errorText(err));
    } finally {
      this.deleteBusy.set(false);
    }
  }
}
