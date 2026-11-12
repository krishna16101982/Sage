import { Component, input, output } from '@angular/core';

/** Clickable column header that sorts a list (▲/▼ on the active column). Use on a <th appSort>. */
@Component({
  selector: 'th[appSort]',
  host: { '[attr.aria-sort]': "active() ? (direction() === 'asc' ? 'ascending' : 'descending') : 'none'" },
  templateUrl: './sort-header.html',
})
export class SortHeader {
  readonly active = input.required<boolean>();
  readonly direction = input.required<'asc' | 'desc'>();
  readonly sort = output<'asc' | 'desc'>();
}
