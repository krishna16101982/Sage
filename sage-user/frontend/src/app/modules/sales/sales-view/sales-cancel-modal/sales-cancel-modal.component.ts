import { Component, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Alert } from '../../../../component/alert/alert';
import { Modal } from '../../../../component/modal/modal';
import { errorText } from '../../../../services/rest/global-rest-service';
import { SalesService } from '../../../../services/sales/sales.service';
import type { Sale } from '../../../../shared/interfaces/sale.interface';

/**
 * Asks for a reason, then either cancels the sale (mode "cancel": sales managers) or sends a cancellation request to
 * the manager (mode "request": sales persons their manager allowed). A cancelled sale stays on record; it is never deleted.
 */
@Component({
  selector: 'app-sales-cancel-modal',
  imports: [FormsModule, Alert, Modal],
  templateUrl: './sales-cancel-modal.component.html',
})
export class SalesCancelModal {
  readonly sale = input.required<Sale>();
  readonly mode = input<'cancel' | 'request'>('cancel');
  readonly closed = output<void>();
  /** Emits the message to show once the sale was cancelled or the request was sent. */
  readonly done = output<string>();
  private readonly service = inject(SalesService);
  reason = '';
  readonly error = signal<string | null>(null);
  readonly busy = signal(false);

  async submit(): Promise<void> {
    this.error.set(null);
    this.busy.set(true);
    try {
      if (this.mode() === 'request') {
        await this.service.requestCancel(this.sale().id, this.reason);
        this.done.emit('Cancellation requested. Your manager will approve or reject it.');
      } else {
        await this.service.cancel(this.sale().id, this.reason);
        this.done.emit(`Sale ${this.sale().saleNumber} cancelled.`);
      }
    } catch (err) {
      this.error.set(errorText(err));
    } finally {
      this.busy.set(false);
    }
  }
}
