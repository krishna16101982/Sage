import { apiGet, apiPost, apiRequest } from "./apiClient";
import type { PagedResult } from "@/types/api";
import type { GroupDetail, GroupItem, GroupQuery, GroupStatus } from "@/types/group";

function toQuery(q: GroupQuery): string {
  const p = new URLSearchParams();
  Object.entries(q).forEach(([k, v]) => {
    if (v !== undefined && v !== "" && v !== null) p.set(k, String(v));
  });
  const s = p.toString();
  return s ? `?${s}` : "";
}

const put = <T,>(path: string, data: unknown) => apiRequest<T>(path, { method: "PUT", body: JSON.stringify(data) });

export const getGroups = (q: GroupQuery) => apiGet<PagedResult<GroupItem>>(`/api/admin/groups${toQuery(q)}`);

/** One group with its permission matrix. */
export const getGroup = (id: string) => apiGet<GroupDetail>(`/api/admin/groups/${encodeURIComponent(id)}`);

export const createGroup = (data: { code: string; name: string; description?: string }) => apiPost<GroupItem>("/api/admin/groups", data);

export const updateGroup = (id: string, data: { name: string; description?: string; status: GroupStatus }) =>
  put<GroupItem>(`/api/admin/groups/${encodeURIComponent(id)}`, data);

export const deleteGroup = (id: string) => apiRequest<null>(`/api/admin/groups/${encodeURIComponent(id)}`, { method: "DELETE" });

/** Replaces the group's whole permission set. */
export const setGroupPermissions = (id: string, permissionIds: number[]) =>
  put<GroupDetail>(`/api/admin/groups/${encodeURIComponent(id)}/permissions`, { permissionIds });
