import { apiGet, apiPost, apiRequest } from "./apiClient";
import type { PagedResult } from "@/types/api";
import type { SalesManagerOption, SalesRole, SalesUser, SalesUserQuery } from "@/types/salesUser";

function toQuery(q: SalesUserQuery): string {
  const p = new URLSearchParams();
  Object.entries(q).forEach(([k, v]) => {
    if (v !== undefined && v !== "" && v !== null) p.set(k, String(v));
  });
  const s = p.toString();
  return s ? `?${s}` : "";
}

const put = <T,>(path: string, data: unknown) => apiRequest<T>(path, { method: "PUT", body: JSON.stringify(data) });
const base = "/api/admin/sales-users";
const at = (id: string) => `${base}/${encodeURIComponent(id)}`;

export interface SalesUserInput {
  firstName: string;
  lastName: string;
  email: string;
  mobileNumber?: string;
  role: SalesRole;
  managerId?: string;
}

export const getSalesUsers = (q: SalesUserQuery) => apiGet<PagedResult<SalesUser>>(`${base}${toQuery(q)}`);

/** Active sales managers, for the manager drop-down. */
export const getSalesManagers = () => apiGet<SalesManagerOption[]>(`${base}/managers`);

export const createSalesUser = (data: SalesUserInput & { username: string; password: string; confirmPassword: string }) => apiPost<SalesUser>(base, data);

export const updateSalesUser = (id: string, data: SalesUserInput) => put<SalesUser>(at(id), data);

export const setSalesUserStatus = (id: string, status: "Active" | "Inactive") => put<SalesUser>(`${at(id)}/status`, { status });

export const resetSalesUserPassword = (id: string, newPassword: string, confirmPassword: string) =>
  apiPost<null>(`${at(id)}/reset-password`, { newPassword, confirmPassword });
