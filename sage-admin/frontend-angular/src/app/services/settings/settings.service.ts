import { Injectable, inject } from '@angular/core';
import type { Setting } from '../../shared/interfaces/audit-setting.interface';
import { GlobalRestService } from '../rest/global-rest-service';

/** /api/admin/settings — application settings. */
@Injectable({ providedIn: 'root' })
export class SettingsService {
  private readonly rest = inject(GlobalRestService);
  private readonly base = '/api/admin/settings';

  list = () => this.rest.get<Setting[]>(this.base);
  update = (key: string, value: string) => this.rest.put<Setting>(`${this.base}/${encodeURIComponent(key)}`, { value });
}
