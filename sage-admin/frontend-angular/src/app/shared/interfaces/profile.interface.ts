import type { AdminUserItem } from './admin-user.interface';

/** Mirrors the API's ProfileDto: the signed-in admin's own account. */
export interface Profile extends AdminUserItem {
  passwordUpdatedAtUtc: string;
  /** The last changes this admin made, newest first (from the audit log). */
  recentActivity: ProfileActivity[];
}

export interface ProfileActivity {
  id: number;
  action: string;
  entityType: string;
  entityId: string | null;
  createdAtUtc: string;
}

/** PUT /api/admin/profile: only the name and mobile number (an administrator changes the email, group and status). */
export interface ProfileInput {
  firstName: string;
  lastName: string;
  mobileNumber?: string;
}
