import { Component, input } from '@angular/core';

/** Temporary body for screens that are delivered in a later sprint. The title comes from the route's data. */
@Component({
  selector: 'app-coming-soon',
  templateUrl: './coming-soon.html',
})
export class ComingSoon {
  readonly title = input('');
}
