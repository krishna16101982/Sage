import type { PagedResult } from './api.interface';

export type SaleStatus = 'Active' | 'Cancelled';

/** Mirrors the Admin API's AdminSaleDto. Amounts are INR; dates are 'yyyy-MM-dd'. Sales are recorded in the User Panel. */
export interface Sale {
  id: string;
  saleNumber: string;
  customerId: string;
  customerName: string;
  categoryName: string;
  productName: string;
  planName: string;
  sellerId: string;
  sellerName: string;
  sellerRole: 'SalesPerson' | 'SalesManager';
  /** The seller's manager when the sale was made (null for a manager's own sale). */
  managerName: string | null;
  quantity: number;
  unitPrice: number;
  unitCost: number;
  listAmount: number;
  saleAmount: number;
  discountAmount: number;
  discountPercent: number;
  costAmount: number;
  margin: number;
  durationUnit: string;
  durationCount: number;
  startDate: string;
  renewalDate: string;
  status: SaleStatus;
  notes: string | null;
  createdAtUtc: string;
  cancelledAtUtc: string | null;
  cancelledByName: string | null;
  cancelReason: string | null;
}

/** A page of sales plus totals of every matching ACTIVE sale. */
export interface SalesPage extends PagedResult<Sale> {
  totalSaleAmount: number;
  totalMargin: number;
}

export interface SaleQuery {
  /** One sales person's sales. */
  sellerId?: string;
  /** Manager-wise: the manager's own sales plus their team's. */
  managerId?: string;
  customerId?: string;
  search?: string;
  status?: SaleStatus;
  soldFrom?: string;
  soldTo?: string;
  page?: number;
  pageSize?: number;
  sortBy?: 'soldAt' | 'saleNumber' | 'customer' | 'product' | 'seller' | 'saleAmount' | 'renewalDate';
  sortDirection?: 'asc' | 'desc';
}

/** Totals for one sales person, or for one manager with their team. Amounts cover active sales. */
export interface SalesSummaryRow {
  id: string;
  name: string;
  username: string;
  role: 'SalesPerson' | 'SalesManager';
  managerName: string | null;
  teamSize: number;
  saleCount: number;
  cancelledCount: number;
  quantity: number;
  listAmount: number;
  discountAmount: number;
  saleAmount: number;
  costAmount: number;
  margin: number;
}

export interface SellerOption {
  id: string;
  name: string;
  role: 'SalesPerson' | 'SalesManager';
  status: string;
  managerName: string | null;
}
