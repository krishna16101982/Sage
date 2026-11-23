/** Mirrors the User API's LeadDto (Lead Records). can* say what the signed-in user may do with the lead. */
export interface Lead {
  id: string;
  leadNumber: string;
  companyId: string;
  companyName: string;
  clientName: string | null;
  clientDesignation: string | null;
  clientEmail: string | null;
  clientPhone: string | null;
  secondaryClientName: string | null;
  secondaryClientDesignation: string | null;
  secondaryClientEmail: string | null;
  secondaryClientPhone: string | null;
  productId: number | null;
  productName: string | null;
  sourceId: number | null;
  sourceName: string | null;
  requirement: string | null;
  statusId: number;
  /** OPEN, WARM, WON, LOST, HOLD or DISQUALIFIED. */
  statusCode: string;
  statusName: string;
  typeId: number;
  typeName: string;
  generatedById: string;
  generatedByName: string;
  assignedId: string;
  assignedName: string;
  monthlyExpectedRevenue: number | null;
  billingCycleId: number | null;
  billingCycleName: string | null;
  nextFollowUpDate: string | null;
  /** "Created Date" on the form (yyyy-MM-dd); aging counts from it. */
  leadDate: string;
  closingDate: string | null;
  remarks: string | null;
  agingDays: number;
  /** The order made from this lead (New Order), if any. */
  saleId: string | null;
  saleNumber: string | null;
  createdByName: string;
  createdAtUtc: string;
  updatedAtUtc: string | null;
  canEdit: boolean;
  canDelete: boolean;
  canOrder: boolean;
}

/** POST / PUT /api/user/leads. customerId is only read when the lead is added. */
export interface LeadInput {
  customerId?: string;
  clientName?: string;
  clientDesignation?: string;
  clientEmail?: string;
  clientPhone?: string;
  secondaryClientName?: string;
  secondaryClientDesignation?: string;
  secondaryClientEmail?: string;
  secondaryClientPhone?: string;
  productId?: number | null;
  sourceId?: number | null;
  requirement?: string;
  statusId: number;
  typeId: number;
  generatedById: string;
  assignedId: string;
  monthlyExpectedRevenue?: number | null;
  billingCycleId?: number | null;
  nextFollowUpDate?: string | null;
  leadDate: string;
  closingDate?: string | null;
  remarks?: string;
}

export type LeadAging = 'lt7' | '7to30' | '31to60' | 'gt60';

/** GET /api/user/leads query (paged by the database). */
export interface LeadQuery {
  search?: string;
  statusId?: number;
  productId?: number;
  assignedId?: string;
  generatedById?: string;
  aging?: LeadAging;
  from?: string;
  to?: string;
  /** Only leads no order was made from yet (the order form's lead picker). */
  withoutOrder?: boolean;
  sortBy?: 'leadNumber' | 'leadDate' | 'company' | 'revenue' | 'aging';
  sortDirection?: 'asc' | 'desc';
  page?: number;
  pageSize?: number;
}

export interface LeadListOption {
  id: number;
  name: string;
}

export interface LeadFixedOption {
  id: number;
  code: string;
  name: string;
}

export interface SalesUserOption {
  id: string;
  name: string;
  role: 'SalesPerson' | 'SalesManager';
  /** Only active people can be chosen on a lead; inactive ones stay in the filters. */
  isActive: boolean;
}

/** One figure of the lead dashboard (a status, source, aging bucket, lead type or product). */
export interface LeadSummaryItem {
  id: number;
  code: string | null;
  name: string;
  /** Leads. */
  total: number;
  /** Won leads. */
  won: number;
  /** Monthly expected revenue of the won leads (INR). */
  revenue: number;
}

/** GET /api/user/leads/summary — the Home dashboard. */
export interface LeadSummary {
  totalLeads: number;
  wonLeads: number;
  /** Won ÷ total, in %. */
  conversionRate: number;
  wonRevenueMonthly: number;
  wonRevenueAnnualized: number;
  statuses: LeadSummaryItem[];
  sources: LeadSummaryItem[];
  aging: LeadSummaryItem[];
  types: LeadSummaryItem[];
  products: LeadSummaryItem[];
}

/** GET /api/user/leads/options */
export interface LeadOptions {
  products: LeadListOption[];
  sources: LeadListOption[];
  billingCycles: LeadListOption[];
  statuses: LeadFixedOption[];
  types: LeadFixedOption[];
  salesUsers: SalesUserOption[];
}
