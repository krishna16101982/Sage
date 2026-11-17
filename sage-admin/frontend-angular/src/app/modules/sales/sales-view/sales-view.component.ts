import { Component, OnInit, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Alert } from '../../../component/alert/alert';
import { errorText } from '../../../services/rest/global-rest-service';
import { SalesService } from '../../../services/sales/sales.service';
import type { Sale } from '../../../shared/interfaces/sale.interface';
import { formatDate } from '../../../shared/utils/dates';
import { WORDING_SECTIONS } from '../../../shared/utils/order-wording';
import { formatDateTime, formatDuration, formatInr } from '../../../shared/utils/format';

/** /sales/:id — one sale, read-only (sales are recorded and cancelled in the User Panel). */
@Component({
  selector: 'app-sales-view',
  imports: [RouterLink, Alert],
  templateUrl: './sales-view.component.html',
})
export class SalesView implements OnInit {
  protected readonly wordingSections = WORDING_SECTIONS;
  readonly wordingOpen = signal(false);
  /** From the route (:id). */
  readonly id = input.required<string>();
  private readonly service = inject(SalesService);
  protected readonly inr = formatInr;
  protected readonly date = formatDate;
  protected readonly dateTime = formatDateTime;
  protected readonly durationText = formatDuration;
  readonly sale = signal<Sale | null>(null);
  readonly error = signal<string | null>(null);
  readonly pdfBusy = signal(false);
  readonly pdfError = signal<string | null>(null);

  async ngOnInit(): Promise<void> {
    try {
      this.sale.set(await this.service.get(this.id()));
    } catch (err) {
      this.error.set(errorText(err, 'Sale not found.'));
    }
  }

  /** Opens (preview) or saves (download) the order form PDF saved by the User Panel. */
  async orderPdf(download: boolean): Promise<void> {
    const s = this.sale();
    if (!s) return;
    this.pdfError.set(null);
    this.pdfBusy.set(true);
    try {
      if (download) await this.service.downloadOrderPdf(s.id, s.saleNumber);
      else await this.service.previewOrderPdf(s.id);
    } catch (err) {
      this.pdfError.set(errorText(err, 'The order form PDF could not be opened.'));
    } finally {
      this.pdfBusy.set(false);
    }
  }
}
