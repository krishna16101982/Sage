/** Mirrors the Admin API's MasterValueDto: one value of an order-form list. */
export interface MasterValue {
  id: number;
  masterType: string;
  code: string;
  name: string;
  sortOrder: number;
  isDefault: boolean;
  isActive: boolean;
  /** Orders that use this value. */
  usedBy: number;
  updatedAtUtc: string | null;
}

/** One order-form list (billing frequency, billing method, payment method, payment term, currency). */
export interface MasterList {
  masterType: string;
  label: string;
  values: MasterValue[];
}

export interface MasterValueInput {
  masterType: string;
  code: string;
  name: string;
  sortOrder: number;
  isDefault: boolean;
  isActive: boolean;
}

/** Order-form details on a sale (Admin sale page). */
export interface AdminOrderDetails {
  freeMonths: number;
  billingFrequency: string | null;
  billingMethod: string | null;
  paymentMethod: string | null;
  paymentTerm: string | null;
  currency: string | null;
  primaryContactName: string | null;
  primaryContactDesignation: string | null;
  primaryContactEmail: string | null;
  primaryContactPhone: string | null;
  billingContactName: string | null;
  billingContactDesignation: string | null;
  billingContactEmail: string | null;
  billingContactPhone: string | null;
  billToAddress: string | null;
  /** The order form's wording sections as saved (keys as on the User Panel's order form), or null. */
  wording: Record<string, string | null> | null;
}
