/** Mirrors the API's ProductCategoryDto / ProductDto / ProductPlanDto. Amounts are INR. */
export type DurationUnit = "Month" | "Quarter" | "Year";
export const DURATION_UNITS: DurationUnit[] = ["Month", "Quarter", "Year"];

export interface ProductPlan {
  id: number;
  productId: number;
  name: string;
  description: string | null;
  durationUnit: DurationUnit;
  durationCount: number;
  price: number;
  cost: number;
  sortOrder: number;
  isActive: boolean;
}

export interface Product {
  id: number;
  categoryId: number;
  code: string;
  name: string;
  description: string | null;
  sortOrder: number;
  isActive: boolean;
  plans: ProductPlan[];
}

export interface ProductCategory {
  id: number;
  code: string;
  name: string;
  description: string | null;
  sortOrder: number;
  isActive: boolean;
  products: Product[];
}
