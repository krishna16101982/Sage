import { Injectable, inject } from '@angular/core';
import type { OrderDetails, OrderFormOptions, OrderInput, OrderPreviewInput } from '../../shared/interfaces/order-form.interface';
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
  /** The saved order as a Word file ({saleNumber}.docx), to change in Word. */
  downloadWord = (saleId: string, saleNumber: string) => this.rest.download(`/api/user/order-form/${saleId}/word`, `${saleNumber}.docx`);
  /** The form as filled in, not saved: Preview PDF (new tab), Download PDF or Download Word. */
  previewDraft = (body: OrderPreviewInput) => this.rest.open('/api/user/order-form/preview?format=pdf', body);
  downloadDraft = (body: OrderPreviewInput, format: 'pdf' | 'word') =>
    this.rest.download(`/api/user/order-form/preview?format=${format}&download=true`, `OrderForm.${format === 'word' ? 'docx' : 'pdf'}`, body);
}
