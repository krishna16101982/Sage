import { apiGet, apiPost, apiRequest } from "./apiClient";
import type { DurationUnit, Product, ProductCategory, ProductPlan } from "@/types/product";

const put = <T,>(path: string, data: unknown) => apiRequest<T>(path, { method: "PUT", body: JSON.stringify(data) });

export interface PlanInput {
  name: string;
  description?: string;
  durationUnit: DurationUnit;
  durationCount: number;
  price: number;
  cost: number;
  sortOrder: number;
}

/** Categories -> products -> plans. includeInactive for the management screen. */
export const getCatalog = (includeInactive: boolean) => apiGet<ProductCategory[]>(`/api/admin/products/catalog?includeInactive=${includeInactive}`);

export const createCategory = (data: { code: string; name: string; description?: string; sortOrder: number }) =>
  apiPost<ProductCategory>("/api/admin/products/categories", data);

export const updateCategory = (id: number, data: { name: string; description?: string; sortOrder: number; isActive: boolean }) =>
  put<ProductCategory>(`/api/admin/products/categories/${id}`, data);

export const createProduct = (data: { categoryId: number; code: string; name: string; description?: string; sortOrder: number }) =>
  apiPost<Product>("/api/admin/products", data);

export const updateProduct = (id: number, data: { categoryId: number; name: string; description?: string; sortOrder: number; isActive: boolean }) =>
  put<Product>(`/api/admin/products/${id}`, data);

export const createPlan = (productId: number, data: PlanInput) => apiPost<ProductPlan>(`/api/admin/products/${productId}/plans`, data);

export const updatePlan = (id: number, data: PlanInput & { isActive: boolean }) => put<ProductPlan>(`/api/admin/products/plans/${id}`, data);
