import type { PagedResult } from './api.interface';
import type { DurationUnit } from './catalog.interface';

export type SaleStatus = 'Active' | 'Cancelled';
export type SaleScope = 'mine' | 'team';

/** Mirrors the User API's SaleDto. Amounts are INR; dates are 'yyyy-MM-dd'. */
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
  quantity: number;
  unitPrice: number;
  unitCost: number;
  listAmount: number;
  saleAmount: number;
  discountAmount: number;
  discountPercent: number;
  costAmount: number;
  margin: number;
  durationUnit: DurationUnit;
  durationCount: number;
  startDate: string;
  renewalDate: string;
  status: SaleStatus;
  notes: string | null;
  createdAtUtc: string;
  cancelledAtUtc: string | null;
  cancelledByName: string | null;
  cancelReason: string | null;
  /** A cancellation request is waiting for the manager. */
  hasPendingCancelRequest: boolean;
  /** Cancellation requests for this sale, newest first (sale details only). */
  cancelRequests?: CancelRequest[];
}

export type CancelRequestStatus = 'Pending' | 'Approved' | 'Rejected';

/** A sales person's request to cancel one of their sales; the manager approves or rejects it. */
export interface CancelRequest {
  id: string;
  saleId: string;
  saleNumber: string;
  customerName: string;
  productName: string;
  saleAmount: number;
  requestedById: string;
  requestedByName: string;
  reason: string;
  status: CancelRequestStatus;
  requestedAtUtc: string;
  decidedByName: string | null;
  decidedAtUtc: string | null;
  decisionNote: string | null;
}

/** A page of sales plus totals of the matching ACTIVE sales. */
export interface SalesPage extends PagedResult<Sale> {
  totalSaleAmount: number;
  totalMargin: number;
}

/** Body of POST /api/user/sales. Give saleAmount OR discountAmount; price and cost come from the price list. */
export interface SaleInput {
  customerId: string;
  planId: number;
  quantity: number;
  saleAmount?: number;
  discountAmount?: number;
  durationUnit?: DurationUnit;
  durationCount?: number;
  startDate: string;
  renewalDate?: string;
  notes?: string;
}

export interface SaleQuery {
  scope?: SaleScope;
  sellerId?: string;
  search?: string;
  status?: SaleStatus;
  soldFrom?: string;
  soldTo?: string;
  page?: number;
  pageSize?: number;
  sortBy?: 'soldAt' | 'saleNumber' | 'customer' | 'product' | 'saleAmount' | 'renewalDate';
  sortDirection?: 'asc' | 'desc';
}

export interface TeamMember {
  id: string;
  name: string;
  username: string;
  status: string;
  /** May ask for their sales to be cancelled. */
  canRequestCancellation: boolean;
  /** Their requests waiting for the manager. */
  pendingRequests: number;
}
