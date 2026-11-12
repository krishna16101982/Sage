import { Component, OnDestroy, output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import type { SalesRole } from '../../../../shared/interfaces/sales-user.interface';

export interface SalesUsersFilterValue {
  search: string;
  role: '' | SalesRole;
  status: '' | 'Active' | 'Inactive';
}

/** Search box, role and status filters above the sales users list. Typing waits a moment before emitting. */
@Component({
  selector: 'app-sales-users-filter',
  imports: [FormsModule],
  templateUrl: './sales-users-filter.component.html',
})
export class SalesUsersFilter implements OnDestroy {
  readonly filterChange = output<SalesUsersFilterValue>();
  search = '';
  role: '' | SalesRole = '';
  status: '' | 'Active' | 'Inactive' = '';
  private timer: ReturnType<typeof setTimeout> | undefined;

  changed(debounce: boolean): void {
    clearTimeout(this.timer);
    const emit = () => this.filterChange.emit({ search: this.search, role: this.role, status: this.status });
    if (debounce) this.timer = setTimeout(emit, 250);
    else emit();
  }

  /** Used by the list when "Team: N" is clicked (a team includes every role). */
  clearRole(): void {
    this.role = '';
  }

  ngOnDestroy(): void {
    clearTimeout(this.timer);
  }
}
