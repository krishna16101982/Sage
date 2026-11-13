/** Mirrors the User API's catalog: active categories > products > plans that can be sold. Amounts are INR. */
export type DurationUnit = 'Month' | 'Quarter' | 'Year';
export const DURATION_UNITS: DurationUnit[] = ['Month', 'Quarter', 'Year'];

export interface CatalogPlan {
  id: number;
  name: string;
  description: string | null;
  durationUnit: DurationUnit;
  durationCount: number;
  /** List price per unit for one plan term. */
  price: number;
  /** Cost per unit for one plan term. */
  cost: number;
}

export interface CatalogProduct {
  id: number;
  code: string;
  name: string;
  description: string | null;
  plans: CatalogPlan[];
}

/** Company rules for recording a sale (Admin Panel > Settings). */
export interface SalesRules {
  /** Largest discount allowed, in % of the list amount (100 = no limit). */
  maxDiscountPercent: number;
  /** How many days in the past a start date may be. */
  maxBackdateDays: number;
  saleNumberPrefix: string;
}

export interface CatalogCategory {
  id: number;
  code: string;
  name: string;
  products: CatalogProduct[];
}
