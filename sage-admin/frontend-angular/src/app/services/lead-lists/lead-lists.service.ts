import { Injectable, inject } from '@angular/core';
import type { LeadList, LeadListValue, LeadListValueInput } from '../../shared/interfaces/lead-list.interface';
import { GlobalRestService } from '../rest/global-rest-service';

/** /api/admin/lead-lists — products, lead sources and billing cycles of the User Panel's lead form (Setting.View / Setting.Edit). */
@Injectable({ providedIn: 'root' })
export class LeadListsService {
  private readonly rest = inject(GlobalRestService);
  private readonly base = '/api/admin/lead-lists';

  lists = () => this.rest.get<LeadList[]>(this.base);
  create = (body: LeadListValueInput) => this.rest.post<LeadListValue>(this.base, body);
  update = (id: number, body: LeadListValueInput) => this.rest.put<LeadListValue>(`${this.base}/${id}`, body);
}
