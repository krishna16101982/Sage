/** Mirrors the API's GroupDto / GroupDetailDto. `id` is the group's public GUID. */
export type GroupStatus = "Active" | "Inactive";

export interface GroupItem {
  id: string;
  code: string;
  name: string;
  description: string | null;
  status: GroupStatus;
  isSystem: boolean;
  userCount: number;
  permissionCount: number;
  createdAtUtc: string;
  updatedAtUtc: string | null;
}

export interface GroupMatrixPermission {
  id: number;
  code: string;
  action: string;
  name: string;
  description: string | null;
  /** False when the permission or its module is inactive: it can be kept or removed, not newly granted. */
  isActive: boolean;
  assigned: boolean;
}

export interface GroupMatrixModule {
  id: number;
  code: string;
  name: string;
  category: string | null;
  isActive: boolean;
  permissions: GroupMatrixPermission[];
}

export interface GroupDetail extends GroupItem {
  modules: GroupMatrixModule[];
}

export interface GroupQuery {
  page?: number;
  pageSize?: number;
  search?: string;
  status?: GroupStatus;
  sortBy?: "name" | "code" | "status" | "userCount" | "createdAtUtc";
  sortDirection?: "asc" | "desc";
}
