import { Injectable, inject } from '@angular/core';
import { GlobalRestService, toQuery } from '../rest/global-rest-service';
import type { PagedResult } from '../../shared/interfaces/api.interface';
import type { AdminUserItem, AdminUserQuery, GroupOption } from '../../shared/interfaces/admin-user.interface';

export interface AdminUserInput {
  firstName: string;
  lastName: string;
  email: string;
  mobileNumber?: string;
  groupId?: string;
}

/** /api/admin/users — Admin Panel accounts. */
@Injectable({ providedIn: 'root' })
export class AdminUsersService {
  private readonly rest = inject(GlobalRestService);
  private readonly base = '/api/admin/users';

  list = (q: AdminUserQuery) => this.rest.get<PagedResult<AdminUserItem>>(`${this.base}${toQuery(q)}`);
  get = (id: string) => this.rest.get<AdminUserItem>(`${this.base}/${encodeURIComponent(id)}`);
  groupOptions = () => this.rest.get<GroupOption[]>(`${this.base}/groups`);
  create = (data: AdminUserInput & { username: string; password: string; confirmPassword: string }) => this.rest.post<AdminUserItem>(this.base, data);
  update = (id: string, data: AdminUserInput) => this.rest.put<AdminUserItem>(`${this.base}/${encodeURIComponent(id)}`, data);
  setStatus = (id: string, status: 'Active' | 'Inactive') => this.rest.put<AdminUserItem>(`${this.base}/${encodeURIComponent(id)}/status`, { status });
  resetPassword = (id: string, newPassword: string, confirmPassword: string) =>
    this.rest.post<null>(`${this.base}/${encodeURIComponent(id)}/reset-password`, { newPassword, confirmPassword });
}
