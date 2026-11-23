import { Injectable, inject } from '@angular/core';
import type { PagedResult } from '../../shared/interfaces/api.interface';
import type { Lead, LeadInput, LeadOptions, LeadQuery, LeadSummary } from '../../shared/interfaces/lead.interface';
import { GlobalRestService, toQuery } from '../rest/global-rest-service';

/** /api/user/leads — Lead Records (paged by the database: one page per call). */
@Injectable({ providedIn: 'root' })
export class LeadsService {
  private readonly rest = inject(GlobalRestService);
  private readonly base = '/api/user/leads';

  list = (q: LeadQuery) => this.rest.get<PagedResult<Lead>>(`${this.base}${toQuery(q)}`);
  get = (id: string) => this.rest.get<Lead>(`${this.base}/${id}`);
  options = () => this.rest.get<LeadOptions>(`${this.base}/options`);
  /** Home dashboard figures: every lead, or only the user's (created, generated or assigned). */
  summary = (mine: boolean) => this.rest.get<LeadSummary>(`${this.base}/summary?mine=${mine}`);
  create = (body: LeadInput) => this.rest.post<Lead>(this.base, body);
  update = (id: string, body: LeadInput) => this.rest.put<Lead>(`${this.base}/${id}`, body);
  remove = (id: string) => this.rest.delete<void>(`${this.base}/${id}`);
}
