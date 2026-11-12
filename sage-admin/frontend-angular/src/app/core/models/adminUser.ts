/** Mirrors the API's AdminUserDto / GroupOption. `id` is the user's public GUID. */
export type UserStatus = "Active" | "Inactive" | "Locked";

export interface AdminUserItem {
  id: string;
  firstName: string;
  lastName: string;
  username: string;
  email: string;
  mobileNumber: string | null;
  status: UserStatus;
  isSuperAdmin: boolean;
  groupId: string | null;
  groupName: string | null;
  groupIsActive: boolean;
  mustChangePassword: boolean;
  isLockedOut: boolean;
  lastLoginAtUtc: string | null;
  createdAtUtc: string;
}

export interface GroupOption {
  id: string;
  name: string;
  code: string;
}

export interface AdminUserQuery {
  page?: number;
  pageSize?: number;
  search?: string;
  status?: UserStatus;
  groupId?: string;
  sortBy?: "name" | "username" | "email" | "status" | "group" | "createdAtUtc" | "lastLoginAtUtc";
  sortDirection?: "asc" | "desc";
}
