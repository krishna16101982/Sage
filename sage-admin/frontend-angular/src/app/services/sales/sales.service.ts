import { Injectable, inject } from '@angular/core';
import type { Sale, SaleQuery, SalesPage, SalesSummaryRow, SellerOption } from '../../shared/interfaces/sale.interface';
import { GlobalRestService, toQuery } from '../rest/global-rest-service';

/** /api/admin/sales — sales recorded in the User Panel (read-only): list, summary per person / manager, detail, CSV exports. */
@Injectable({ providedIn: 'root' })
export class SalesService {
  private readonly rest = inject(GlobalRestService);
  private readonly base = '/api/admin/sales';

  list = (q: SaleQuery) => this.rest.get<SalesPage>(`${this.base}${toQuery(q)}`);
  get = (id: string) => this.rest.get<Sale>(`${this.base}/${id}`);
  summary = (q: { groupBy: 'seller' | 'manager'; soldFrom?: string; soldTo?: string }) =>
    this.rest.get<SalesSummaryRow[]>(`${this.base}/summary${toQuery(q)}`);
  sellers = () => this.rest.get<SellerOption[]>(`${this.base}/sellers`);
  /** Every sale matching the filters (no paging) as a CSV file; the server refuses more than 50,000. */
  exportList = (q: Omit<SaleQuery, 'page' | 'pageSize'>) => this.rest.download(`${this.base}/export${toQuery(q)}`, 'sales.csv');
  exportSummary = (q: { groupBy: 'seller' | 'manager'; soldFrom?: string; soldTo?: string }) =>
    this.rest.download(`${this.base}/summary/export${toQuery(q)}`, `sales-by-${q.groupBy === 'manager' ? 'manager' : 'person'}.csv`);
}
