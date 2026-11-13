import { Component, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Alert } from '../../../../component/alert/alert';
import { Modal } from '../../../../component/modal/modal';
import { errorText } from '../../../../services/rest/global-rest-service';
import { SalesService } from '../../../../services/sales/sales.service';
import type { Sale } from '../../../../shared/interfaces/sale.interface';

/** "Cancel sale" confirmation with a required reason. A cancelled sale stays on record; it is never deleted. */
@Component({
  selector: 'app-sales-cancel-modal',
  imports: [FormsModule, Alert, Modal],
  templateUrl: './sales-cancel-modal.component.html',
})
export class SalesCancelModal {
  readonly sale = input.required<Sale>();
  readonly closed = output<void>();
  /** Emits the sale as it is after cancelling. */
  readonly cancelled = output<Sale>();
  private readonly service = inject(SalesService);
  reason = '';
  readonly error = signal<string | null>(null);
  readonly busy = signal(false);

  async submit(): Promise<void> {
    this.error.set(null);
    this.busy.set(true);
    try {
      this.cancelled.emit(await this.service.cancel(this.sale().id, this.reason));
    } catch (err) {
      this.error.set(errorText(err));
    } finally {
      this.busy.set(false);
    }
  }
}
