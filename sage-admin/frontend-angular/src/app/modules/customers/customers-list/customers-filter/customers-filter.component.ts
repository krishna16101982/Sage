import { Component, OnDestroy, output } from '@angular/core';
import { FormsModule } from '@angular/forms';

/** Search box above the customer list. Typing waits a moment before emitting. */
@Component({
  selector: 'app-customers-filter',
  imports: [FormsModule],
  templateUrl: './customers-filter.component.html',
})
export class CustomersFilter implements OnDestroy {
  readonly searchChange = output<string>();
  search = '';
  private timer: ReturnType<typeof setTimeout> | undefined;

  changed(): void {
    clearTimeout(this.timer);
    this.timer = setTimeout(() => this.searchChange.emit(this.search), 250);
  }

  ngOnDestroy(): void {
    clearTimeout(this.timer);
  }
}
