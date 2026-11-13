import { Injectable, inject } from '@angular/core';
import { GlobalRestService, toQuery } from '../rest/global-rest-service';
import type { PagedResult } from '../../shared/interfaces/api.interface';
import type { ModuleItemDto, PermissionItem, PermissionQuery } from '../../shared/interfaces/permission.interface';

/** /api/admin/permissions — permission modules and the permissions inside them. */
@Injectable({ providedIn: 'root' })
export class PermissionsService {
  private readonly rest = inject(GlobalRestService);
  private readonly base = '/api/admin/permissions';

  list = (q: PermissionQuery) => this.rest.get<PagedResult<PermissionItem>>(`${this.base}${toQuery(q)}`);
  /** Modules with their permissions (includeInactive for management screens). */
  modules = (includeInactive = false) => this.rest.get<ModuleItemDto[]>(`${this.base}/modules?includeInactive=${includeInactive}`);
  createModule = (data: { code: string; name: string; category?: string; sortOrder: number; menuPath?: string }) => this.rest.post<ModuleItemDto>(`${this.base}/modules`, data);
  updateModule = (id: number, data: { name: string; category?: string; sortOrder: number; isActive: boolean; menuPath: string }) =>
    this.rest.put<ModuleItemDto>(`${this.base}/modules/${id}`, data);
  createPermission = (data: { moduleId: number; action: string; name: string; description?: string }) => this.rest.post<PermissionItem>(this.base, data);
  updatePermission = (id: number, data: { name: string; description?: string; isActive: boolean }) => this.rest.put<PermissionItem>(`${this.base}/${id}`, data);

  /** There is no single-item endpoint: find a module or permission in the full module list. */
  async findModule(id: number): Promise<ModuleItemDto | undefined> {
    return (await this.modules(true)).find((m) => m.id === id);
  }

  async findPermission(id: number): Promise<PermissionItem | undefined> {
    return (await this.modules(true)).flatMap((m) => m.permissions).find((p) => p.id === id);
  }
}
