import { Component, HostListener, input, output } from '@angular/core';

/** Error or success message box. Styles: .alert in components.css. */
@Component({
  selector: 'app-alert',
  templateUrl: './alert.component.html',
})
export class AlertComponent {
  readonly kind = input<'error' | 'success'>('error');
}

/** Dialog over the page; closes on Escape or a click on the backdrop. size "md" for forms, "xl" for wide content. */
@Component({
  selector: 'app-modal',
  templateUrl: './modal.component.html',
})
export class ModalComponent {
  readonly title = input.required<string>();
  readonly size = input<'md' | 'xl'>('md');
  readonly closed = output<void>();

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.closed.emit();
  }
}

/** "N total · Previous · Page x of y · Next" under a list. */
@Component({
  selector: 'app-pagination',
  templateUrl: './pagination.component.html',
})
export class PaginationComponent {
  readonly page = input.required<number>();
  readonly totalPages = input.required<number>();
  readonly totalCount = input.required<number>();
  readonly pageChange = output<number>();
}

/** Clickable column header that sorts a list (▲/▼ on the active column). Use on a <th>. */
@Component({
  selector: 'th[appSort]',
  host: { '[attr.aria-sort]': "active() ? (direction() === 'asc' ? 'ascending' : 'descending') : 'none'" },
  templateUrl: './sort-header.component.html',
})
export class SortHeaderComponent {
  readonly active = input.required<boolean>();
  readonly direction = input.required<'asc' | 'desc'>();
  readonly sort = output<'asc' | 'desc'>();
}

/** Password rules shown under password fields (matches the API's policy). */
export const PASSWORD_HINT = 'At least 10 characters with upper and lower case letters, a digit and a symbol.';

/** Formats an ISO date for lists, e.g. "12 Nov 2026, 5:17 pm". */
export function formatDateTime(iso: string | null): string {
  return iso ? new Date(iso).toLocaleString('en-IN', { dateStyle: 'medium', timeStyle: 'short' }) : 'Never';
}
