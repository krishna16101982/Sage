import { Injectable, inject } from '@angular/core';
import type { LeadSummary } from '../../shared/interfaces/lead-summary.interface';
import { GlobalRestService, toQuery } from '../rest/global-rest-service';

/** /api/admin/leads — the User Panel's leads as the Admin dashboard sees them (Sales.View). */
@Injectable({ providedIn: 'root' })
export class LeadsService {
  private readonly rest = inject(GlobalRestService);
  private readonly base = '/api/admin/leads';

  /** Dashboard figures: every lead, or one sales user's (salesUserId = their id). */
  summary = (salesUserId?: string) => this.rest.get<LeadSummary>(`${this.base}/summary${toQuery({ salesUserId })}`);
}
