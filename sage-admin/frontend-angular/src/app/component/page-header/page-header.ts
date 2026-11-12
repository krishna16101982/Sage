import { Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';

/** Title of an Add/Edit page with a "Back to …" link to its list. */
@Component({
  selector: 'app-page-header',
  imports: [RouterLink],
  templateUrl: './page-header.html',
})
export class PageHeader {
  readonly title = input.required<string>();
  /** Where "Back" goes, e.g. '/users/list'. */
  readonly backLink = input.required<string>();
  readonly backLabel = input('Back to list');
}
