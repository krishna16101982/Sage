import { Injectable, inject } from '@angular/core';
import { GlobalRestService, toQuery } from '../rest/global-rest-service';
import type { PagedResult } from '../../shared/interfaces/api.interface';
import type { GroupDetail, GroupItem, GroupQuery, GroupStatus } from '../../shared/interfaces/group.interface';

/** /api/admin/groups — permission groups and their permission matrix. */
@Injectable({ providedIn: 'root' })
export class GroupsService {
  private readonly rest = inject(GlobalRestService);
  private readonly base = '/api/admin/groups';

  list = (q: GroupQuery) => this.rest.get<PagedResult<GroupItem>>(`${this.base}${toQuery(q)}`);
  get = (id: string) => this.rest.get<GroupDetail>(`${this.base}/${encodeURIComponent(id)}`);
  create = (data: { code: string; name: string; description?: string }) => this.rest.post<GroupItem>(this.base, data);
  update = (id: string, data: { name: string; description?: string; status: GroupStatus }) => this.rest.put<GroupItem>(`${this.base}/${encodeURIComponent(id)}`, data);
  delete = (id: string) => this.rest.delete<null>(`${this.base}/${encodeURIComponent(id)}`);
  setPermissions = (id: string, permissionIds: number[]) => this.rest.put<GroupDetail>(`${this.base}/${encodeURIComponent(id)}/permissions`, { permissionIds });
}
