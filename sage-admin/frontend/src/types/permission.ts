/** Mirrors the API's PermissionDto / ModuleDto. */
export interface PermissionItem {
  id: number;
  moduleId: number;
  moduleCode: string;
  moduleName: string;
  action: string;
  code: string; // Module.Action
  name: string;
  description: string | null;
  isActive: boolean;
  isSystem: boolean;
}

export interface ModuleItemDto {
  id: number;
  code: string;
  name: string;
  category: string | null;
  sortOrder: number;
  isActive: boolean;
  isSystem: boolean;
  permissionCount: number;
  permissions: PermissionItem[];
}

export interface PermissionQuery {
  page?: number;
  pageSize?: number;
  search?: string;
  moduleId?: number;
  isActive?: boolean;
  sortBy?: string;
  sortDirection?: "asc" | "desc";
}
