import { Component, OnDestroy, output } from '@angular/core';
import { FormsModule } from '@angular/forms';

export interface AdminUsersFilterValue {
  search: string;
  status: '' | 'Active' | 'Inactive';
}

/** Search box and status filter above the admin users list. Typing waits a moment before emitting. */
@Component({
  selector: 'app-admin-users-filter',
  imports: [FormsModule],
  templateUrl: './admin-users-filter.component.html',
})
export class AdminUsersFilter implements OnDestroy {
  readonly filterChange = output<AdminUsersFilterValue>();
  search = '';
  status: '' | 'Active' | 'Inactive' = '';
  private timer: ReturnType<typeof setTimeout> | undefined;

  changed(debounce: boolean): void {
    clearTimeout(this.timer);
    const emit = () => this.filterChange.emit({ search: this.search, status: this.status });
    if (debounce) this.timer = setTimeout(emit, 250);
    else emit();
  }

  ngOnDestroy(): void {
    clearTimeout(this.timer);
  }
}
