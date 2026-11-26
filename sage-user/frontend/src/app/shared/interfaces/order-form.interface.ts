import type { DurationUnit } from './catalog.interface';

/** One value of an order-form list (Admin Panel > Settings > Order form lists). */
export interface MasterOption {
  id: number;
  code: string;
  name: string;
  isDefault: boolean;
}

/** The order form's wording, section by section (Edit wording by section). New lines separate lines. */
export interface OrderWording {
  signReturnLine: string | null;
  signReturnInstruction: string | null;
  orderNotes: string | null;
  /** Only on an order with free months. */
  freeMonthNote: string | null;
  extraItemTitle: string | null;
  extraItemDescription: string | null;
  productTitle: string | null;
  productDescription: string | null;
  periodLine: string | null;
  taxInformation: string | null;
  /** One clause per line. */
  signatureClauses: string | null;
  /** One line per row. */
  footer: string | null;
}

/** GET /api/user/order-form/options */
export interface OrderFormOptions {
  defaultGstPercent: number;
  maxFreeMonths: number;
  maxDiscountPercent: number;
  maxBackdateDays: number;
  billingFrequencies: MasterOption[];
  billingMethods: MasterOption[];
  paymentMethods: MasterOption[];
  paymentTerms: MasterOption[];
  currencies: MasterOption[];
  trackingStatuses: TrackingStatusOption[];
  /** Standard wording; {freeMonths} and {gstPercent} are filled in by the form. */
  wording: OrderWording;
}

export interface OrderContact {
  name: string;
  designation: string;
  email: string;
  phone: string;
}

/** POST /api/user/order-form/preview: the form as shown on the page, printed without saving. */
export interface OrderPreviewInput {
  customerName: string;
  productCode: string | null;
  orderDate: string;
  startDate: string;
  endDate: string;
  termMonths: number;
  freeMonths: number;
  primaryContact: OrderContact;
  billingFrequency: string | null;
  billingMethod: string | null;
  paymentMethod: string | null;
  paymentTerm: string | null;
  currency: string | null;
  quantity: number;
  unitPrice: number;
  listAmount: number;
  discountAmount: number;
  saleAmount: number;
  gstPercent: number;
  taxAmount: number;
  totalAmount: number;
  includedItemQuantity: number | null;
  wording: OrderWording;
}

/** POST /api/user/order-form */
export interface OrderInput {
  customerId: string;
  planId: number;
  quantity: number;
  discountAmount: number;
  durationUnit: DurationUnit;
  durationCount: number;
  startDate: string;
  notes?: string;
  gstPercent: number;
  freeMonths: number;
  billingFrequencyId: number;
  billingMethodId: number;
  paymentMethodId: number;
  paymentTermId: number;
  currencyId: number;
  primaryContact: OrderContact;
  billingContact: OrderContact;
  billToAddress?: string;
  wording: OrderWording;
  /** Quantity of the plan's included item (e.g. 1000 Team Link Extend seats). */
  includedItemQuantity?: number;
  /** New order from a Won lead (Lead Records > New Order): the order is linked to the lead. */
  leadId?: string;
  /** Draft 1, Sent to DocuSign 2, Signed 3; Cancelled 4 (edit only, with cancelReason) cancels the sale. */
  trackingStatusId?: number;
  cancelReason?: string;
}

/** GET /api/user/order-form/{saleId} */
export interface OrderDetails {
  saleId: string;
  hasOrderForm: boolean;
  gstPercent: number;
  taxAmount: number;
  totalAmount: number;
  freeMonths: number;
  billingFrequency: string | null;
  billingMethod: string | null;
  paymentMethod: string | null;
  paymentTerm: string | null;
  currency: string | null;
  primaryContact: OrderContact | null;
  billingContact: OrderContact | null;
  billToAddress: string | null;
  /** The wording this order was saved with (null for a sale from the simple page). */
  wording: OrderWording | null;
  /** Seat-priced plans: the financial year and seat range ("10-19", "50000+") the price came from; null = the plan's single price. */
  priceFiscalYear: number | null;
  priceSeatRange: string | null;
  includedItemQuantity: number | null;
  /** What the Edit order page needs to fill the form again. */
  categoryId: number;
  productId: number;
  planId: number;
  billingFrequencyId: number | null;
  billingMethodId: number | null;
  paymentMethodId: number | null;
  paymentTermId: number | null;
  currencyId: number | null;
  /** Financial year of the order date (2027 = FY27): an edit is priced with this year's seat prices. */
  orderFiscalYear: number;
  /** Saved with the order form, active and no cancellation request waiting. */
  canEdit: boolean;
  trackingStatusId: number | null;
  trackingCode: string | null;
  trackingName: string | null;
}

/** One tracking status of an order form (Draft, Sent to DocuSign, Signed, Cancelled). */
export interface TrackingStatusOption {
  id: number;
  code: 'DRAFT' | 'SENT_DOCUSIGN' | 'SIGNED' | 'CANCELLED';
  name: string;
}

/** Tracking status id of "Cancelled": choosing it cancels the sale (manager) or asks the manager to (sales person). */
export const TRACKING_CANCELLED = 4;

/** The fixed tracking statuses (dbo.OrderTrackingStatuses, DDL 036), used when the API sends none (e.g. an older API). */
export const TRACKING_STATUSES: TrackingStatusOption[] = [
  { id: 1, code: 'DRAFT', name: 'Draft' },
  { id: 2, code: 'SENT_DOCUSIGN', name: 'Sent to DocuSign' },
  { id: 3, code: 'SIGNED', name: 'Signed' },
  { id: TRACKING_CANCELLED, code: 'CANCELLED', name: 'Cancelled' },
];

/** The API's tracking statuses, or the fixed ones when it sent none. */
export const trackingStatusesOf = (options: { trackingStatuses?: TrackingStatusOption[] } | null | undefined): TrackingStatusOption[] =>
  options?.trackingStatuses?.length ? options.trackingStatuses : TRACKING_STATUSES;
