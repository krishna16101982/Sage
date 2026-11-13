import { Injectable, inject } from '@angular/core';
import type { PagedResult } from '../../shared/interfaces/api.interface';
import type { AuditLogDetail, AuditLogItem, AuditLogOptions, AuditLogQuery } from '../../shared/interfaces/audit-setting.interface';
import { GlobalRestService, toQuery } from '../rest/global-rest-service';

/** /api/admin/audit-logs — the audit log (read-only). */
@Injectable({ providedIn: 'root' })
export class AuditLogsService {
  private readonly rest = inject(GlobalRestService);
  private readonly base = '/api/admin/audit-logs';

  list = (q: AuditLogQuery) => this.rest.get<PagedResult<AuditLogItem>>(`${this.base}${toQuery(q)}`);
  get = (id: number) => this.rest.get<AuditLogDetail>(`${this.base}/${id}`);
  options = () => this.rest.get<AuditLogOptions>(`${this.base}/options`);
}
