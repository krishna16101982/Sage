import { Component, OnDestroy, output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import type { GroupStatus } from '../../../../shared/interfaces/group.interface';

export interface GroupsFilterValue {
  search: string;
  status: '' | GroupStatus;
}

/** Search box and status filter above the groups list. Typing waits a moment before emitting. */
@Component({
  selector: 'app-groups-filter',
  imports: [FormsModule],
  templateUrl: './groups-filter.component.html',
})
export class GroupsFilter implements OnDestroy {
  readonly filterChange = output<GroupsFilterValue>();
  search = '';
  status: '' | GroupStatus = '';
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
