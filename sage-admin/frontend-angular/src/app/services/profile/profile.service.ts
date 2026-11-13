import { Injectable, inject } from '@angular/core';
import type { Profile, ProfileInput } from '../../shared/interfaces/profile.interface';
import { GlobalRestService } from '../rest/global-rest-service';

/** /api/admin/profile — the signed-in admin's own account (no permission needed). */
@Injectable({ providedIn: 'root' })
export class ProfileService {
  private readonly rest = inject(GlobalRestService);

  get = () => this.rest.get<Profile>('/api/admin/profile');
  update = (body: ProfileInput) => this.rest.put<Profile>('/api/admin/profile', body);
}
