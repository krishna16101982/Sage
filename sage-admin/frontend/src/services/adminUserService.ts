import { apiGet, apiPost, apiRequest } from "./apiClient";
import type { PagedResult } from "@/types/api";
import type { AdminUserItem, AdminUserQuery, GroupOption } from "@/types/adminUser";

function toQuery(q: AdminUserQuery): string {
  const p = new URLSearchParams();
  Object.entries(q).forEach(([k, v]) => {
    if (v !== undefined && v !== "" && v !== null) p.set(k, String(v));
  });
  const s = p.toString();
  return s ? `?${s}` : "";
}

const put = <T,>(path: string, data: unknown) => apiRequest<T>(path, { method: "PUT", body: JSON.stringify(data) });
const base = "/api/admin/users";
const at = (id: string) => `${base}/${encodeURIComponent(id)}`;

export interface AdminUserInput {
  firstName: string;
  lastName: string;
  email: string;
  mobileNumber?: string;
  groupId?: string;
}

export const getAdminUsers = (q: AdminUserQuery) => apiGet<PagedResult<AdminUserItem>>(`${base}${toQuery(q)}`);

/** Active permission groups, for the group drop-down. */
export const getGroupOptions = () => apiGet<GroupOption[]>(`${base}/groups`);

export const createAdminUser = (data: AdminUserInput & { username: string; password: string; confirmPassword: string }) => apiPost<AdminUserItem>(base, data);

export const updateAdminUser = (id: string, data: AdminUserInput) => put<AdminUserItem>(at(id), data);

export const setAdminUserStatus = (id: string, status: "Active" | "Inactive") => put<AdminUserItem>(`${at(id)}/status`, { status });

export const resetAdminUserPassword = (id: string, newPassword: string, confirmPassword: string) =>
  apiPost<null>(`${at(id)}/reset-password`, { newPassword, confirmPassword });
