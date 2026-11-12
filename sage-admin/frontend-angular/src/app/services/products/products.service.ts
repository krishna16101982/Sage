import { Injectable, inject } from '@angular/core';
import { GlobalRestService } from '../rest/global-rest-service';
import type { DurationUnit, Product, ProductCategory, ProductPlan } from '../../shared/interfaces/product.interface';

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
export class ProductsService {
  private readonly rest = inject(GlobalRestService);
  private readonly base = '/api/admin/products';

  catalog = (includeInactive: boolean) => this.rest.get<ProductCategory[]>(`${this.base}/catalog?includeInactive=${includeInactive}`);
  createCategory = (data: { code: string; name: string; description?: string; sortOrder: number }) => this.rest.post<ProductCategory>(`${this.base}/categories`, data);
  updateCategory = (id: number, data: { name: string; description?: string; sortOrder: number; isActive: boolean }) =>
    this.rest.put<ProductCategory>(`${this.base}/categories/${id}`, data);
  createProduct = (data: { categoryId: number; code: string; name: string; description?: string; sortOrder: number }) => this.rest.post<Product>(this.base, data);
  updateProduct = (id: number, data: { categoryId: number; name: string; description?: string; sortOrder: number; isActive: boolean }) =>
    this.rest.put<Product>(`${this.base}/${id}`, data);
  createPlan = (productId: number, data: PlanInput) => this.rest.post<ProductPlan>(`${this.base}/${productId}/plans`, data);
  updatePlan = (id: number, data: PlanInput & { isActive: boolean }) => this.rest.put<ProductPlan>(`${this.base}/plans/${id}`, data);

  /** There are no single-item endpoints: find items in the full catalog (it is small). */
  async findCategory(id: number): Promise<ProductCategory | undefined> {
    return (await this.catalog(true)).find((c) => c.id === id);
  }

  async findProduct(id: number): Promise<Product | undefined> {
    return (await this.catalog(true)).flatMap((c) => c.products).find((p) => p.id === id);
  }

  async findPlan(id: number): Promise<{ product: Product; plan: ProductPlan } | undefined> {
    for (const product of (await this.catalog(true)).flatMap((c) => c.products)) {
      const plan = product.plans.find((p) => p.id === id);
      if (plan) return { product, plan };
    }
    return undefined;
  }
}
