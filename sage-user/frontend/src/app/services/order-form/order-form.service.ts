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
  details = (saleId: string) => this.rest.get<OrderDetails>(`/api/user/order-form/${saleId}`);
}
