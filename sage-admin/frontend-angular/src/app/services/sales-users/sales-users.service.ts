import { Injectable, inject } from '@angular/core';
import { GlobalRestService, toQuery } from '../rest/global-rest-service';
import type { PagedResult } from '../../shared/interfaces/api.interface';
import type { SalesManagerOption, SalesRole, SalesUser, SalesUserQuery, SalesUserHistoryEntry } from '../../shared/interfaces/sales-user.interface';

export interface SalesUserInput {
  firstName: string;
  lastName: string;
  email: string;
  mobileNumber?: string;
  role: SalesRole;
  managerId?: string;
}

/** /api/admin/sales-users — User Panel accounts (sales persons and managers). */
@Injectable({ providedIn: 'root' })
export class SalesUsersService {
  private readonly rest = inject(GlobalRestService);
  private readonly base = '/api/admin/sales-users';

  list = (q: SalesUserQuery) => this.rest.get<PagedResult<SalesUser>>(`${this.base}${toQuery(q)}`);
  get = (id: string) => this.rest.get<SalesUser>(`${this.base}/${encodeURIComponent(id)}`);
  managers = () => this.rest.get<SalesManagerOption[]>(`${this.base}/managers`);
  /** Status, manager and role changes (Admin Panel and managers in the User Panel): one person, or everyone. */
  history = (salesUserId?: string | null) => this.rest.get<SalesUserHistoryEntry[]>(`${this.base}/history${toQuery({ salesUserId })}`);
  create = (data: SalesUserInput & { username: string; password: string; confirmPassword: string }) => this.rest.post<SalesUser>(this.base, data);
  update = (id: string, data: SalesUserInput) => this.rest.put<SalesUser>(`${this.base}/${encodeURIComponent(id)}`, data);
  setStatus = (id: string, status: 'Active' | 'Inactive') => this.rest.put<SalesUser>(`${this.base}/${encodeURIComponent(id)}/status`, { status });
  resetPassword = (id: string, newPassword: string, confirmPassword: string) =>
    this.rest.post<null>(`${this.base}/${encodeURIComponent(id)}/reset-password`, { newPassword, confirmPassword });
}
