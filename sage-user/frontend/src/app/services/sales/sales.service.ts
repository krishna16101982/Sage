import { Injectable, inject } from '@angular/core';
import type { CancelRequest, CancelRequestStatus, Sale, SaleInput, SaleQuery, SalesPage, TeamMember } from '../../shared/interfaces/sale.interface';
import { GlobalRestService, toQuery } from '../rest/global-rest-service';

/** /api/user/sales — record a sale, my / team sales, cancel. */
@Injectable({ providedIn: 'root' })
export class SalesService {
  private readonly rest = inject(GlobalRestService);
  private readonly base = '/api/user/sales';

  list = (q: SaleQuery) => this.rest.get<SalesPage>(`${this.base}${toQuery(q)}`);
  get = (id: string) => this.rest.get<Sale>(`${this.base}/${id}`);
  create = (data: SaleInput) => this.rest.post<Sale>(this.base, data);
  /** Sales managers only: cancel directly. */
  cancel = (id: string, reason: string) => this.rest.post<Sale>(`${this.base}/${id}/cancel`, { reason });
  /** Sales persons allowed by their manager: ask for the sale to be cancelled. */
  requestCancel = (id: string, reason: string) => this.rest.post<CancelRequest>(`${this.base}/${id}/cancel-request`, { reason });
  /** Managers: their team's requests. Sales persons: their own. */
  cancelRequests = (status?: CancelRequestStatus) => this.rest.get<CancelRequest[]>(`${this.base}/cancel-requests${status ? '?status=' + status : ''}`);
  approveCancel = (requestId: string, note?: string) => this.rest.post<CancelRequest>(`${this.base}/cancel-requests/${requestId}/approve`, { note });
  rejectCancel = (requestId: string, note?: string) => this.rest.post<CancelRequest>(`${this.base}/cancel-requests/${requestId}/reject`, { note });
  /** Sales managers only. */
  teamMembers = () => this.rest.get<TeamMember[]>(`${this.base}/team-members`);
  setCancelPermission = (memberId: string, canRequestCancellation: boolean) =>
    this.rest.put<TeamMember>(`${this.base}/team-members/${memberId}/cancel-permission`, { canRequestCancellation });
}
