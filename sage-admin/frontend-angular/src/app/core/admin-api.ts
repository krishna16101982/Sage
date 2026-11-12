import { Injectable, inject } from '@angular/core';
import { ApiClient, toQuery } from './api-client';
import type { PagedResult } from './models/api';
import type { AdminUserItem, AdminUserQuery, GroupOption } from './models/adminUser';
import type { GroupDetail, GroupItem, GroupQuery, GroupStatus } from './models/group';
import type { ModuleItemDto, PermissionItem, PermissionQuery } from './models/permission';
import type { DurationUnit, Product, ProductCategory, ProductPlan } from './models/product';
import type { SalesManagerOption, SalesRole, SalesUser, SalesUserQuery } from './models/salesUser';

const enc = encodeURIComponent;

/** /api/admin/users — Admin Panel accounts. */
@Injectable({ providedIn: 'root' })
export class AdminUsersApi {
  private readonly api = inject(ApiClient);
  list = (q: AdminUserQuery) => this.api.get<PagedResult<AdminUserItem>>(`/api/admin/users${toQuery(q)}`);
  groupOptions = () => this.api.get<GroupOption[]>('/api/admin/users/groups');
  create = (data: AdminUserInput & { username: string; password: string; confirmPassword: string }) => this.api.post<AdminUserItem>('/api/admin/users', data);
  update = (id: string, data: AdminUserInput) => this.api.put<AdminUserItem>(`/api/admin/users/${enc(id)}`, data);
  setStatus = (id: string, status: 'Active' | 'Inactive') => this.api.put<AdminUserItem>(`/api/admin/users/${enc(id)}/status`, { status });
  resetPassword = (id: string, newPassword: string, confirmPassword: string) =>
    this.api.post<null>(`/api/admin/users/${enc(id)}/reset-password`, { newPassword, confirmPassword });
}

export interface AdminUserInput {
  firstName: string;
  lastName: string;
  email: string;
  mobileNumber?: string;
  groupId?: string;
}

/** /api/admin/sales-users — User Panel accounts. */
@Injectable({ providedIn: 'root' })
export class SalesUsersApi {
  private readonly api = inject(ApiClient);
  list = (q: SalesUserQuery) => this.api.get<PagedResult<SalesUser>>(`/api/admin/sales-users${toQuery(q)}`);
  managers = () => this.api.get<SalesManagerOption[]>('/api/admin/sales-users/managers');
  create = (data: SalesUserInput & { username: string; password: string; confirmPassword: string }) => this.api.post<SalesUser>('/api/admin/sales-users', data);
  update = (id: string, data: SalesUserInput) => this.api.put<SalesUser>(`/api/admin/sales-users/${enc(id)}`, data);
  setStatus = (id: string, status: 'Active' | 'Inactive') => this.api.put<SalesUser>(`/api/admin/sales-users/${enc(id)}/status`, { status });
  resetPassword = (id: string, newPassword: string, confirmPassword: string) =>
    this.api.post<null>(`/api/admin/sales-users/${enc(id)}/reset-password`, { newPassword, confirmPassword });
}

export interface SalesUserInput {
  firstName: string;
  lastName: string;
  email: string;
  mobileNumber?: string;
  role: SalesRole;
  managerId?: string;
}

/** /api/admin/groups — permission groups and their permission matrix. */
@Injectable({ providedIn: 'root' })
export class GroupsApi {
  private readonly api = inject(ApiClient);
  list = (q: GroupQuery) => this.api.get<PagedResult<GroupItem>>(`/api/admin/groups${toQuery(q)}`);
  get = (id: string) => this.api.get<GroupDetail>(`/api/admin/groups/${enc(id)}`);
  create = (data: { code: string; name: string; description?: string }) => this.api.post<GroupItem>('/api/admin/groups', data);
  update = (id: string, data: { name: string; description?: string; status: GroupStatus }) => this.api.put<GroupItem>(`/api/admin/groups/${enc(id)}`, data);
  delete = (id: string) => this.api.delete<null>(`/api/admin/groups/${enc(id)}`);
  setPermissions = (id: string, permissionIds: number[]) => this.api.put<GroupDetail>(`/api/admin/groups/${enc(id)}/permissions`, { permissionIds });
}

/** /api/admin/permissions — modules and permissions. */
@Injectable({ providedIn: 'root' })
export class PermissionsApi {
  private readonly api = inject(ApiClient);
  list = (q: PermissionQuery) => this.api.get<PagedResult<PermissionItem>>(`/api/admin/permissions${toQuery(q)}`);
  modules = (includeInactive = false) => this.api.get<ModuleItemDto[]>(`/api/admin/permissions/modules?includeInactive=${includeInactive}`);
  createModule = (data: { code: string; name: string; category?: string; sortOrder: number }) => this.api.post<ModuleItemDto>('/api/admin/permissions/modules', data);
  updateModule = (id: number, data: { name: string; category?: string; sortOrder: number; isActive: boolean }) =>
    this.api.put<ModuleItemDto>(`/api/admin/permissions/modules/${id}`, data);
  createPermission = (data: { moduleId: number; action: string; name: string; description?: string }) => this.api.post<PermissionItem>('/api/admin/permissions', data);
  updatePermission = (id: number, data: { name: string; description?: string; isActive: boolean }) => this.api.put<PermissionItem>(`/api/admin/permissions/${id}`, data);
}

export interface PlanInput {
  name: string;
  description?: string;
  durationUnit: DurationUnit;
  durationCount: number;
  price: number;
  cost: number;
  sortOrder: number;
}

/** /api/admin/products — categories, products and plans (price list). */
@Injectable({ providedIn: 'root' })
export class ProductsApi {
  private readonly api = inject(ApiClient);
  catalog = (includeInactive: boolean) => this.api.get<ProductCategory[]>(`/api/admin/products/catalog?includeInactive=${includeInactive}`);
  createCategory = (data: { code: string; name: string; description?: string; sortOrder: number }) => this.api.post<ProductCategory>('/api/admin/products/categories', data);
  updateCategory = (id: number, data: { name: string; description?: string; sortOrder: number; isActive: boolean }) =>
    this.api.put<ProductCategory>(`/api/admin/products/categories/${id}`, data);
  createProduct = (data: { categoryId: number; code: string; name: string; description?: string; sortOrder: number }) => this.api.post<Product>('/api/admin/products', data);
  updateProduct = (id: number, data: { categoryId: number; name: string; description?: string; sortOrder: number; isActive: boolean }) =>
    this.api.put<Product>(`/api/admin/products/${id}`, data);
  createPlan = (productId: number, data: PlanInput) => this.api.post<ProductPlan>(`/api/admin/products/${productId}/plans`, data);
  updatePlan = (id: number, data: PlanInput & { isActive: boolean }) => this.api.put<ProductPlan>(`/api/admin/products/plans/${id}`, data);
}
