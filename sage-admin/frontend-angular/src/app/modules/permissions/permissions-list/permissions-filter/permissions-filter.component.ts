import { Component, OnDestroy, input, output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import type { ModuleItemDto } from '../../../../shared/interfaces/permission.interface';

export interface PermissionsFilterValue {
  search: string;
  moduleId: number | '';
  status: '' | 'true' | 'false';
}

/** Search box, module and status filters above the permissions list. Typing waits a moment before emitting. */
@Component({
  selector: 'app-permissions-filter',
  imports: [FormsModule],
  templateUrl: './permissions-filter.component.html',
})
export class PermissionsFilter implements OnDestroy {
  readonly modules = input.required<ModuleItemDto[]>();
  readonly filterChange = output<PermissionsFilterValue>();
  search = '';
  moduleId: number | '' = '';
  status: '' | 'true' | 'false' = '';
  private timer: ReturnType<typeof setTimeout> | undefined;

  changed(debounce: boolean): void {
    clearTimeout(this.timer);
    const emit = () => this.filterChange.emit({ search: this.search, moduleId: this.moduleId, status: this.status });
    if (debounce) this.timer = setTimeout(emit, 250);
    else emit();
  }

  ngOnDestroy(): void {
    clearTimeout(this.timer);
  }
}
