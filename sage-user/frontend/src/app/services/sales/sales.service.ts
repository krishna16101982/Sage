import { Injectable, inject } from '@angular/core';
import type { Sale, SaleInput, SaleQuery, SalesPage, TeamMember } from '../../shared/interfaces/sale.interface';
import { GlobalRestService, toQuery } from '../rest/global-rest-service';

/** /api/user/sales — record a sale, my / team sales, cancel. */
@Injectable({ providedIn: 'root' })
export class SalesService {
  private readonly rest = inject(GlobalRestService);
  private readonly base = '/api/user/sales';

  list = (q: SaleQuery) => this.rest.get<SalesPage>(`${this.base}${toQuery(q)}`);
  get = (id: string) => this.rest.get<Sale>(`${this.base}/${id}`);
  create = (data: SaleInput) => this.rest.post<Sale>(this.base, data);
  cancel = (id: string, reason: string) => this.rest.post<Sale>(`${this.base}/${id}/cancel`, { reason });
  /** Sales managers only. */
  teamMembers = () => this.rest.get<TeamMember[]>(`${this.base}/team-members`);
}
