import { Injectable, inject } from '@angular/core';
import { AuthService } from '../auth/auth.service';

/** Permission checks for the UI (hide menus/buttons, guard routes). UX only: the API checks every request itself. */
@Injectable({ providedIn: 'root' })
export class PermissionService {
  private readonly auth = inject(AuthService);

  /** True when the signed-in user holds the permission (Super Admins hold every permission). */
  has(code: string): boolean {
    const u = this.auth.user();
    return !!u && (u.isSuperAdmin || u.permissions.includes(code));
  }

  /** True when the user holds all of the given permissions. */
  hasAll(codes: string[]): boolean {
    return codes.every((c) => this.has(c));
  }
}
