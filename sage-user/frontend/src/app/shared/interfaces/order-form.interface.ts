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
  /** Standard wording; {freeMonths} and {gstPercent} are filled in by the form. */
  wording: OrderWording;
}

export interface OrderContact {
  name: string;
  designation: string;
  email: string;
  phone: string;
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
}
