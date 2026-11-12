import { Component, input } from '@angular/core';

/** Error or success message box. Styles: .alert in styles/components.css. */
@Component({
  selector: 'app-alert',
  templateUrl: './alert.html',
})
export class Alert {
  readonly kind = input<'error' | 'success'>('error');
}
