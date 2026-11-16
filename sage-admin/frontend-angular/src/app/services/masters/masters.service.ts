import { Injectable, inject } from '@angular/core';
import type { MasterList, MasterValue, MasterValueInput } from '../../shared/interfaces/master.interface';
import { GlobalRestService } from '../rest/global-rest-service';

/** /api/admin/masters — the order-form lists (Setting.View to see, Setting.Edit to change). */
@Injectable({ providedIn: 'root' })
export class MastersService {
  private readonly rest = inject(GlobalRestService);
  private readonly base = '/api/admin/masters';

  lists = () => this.rest.get<MasterList[]>(this.base);
  create = (body: MasterValueInput) => this.rest.post<MasterValue>(this.base, body);
  update = (id: number, body: MasterValueInput) => this.rest.put<MasterValue>(`${this.base}/${id}`, body);
}
