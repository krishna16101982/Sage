import { Component, OnDestroy, input, output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import type { SaleStatus, TeamMember } from '../../../../shared/interfaces/sale.interface';

export interface SalesFilterValue {
  search: string;
  status: SaleStatus | '';
  soldFrom: string;
  soldTo: string;
  sellerId: string;
}

/** Filters above the sales list. Typing in the search box waits a moment before emitting. */
@Component({
  selector: 'app-sales-filter',
  imports: [FormsModule],
  templateUrl: './sales-filter.component.html',
})
export class SalesFilter implements OnDestroy {
  /** Team sales only: the manager's team (the seller filter is hidden when empty). */
  readonly members = input<TeamMember[]>([]);
  readonly filterChange = output<SalesFilterValue>();
  search = '';
  status: SaleStatus | '' = '';
  soldFrom = '';
  soldTo = '';
  sellerId = '';
  private timer: ReturnType<typeof setTimeout> | undefined;

  changed(debounce: boolean): void {
    clearTimeout(this.timer);
    const emit = () =>
      this.filterChange.emit({ search: this.search, status: this.status, soldFrom: this.soldFrom, soldTo: this.soldTo, sellerId: this.sellerId });
    if (debounce) this.timer = setTimeout(emit, 250);
    else emit();
  }

  ngOnDestroy(): void {
    clearTimeout(this.timer);
  }
}
