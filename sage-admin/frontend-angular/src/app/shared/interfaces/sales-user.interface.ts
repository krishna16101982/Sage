/** Mirrors the API's SalesUserDto / SalesManagerOption. `id` is the user's public GUID. */
export type SalesRole = "SalesPerson" | "SalesManager";
export type SalesUserStatus = "Active" | "Inactive" | "Locked";

export const SALES_ROLE_LABEL: Record<SalesRole, string> = { SalesPerson: "Sales person", SalesManager: "Sales manager" };

export interface SalesUser {
  id: string;
  firstName: string;
  lastName: string;
  username: string;
  email: string;
  mobileNumber: string | null;
  role: SalesRole;
  status: SalesUserStatus;
  managerId: string | null;
  managerName: string | null;
  teamSize: number;
  mustChangePassword: boolean;
  isLockedOut: boolean;
  lastLoginAtUtc: string | null;
  createdAtUtc: string;
}

export interface SalesManagerOption {
  id: string;
  name: string;
  username: string;
  teamSize: number;
}

export interface SalesUserQuery {
  page?: number;
  pageSize?: number;
  search?: string;
  role?: SalesRole;
  status?: SalesUserStatus;
  managerId?: string;
  sortBy?: "name" | "username" | "email" | "role" | "status" | "manager" | "createdAtUtc" | "lastLoginAtUtc";
  sortDirection?: "asc" | "desc";
}

/** One change to a sales user (GET /api/admin/sales-users/history). */
export interface SalesUserHistoryEntry {
  atUtc: string;
  salesUserId: string | null;
  salesUserName: string | null;
  username: string | null;
  /** Created, Status, Manager, Role, Details, Cancel requests. */
  change: string;
  /** e.g. "Active → Inactive", "Kumar Gaurav → Asha Rao". */
  details: string;
  byName: string | null;
  /** "Admin Panel" or "User Panel". */
  byPanel: string;
}
