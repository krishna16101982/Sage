import { Component, OnDestroy, OnInit, computed, input, output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import type { SaleStatus, SellerOption } from '../../../../shared/interfaces/sale.interface';

export interface SalesFilterValue {
  search: string;
  sellerId: string;
  managerId: string;
  status: SaleStatus | '';
  soldFrom: string;
  soldTo: string;
}

/**
 * Filters above the sales list: one sales person, or manager-wise (the manager's own sales plus their team's), status
 * and the dates the sales were recorded. Typing in the search box waits a moment before emitting.
 */
@Component({
  selector: 'app-sales-filter',
  imports: [FormsModule],
  templateUrl: './sales-filter.component.html',
})
export class SalesFilter implements OnInit, OnDestroy {
  readonly sellers = input<SellerOption[]>([]);
  /** Starting values (e.g. from the Summary page's links). */
  readonly initial = input<Partial<SalesFilterValue>>({});
  readonly filterChange = output<SalesFilterValue>();
  readonly managers = computed(() => this.sellers().filter((s) => s.role === 'SalesManager'));

  search = '';
  sellerId = '';
  managerId = '';
  status: SaleStatus | '' = '';
  soldFrom = '';
  soldTo = '';
  private timer: ReturnType<typeof setTimeout> | undefined;

  ngOnInit(): void {
    Object.assign(this, { search: '', sellerId: '', managerId: '', status: '', soldFrom: '', soldTo: '', ...this.initial() });
  }

  /** A person and a manager filter at the same time rarely makes sense: choosing one clears the other. */
  pickSeller(): void {
    if (this.sellerId) this.managerId = '';
    this.changed(false);
  }

  pickManager(): void {
    if (this.managerId) this.sellerId = '';
    this.changed(false);
  }

  changed(debounce: boolean): void {
    clearTimeout(this.timer);
    const emit = () =>
      this.filterChange.emit({
        search: this.search, sellerId: this.sellerId, managerId: this.managerId, status: this.status, soldFrom: this.soldFrom, soldTo: this.soldTo,
      });
    if (debounce) this.timer = setTimeout(emit, 250);
    else emit();
  }

  ngOnDestroy(): void {
    clearTimeout(this.timer);
  }
}
