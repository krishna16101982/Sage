import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Alert } from '../../component/alert/alert';
import { initials } from '../../component/icon/icon';
import { AuthService } from '../../services/auth/auth.service';
import { PermissionService } from '../../services/permission/permission.service';
import { ProfileService } from '../../services/profile/profile.service';
import { errorText } from '../../services/rest/global-rest-service';
import { Perm } from '../../shared/enumarations/permission-codes';
import type { Profile, ProfileActivity } from '../../shared/interfaces/profile.interface';
import { formatDateTime } from '../../shared/utils/format';

/** Words for audit actions, e.g. "AssignPermissions" → "Assign permissions". */
const ACTION_WORDS: Record<string, string> = {
  Create: 'Added',
  Update: 'Changed',
  Delete: 'Deleted',
  StatusChange: 'Changed the status of',
  AssignPermissions: 'Changed the permissions of',
  AssignGroup: 'Changed the group of',
  Login: 'Signed in',
  LoginFailed: 'Failed sign-in',
  Logout: 'Signed out',
  PasswordChange: 'Changed your password',
  PasswordReset: 'Reset the password of',
  PasswordResetRequested: 'Asked for a password reset',
  UpdateProfile: 'Updated your profile',
  Export: 'Exported',
};

/** "AdminUser" → "admin user", "SalesUser" → "sales user". */
const words = (code: string) => code.replace(/([a-z])([A-Z])/g, '$1 $2').toLowerCase();

/**
 * /profile — My Profile, for every signed-in admin (no permission needed): account details, own name and mobile number
 * (email, group and status are changed by an administrator), password (links to Change password), what the user can
 * do, and their recent activity.
 */
@Component({
  selector: 'app-profile',
  imports: [FormsModule, RouterLink, Alert],
  templateUrl: './profile.html',
})
export class ProfilePage implements OnInit {
  private readonly service = inject(ProfileService);
  protected readonly auth = inject(AuthService);
  protected readonly dateTime = formatDateTime;
  readonly canSeeAudit = inject(PermissionService).has(Perm.AuditLogView);

  readonly profile = signal<Profile | null>(null);
  readonly error = signal<string | null>(null);
  readonly saveError = signal<string | null>(null);
  readonly notice = signal<string | null>(null);
  readonly busy = signal(false);

  firstName = '';
  lastName = '';
  mobileNumber = '';

  readonly initials = computed(() => initials(this.profile()?.firstName, this.profile()?.lastName));

  /** The user's permissions grouped by module, e.g. Customers: View, Add, Edit. */
  readonly access = computed(() => {
    const u = this.auth.user();
    if (!u || u.isSuperAdmin) return [];
    const names = new Map(u.modules.map((m) => [m.code, m.name]));
    const byModule = new Map<string, string[]>();
    for (const p of u.permissions) {
      const dot = p.lastIndexOf('.');
      const module = dot > 0 ? p.slice(0, dot) : p;
      const action = dot > 0 ? p.slice(dot + 1) : '';
      byModule.set(module, [...(byModule.get(module) ?? []), action]);
    }
    return [...byModule.entries()]
      .map(([code, actions]) => ({ name: names.get(code) ?? code, actions: actions.filter(Boolean).join(', ') }))
      .sort((a, b) => a.name.localeCompare(b.name));
  });

  /** True when the form differs from what is saved (the form fields are plain properties, so this is a method). */
  changed(): boolean {
    const p = this.profile();
    return !!p && (this.firstName.trim() !== p.firstName || this.lastName.trim() !== p.lastName || this.mobileNumber.trim() !== (p.mobileNumber ?? ''));
  }

  async ngOnInit(): Promise<void> {
    try {
      this.show(await this.service.get());
    } catch (err) {
      this.error.set(errorText(err, 'Unable to load your profile.'));
    }
  }

  private show(p: Profile): void {
    this.profile.set(p);
    this.firstName = p.firstName;
    this.lastName = p.lastName;
    this.mobileNumber = p.mobileNumber ?? '';
  }

  /** What one audit entry means, in words. */
  describe(a: ProfileActivity): string {
    const verb = ACTION_WORDS[a.action] ?? words(a.action);
    // Actions about the user's own session or account read better without the object.
    if (['Login', 'LoginFailed', 'Logout', 'PasswordChange', 'PasswordResetRequested', 'UpdateProfile'].includes(a.action)) return verb;
    const what = a.action === 'Export' ? 'sales' : words(a.entityType);
    return `${verb} ${what}${a.entityId && a.action !== 'Export' && a.entityType === 'Setting' ? ' ' + a.entityId : ''}`;
  }

  async save(): Promise<void> {
    this.saveError.set(null);
    this.notice.set(null);
    if (!this.firstName.trim() || !this.lastName.trim()) {
      this.saveError.set('Enter your first and last name.');
      return;
    }
    this.busy.set(true);
    try {
      const saved = await this.service.update({
        firstName: this.firstName.trim(),
        lastName: this.lastName.trim(),
        mobileNumber: this.mobileNumber.trim() || undefined,
      });
      this.show(saved);
      this.notice.set('Your profile has been saved.');
      await this.auth.reloadUser();   // the name in the top bar and the sidebar
    } catch (err) {
      this.saveError.set(errorText(err));
    } finally {
      this.busy.set(false);
    }
  }
}
