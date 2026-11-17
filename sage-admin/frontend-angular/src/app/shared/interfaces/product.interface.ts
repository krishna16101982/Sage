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
  /** Optional item that comes with the plan, printed on the order form (e.g. "Sales Navigator Team Link Extend Seat"). */
  includedItemTitle: string | null;
  /** May use {paidMonths}, {freeMonths}, {totalMonths} and {seats}. */
  includedItemDescription: string | null;
  /** Standard quantity of the included item on an order (e.g. 1000). */
  includedItemQuantity: number | null;
  /** Priced by seat range per financial year; price/cost above are then only a fallback. */
  hasSeatPrices: boolean;
}

/** One seat range: every seat of an order in this range costs `price` (per seat, one plan term). */
export interface SeatPriceRange {
  minSeats: number;
  /** null = "and above". */
  maxSeats: number | null;
  price: number;
  /** null = the plan's own cost. */
  cost: number | null;
}

/** A plan's seat ranges for one Indian financial year: 2027 = FY27 = Apr 2026 to Mar 2027. */
export interface PlanSeatPriceYear {
  fiscalYear: number;
  ranges: SeatPriceRange[];
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
