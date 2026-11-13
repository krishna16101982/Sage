/** Mirrors the Admin API CustomerDto. Customers are shared with the User Panel (sales users add them there too). */
export interface Customer {
  id: string;
  name: string;
  contactPerson: string | null;
  email: string | null;
  phone: string | null;
  gstin: string | null;
  address: string | null;
  city: string | null;
  state: string | null;
  country: string;
  createdAtUtc: string;
  /** The sales person who added it, or an admin user followed by "(Admin)". */
  createdByName: string | null;
  /** Active sales to this customer (all sellers). */
  saleCount: number;
}

export interface CustomerInput {
  name: string;
  contactPerson?: string;
  email?: string;
  phone?: string;
  gstin?: string;
  address?: string;
  city?: string;
  state?: string;
  country?: string;
}

export interface CustomerQuery {
  page?: number;
  pageSize?: number;
  search?: string;
  sortBy?: 'name' | 'city' | 'createdAtUtc';
  sortDirection?: 'asc' | 'desc';
}
