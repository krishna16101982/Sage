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
  /** Priced by seat range per financial year (then price/cost above are not used on the order form). */
  hasSeatPrices: boolean;
  /** The financial year seatPrices are for (2027 = FY27 = Apr 2026 to Mar 2027); null when the plan has no seat prices. */
  priceFiscalYear: number | null;
  /** This financial year's seat ranges (empty when none are set for this year yet). */
  seatPrices: CatalogSeatPrice[];
}

/** One seat range: every seat of an order in this range costs `price` (per seat, one plan term). */
export interface CatalogSeatPrice {
  minSeats: number;
  /** null = "and above". */
  maxSeats: number | null;
  price: number;
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
