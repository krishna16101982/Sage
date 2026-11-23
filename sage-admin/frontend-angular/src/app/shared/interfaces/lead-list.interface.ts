/** Mirrors the Admin API's LeadListValueDto: one value of a lead list (product, lead source, billing cycle). */
export interface LeadListValue {
  id: number;
  /** Product, Source or BillingCycle. */
  list: string;
  name: string;
  sortOrder: number;
  isActive: boolean;
  /** Leads that use this value. */
  usedBy: number;
  updatedAtUtc: string | null;
}

export interface LeadList {
  list: string;
  label: string;
  values: LeadListValue[];
}

export interface LeadListValueInput {
  list: string;
  name: string;
  sortOrder: number;
  isActive: boolean;
}
