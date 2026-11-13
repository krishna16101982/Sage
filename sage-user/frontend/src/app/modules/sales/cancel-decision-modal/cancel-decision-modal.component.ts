import { Component, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Alert } from '../../../component/alert/alert';
import { Modal } from '../../../component/modal/modal';
import { errorText } from '../../../services/rest/global-rest-service';
import { SalesService } from '../../../services/sales/sales.service';
import type { CancelRequest } from '../../../shared/interfaces/sale.interface';
import { formatInr } from '../../../shared/utils/format';

/** Manager: approve (the sale is cancelled) or reject a cancellation request, with an optional note to the sales person. */
@Component({
  selector: 'app-cancel-decision-modal',
  imports: [FormsModule, Alert, Modal],
  templateUrl: './cancel-decision-modal.component.html',
})
export class CancelDecisionModal {
  readonly request = input.required<CancelRequest>();
  readonly approve = input.required<boolean>();
  readonly closed = output<void>();
  /** Emits the message to show after deciding. */
  readonly decided = output<string>();
  private readonly service = inject(SalesService);
  protected readonly inr = formatInr;
  note = '';
  readonly error = signal<string | null>(null);
  readonly busy = signal(false);

  async submit(): Promise<void> {
    this.error.set(null);
    this.busy.set(true);
    const r = this.request();
    try {
      if (this.approve()) {
        await this.service.approveCancel(r.id, this.note.trim() || undefined);
        this.decided.emit(`Approved: sale ${r.saleNumber} is cancelled.`);
      } else {
        await this.service.rejectCancel(r.id, this.note.trim() || undefined);
        this.decided.emit(`Rejected: sale ${r.saleNumber} stays active.`);
      }
    } catch (err) {
      this.error.set(errorText(err));
    } finally {
      this.busy.set(false);
    }
  }
}
