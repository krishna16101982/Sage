import { apiGet, apiPost, apiRequest } from "./apiClient";
import type { PagedResult } from "@/types/api";
import type { ModuleItemDto, PermissionItem, PermissionQuery } from "@/types/permission";

function toQuery(q: PermissionQuery): string {
  const p = new URLSearchParams();
  Object.entries(q).forEach(([k, v]) => {
    if (v !== undefined && v !== "" && v !== null) p.set(k, String(v));
  });
  const s = p.toString();
  return s ? `?${s}` : "";
}

const put = <T,>(path: string, data: unknown) => apiRequest<T>(path, { method: "PUT", body: JSON.stringify(data) });

export const getPermissions = (q: PermissionQuery) => apiGet<PagedResult<PermissionItem>>(`/api/admin/permissions${toQuery(q)}`);

export const getModules = (includeInactive = false) =>
  apiGet<ModuleItemDto[]>(`/api/admin/permissions/modules?includeInactive=${includeInactive}`);

export const createModule = (data: { code: string; name: string; category?: string; sortOrder: number }) =>
  apiPost<ModuleItemDto>("/api/admin/permissions/modules", data);

export const updateModule = (id: number, data: { name: string; category?: string; sortOrder: number; isActive: boolean }) =>
  put<ModuleItemDto>(`/api/admin/permissions/modules/${id}`, data);

export const createPermission = (data: { moduleId: number; action: string; name: string; description?: string }) =>
  apiPost<PermissionItem>("/api/admin/permissions", data);

export const updatePermission = (id: number, data: { name: string; description?: string; isActive: boolean }) =>
  put<PermissionItem>(`/api/admin/permissions/${id}`, data);
