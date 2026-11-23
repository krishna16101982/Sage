import { Injectable, inject } from '@angular/core';
import type { OrderDetails, OrderFormOptions, OrderInput } from '../../shared/interfaces/order-form.interface';
import type { Sale } from '../../shared/interfaces/sale.interface';
import { GlobalRestService } from '../rest/global-rest-service';

/** /api/user/order-form — the New order form: its lists and defaults, saving an order, a sale's order details. */
@Injectable({ providedIn: 'root' })
export class OrderFormService {
  private readonly rest = inject(GlobalRestService);

  options = () => this.rest.get<OrderFormOptions>('/api/user/order-form/options');
  create = (body: OrderInput) => this.rest.post<Sale>('/api/user/order-form', body);
  /** Edits an order saved with the order form (same body); the PDF is made again. */
  update = (saleId: string, body: OrderInput) => this.rest.put<Sale>(`/api/user/order-form/${saleId}`, body);
  /** Order forms list: Draft / Sent to DocuSign / Signed (Cancelled goes through Cancel, with a reason). */
  setTracking = (saleId: string, trackingStatusId: number, reason?: string) =>
    this.rest.put<Sale>(`/api/user/order-form/${saleId}/tracking`, { trackingStatusId, reason });
  details = (saleId: string) => this.rest.get<OrderDetails>(`/api/user/order-form/${saleId}`);
  /** The order form PDF saved by order number: opened in a new tab (preview) or saved as {saleNumber}.pdf. */
  previewPdf = (saleId: string) => this.rest.open(`/api/user/order-form/${saleId}/pdf`);
  downloadPdf = (saleId: string, saleNumber: string) => this.rest.download(`/api/user/order-form/${saleId}/pdf?download=true`, `${saleNumber}.pdf`);
}
