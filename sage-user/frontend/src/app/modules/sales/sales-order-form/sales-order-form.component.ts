import { Component, OnInit, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { Alert } from '../../../component/alert/alert';
import { CatalogService } from '../../../services/catalog/catalog.service';
import { CustomersService } from '../../../services/customers/customers.service';
import { LeadsService } from '../../../services/leads/leads.service';
import { AuthService } from '../../../services/auth/auth.service';
import { MessageService } from '../../../services/message/message.service';
import { OrderFormService } from '../../../services/order-form/order-form.service';
import { errorText } from '../../../services/rest/global-rest-service';
import { SalesService } from '../../../services/sales/sales.service';
import { DURATION_UNITS, type CatalogCategory, type DurationUnit } from '../../../shared/interfaces/catalog.interface';
import type { Customer } from '../../../shared/interfaces/customer.interface';
import type { Lead } from '../../../shared/interfaces/lead.interface';
import { LeadPicker } from './lead-picker/lead-picker.component';
import { TRACKING_CANCELLED, trackingStatusesOf, type MasterOption, type OrderContact, type OrderFormOptions, type OrderPreviewInput, type OrderWording } from '../../../shared/interfaces/order-form.interface';
import { WORDING_SECTIONS, longDate } from '../../../shared/utils/order-wording';
import { sameRich } from '../../../shared/utils/rich-text';
import { RichTextEditor } from '../../../component/rich-text-editor/rich-text-editor';
import { findSeatRange, fyLabel, seatRangeLabel } from '../../../shared/utils/seat-prices';
import { addDays, addMonths, dmy, formatDate, localDate, today } from '../../../shared/utils/dates';
import { formatDuration, formatInr } from '../../../shared/utils/format';
import { CustomerPicker } from '../sales-add/customer-picker/customer-picker.component';

/** Rounds to paise (2 decimals). */
const r2 = (n: number) => Math.round(n * 100) / 100;
const emptyContact = (): OrderContact => ({ name: '', designation: '', email: '', phone: '' });
const EMAIL = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
/** 10 digits, optionally after +91 or a leading 0 (spaces and dashes are ignored): the same rule as the server. */
const PHONE = /^(?:\+91|0)?[0-9]{10}$/;

/**
 * /sales/order-form — the New order form: customer, product and pricing (with GST), contacts and bill-to address, order
 * terms (free months, billing and payment from the master lists), and a running order summary. Saved through
 * POST /api/user/order-form as an ordinary sale plus its order details. The simple New sale page is separate.
 *
 * /sales/:id/edit — the same form filled with a saved order (the seller or their manager, while the order is active with
 * no cancellation request waiting). Saved through PUT /api/user/order-form/:id: the sale number stays, the price is worked
 * out again with the seat prices of the order date's financial year, and the order form PDF is made again.
 */
@Component({
  selector: 'app-sales-order-form',
  imports: [FormsModule, RouterLink, Alert, CustomerPicker, LeadPicker, RichTextEditor],
  templateUrl: './sales-order-form.component.html',
})
export class SalesOrderForm implements OnInit {
  /** From the route (/sales/:id/edit); empty on the New order form. */
  readonly id = input<string>();
  /** From the query string (?leadId=, Lead Records > New Order): that lead is chosen in the Lead box. */
  readonly leadId = input<string>();
  /** Step 1: the lead this new order is for (company and contact come from it), or null = enter the company manually. */
  readonly selectedLead = signal<Lead | null>(null);
  /** Edit: the lead the order was made from (shown, cannot change). */
  readonly linkedLeadNumber = signal<string | null>(null);
  /** The company comes from the lead: its search / Add new company are hidden. */
  readonly companyFromLead = computed(() => (this.editing() ? !!this.linkedLeadNumber() : !!this.selectedLead()));
  /** The lead whose company and contact are in the form (another choice fills them again; "No lead" clears them). */
  private filledFromLead: string | null = null;
  /** From the query string (?copyFrom=): Duplicate — a new order filled from this saved order. */
  readonly copyFrom = input<string>();
  /** Duplicate: the sale number copied (shown above the form). */
  readonly copiedFrom = signal<string | null>(null);

  // ---- Tracking (DDL 036) ----
  private readonly auth = inject(AuthService);
  protected readonly trackingCancelled = TRACKING_CANCELLED;
  /** Draft (1), Sent to DocuSign (2), Signed (3); Cancelled (4) only when editing, and cancels the sale. */
  readonly trackingStatusId = signal<number>(1);
  readonly cancelReason = signal('');
  /** A new order cannot be cancelled; on Edit, Cancelled needs a manager, or a sales person their manager allowed. */
  readonly trackingChoices = computed(() => {
    const all = trackingStatusesOf(this.options());
    const user = this.auth.user();
    const mayCancel = this.editing() && !!user && (user.isManager || user.canRequestCancellation);
    return all.filter((t) => t.id !== TRACKING_CANCELLED || mayCancel);
  });
  readonly isManager = computed(() => !!this.auth.user()?.isManager);
  readonly editing = computed(() => !!this.id());
  /** Edit: the order's sale number (shown in the title). */
  readonly saleNumber = signal<string | null>(null);
  /** Edit: a note above the form, e.g. the order's plan is no longer for sale. */
  readonly editNotice = signal<string | null>(null);
  /** Edit: the saved start date (the backdating limit applies only when it is changed, as on the server). */
  private originalStart: string | null = null;
  /** The customer whose contact and address are already in the form (choosing another one fills them again). */
  private filledFor: string | null = null;
  /** Edit: the saved discount, kept to the paisa while the list amount and discount % are unchanged. */
  private readonly keptDiscount = signal<{ list: number; percent: number; amount: number } | null>(null);
  private readonly catalogService = inject(CatalogService);
  private readonly orderFormService = inject(OrderFormService);
  private readonly salesService = inject(SalesService);
  private readonly customersService = inject(CustomersService);
  private readonly leadsService = inject(LeadsService);
  private readonly router = inject(Router);
  private readonly messages = inject(MessageService);
  protected readonly inr = formatInr;
  protected readonly date = formatDate;
  /** dd-MM-yyyy for the End date field. */
  protected readonly dmy = dmy;
  /** "16th November 2026" for the summary period (as on the order form PDF). */
  protected readonly spaced = (date: string) => longDate(date).replace(/-/g, ' ');
  protected readonly durationText = formatDuration;
  protected readonly units = DURATION_UNITS;
  /** Today on a new order; the day it was saved on an edited one. */
  readonly orderDate = signal(today());
  protected readonly fyLabel = fyLabel;
  protected readonly rangeLabel = seatRangeLabel;

  readonly catalog = signal<CatalogCategory[] | null>(null);
  readonly options = signal<OrderFormOptions | null>(null);
  readonly loadError = signal<string | null>(null);
  readonly saveError = signal<string | null>(null);
  readonly busy = signal(false);

  // ---- 1. Customer ----
  readonly customer = signal<Customer | null>(null);

  // ---- 2. Product and pricing ----
  readonly categoryId = signal<number | null>(null);
  readonly productId = signal<number | null>(null);
  readonly planId = signal<number | null>(null);
  readonly category = computed(() => this.catalog()?.find((c) => c.id === this.categoryId()) ?? null);
  readonly product = computed(() => this.category()?.products.find((p) => p.id === this.productId()) ?? null);
  readonly plan = computed(() => this.product()?.plans.find((p) => p.id === this.planId()) ?? null);
  readonly quantity = signal<number | null>(1);
  readonly discountPercent = signal<number | null>(0);
  readonly gstPercent = signal<number | null>(18);
  /** Quantity of the plan's included item (Team Link Extend seats), from the plan; the seller may change it. */
  readonly includedItemQuantity = signal<number | null>(null);

  /**
   * Seat-priced plans (e.g. LinkedIn Advanced / Advanced Plus): the range the seat count falls in, for this financial
   * year. Every seat is charged that range's price (8 seats -> the 1-9 price, 150 seats -> the 100-199 price).
   */
  readonly seatRange = computed(() => {
    const plan = this.plan();
    const seats = Number(this.quantity());
    return plan?.hasSeatPrices && seats >= 1 ? findSeatRange(plan.seatPrices, seats) : null;
  });
  /** Price per seat: the seat range's price, or the plan's single price. */
  readonly unitPrice = computed(() => {
    const plan = this.plan();
    if (!plan) return 0;
    return plan.hasSeatPrices ? (this.seatRange()?.price ?? 0) : plan.price;
  });
  /** Months in one plan term (the price is per term): 12 for a 1-year plan. */
  readonly planMonths = computed(() => {
    const plan = this.plan();
    return plan ? (plan.durationUnit === 'Year' ? 12 : plan.durationUnit === 'Quarter' ? 3 : 1) * plan.durationCount : 0;
  });
  /** "year" for a 1-year plan, otherwise "12 months" etc. (what the unit price covers). */
  readonly planTermText = computed(() => (this.planMonths() === 12 ? 'year' : `${this.planMonths()} months`));
  /** Plan terms paid for: 24 paid months of a 1-year plan = 2 (1 until paid months are chosen). Same rule as the server. */
  readonly terms = computed(() => (this.planMonths() > 0 && this.paidMonths() > 0 ? this.paidMonths() / this.planMonths() : 1));
  /** "2 years" (shown in the subtotal when more than one term is paid for). */
  readonly termsText = computed(() => {
    const t = Math.round(this.terms() * 100) / 100;
    return this.planMonths() === 12 ? `${t} year${t === 1 ? '' : 's'}` : `${t} terms`;
  });
  /** Unit price for the whole paid term, rounded like the server: price per term × terms. */
  readonly termUnitPrice = computed(() => r2(this.unitPrice() * this.terms()));
  readonly listAmount = computed(() => r2(this.termUnitPrice() * (this.quantity() ?? 0)));
  readonly discountAmount = computed(() => {
    const kept = this.keptDiscount();
    if (kept && kept.list === this.listAmount() && kept.percent === this.discountPercent()) return kept.amount;
    return r2((this.listAmount() * (this.discountPercent() ?? 0)) / 100);
  });
  readonly saleAmount = computed(() => r2(this.listAmount() - this.discountAmount()));
  readonly taxAmount = computed(() => r2((this.saleAmount() * (this.gstPercent() ?? 0)) / 100));
  readonly totalAmount = computed(() => r2(this.saleAmount() + this.taxAmount()));

  // ---- 3. Contacts and billing address ----
  readonly primary = signal<OrderContact>(emptyContact());
  readonly billing = signal<OrderContact>(emptyContact());
  billToAddress = '';

  // ---- 4. Order terms ----
  readonly startDate = signal(today());
  /** Paid months: 12, 24 or 36 (saved as 1, 2 or 3 years). Starts on "Select". */
  protected readonly paidMonthChoices = [12, 24, 36];
  readonly paidMonthsChoice = signal<number | null>(null);
  readonly durationUnit = signal<DurationUnit>('Year');
  readonly durationCount = signal<number | null>(null);
  readonly freeMonths = signal(0);
  readonly billingFrequencyId = signal<number | null>(null);
  readonly billingMethodId = signal<number | null>(null);
  readonly paymentMethodId = signal<number | null>(null);
  readonly paymentTermId = signal<number | null>(null);
  readonly currencyId = signal<number | null>(null);
  notes = '';

  readonly paidMonths = computed(() => {
    const count = this.durationCount() ?? 0;
    return this.durationUnit() === 'Year' ? 12 * count : this.durationUnit() === 'Quarter' ? 3 * count : count;
  });
  readonly termText = computed(() => (this.freeMonths() > 0 ? `${this.paidMonths()} + ${this.freeMonths()} months` : `${this.paidMonths()} months`));
  /** Same rule as the server: start + paid duration + free months. */
  /**
   * Last day of the order: start + paid months + free months, less one day (17-11-2026 for 1 year ends 16-11-2027;
   * with 2 free months 16-01-2028). The sale's renewal date (saved by the server) is the day after.
   */
  readonly endDate = computed(() =>
    this.startDate() && this.paidMonths() > 0 ? addDays(addMonths(this.startDate(), this.paidMonths() + this.freeMonths()), -1) : '');
  readonly freeMonthChoices = computed(() => Array.from({ length: (this.options()?.maxFreeMonths ?? 0) + 1 }, (_, i) => i));
  readonly earliestStart = computed(() => addDays(today(), -(this.options()?.maxBackdateDays ?? 365)));

  private readonly nameOf = (list: MasterOption[] | undefined, id: number | null) => list?.find((o) => o.id === id)?.name ?? '-';
  readonly billingText = computed(() => {
    const o = this.options();
    return `${this.nameOf(o?.billingFrequencies, this.billingFrequencyId())}, ${this.nameOf(o?.billingMethods, this.billingMethodId())}`;
  });
  readonly currencyText = computed(() => this.nameOf(this.options()?.currencies, this.currencyId()));

  // ---- 5. Edit wording by section ----
  protected readonly wordingSections = WORDING_SECTIONS;
  readonly wordingOpen = signal(false);
  /** Sections the seller has changed for this order; the rest follow the standard text and the form's values. */
  readonly wordingOverrides = signal<Partial<Record<keyof OrderWording, string>>>({});
  /**
   * Standard text with this order's values filled in. Product title = product + plan name; product description and the
   * included item (e.g. LinkedIn's Team Link Extend seat) come from the chosen plan, with {paidMonths}, {freeMonths},
   * {totalMonths} and {seats} filled in.
   */
  readonly standardWording = computed<OrderWording>(() => {
    const std = this.options()?.wording;
    const free = this.freeMonths();
    const product = this.product();
    const plan = this.plan();
    const fill = (text: string | null | undefined) =>
      (text ?? '')
        .replace(/\{paidMonths\}/g, String(this.paidMonths()))
        .replace(/\{freeMonths\}/g, String(free))
        .replace(/\{totalMonths\}/g, String(this.paidMonths() + free))
        .replace(/\{seats\}/g, String(this.quantity() ?? 0))
        .replace(/\{quantity\}/g, String(this.includedItemQuantity() ?? ''));
    const lastDay = this.endDate();
    return {
      signReturnLine: std?.signReturnLine ?? '',
      signReturnInstruction: std?.signReturnInstruction ?? '',
      orderNotes: std?.orderNotes ?? '',
      // "{freeMonths} free months" reads "1 free month" for one month.
      freeMonthNote: (std?.freeMonthNote ?? '').replace(/\{freeMonths\} free months/g, free === 1 ? '1 free month' : `${free} free months`).replace(/\{freeMonths\}/g, String(free)),
      extraItemTitle: plan?.includedItemTitle ?? '',
      extraItemDescription: fill(plan?.includedItemDescription),
      productTitle: product && plan ? `${product.name} ${plan.name}` : (product?.name ?? ''),
      productDescription: plan?.description || product?.description || '',
      periodLine: this.startDate() && lastDay
        ? `${longDate(this.startDate())} to ${longDate(lastDay)}${free > 0 ? ` (${free} free month${free === 1 ? '' : 's'} included)` : ''}`
        : '',
      taxInformation: (std?.taxInformation ?? '').replace(/\{gstPercent\}/g, String(this.gstPercent() ?? 0)),
      signatureClauses: std?.signatureClauses ?? '',
      footer: std?.footer ?? '',
    };
  });
  /** What the order will say: the seller's text where changed, otherwise the standard text. */
  readonly wording = computed<OrderWording>(() => {
    const std = this.standardWording();
    const own = this.wordingOverrides();
    const result = { ...std };
    for (const key of Object.keys(own) as (keyof OrderWording)[]) result[key] = own[key] ?? null;
    return result;
  });
  readonly changedSections = computed(() => Object.keys(this.wordingOverrides()).length);

  /** Problems shown under the form; Save stays disabled while there are any. */
  readonly problems = computed(() => {
    const p: string[] = [];
    if (!this.customer()) p.push('Choose a company, or add a new one.');
    if (!this.plan()) p.push('Choose a category, product and plan.');
    const q = this.quantity();
    if (q === null || q < 1 || q > 100000 || !Number.isInteger(q)) p.push('Seats must be a whole number from 1 to 100000.');
    else {
      const plan = this.plan();
      if (plan?.hasSeatPrices && plan.seatPrices.length === 0)
        p.push(`This plan has no seat prices for ${fyLabel(plan.priceFiscalYear ?? 0)} yet. Ask the admin to add them.`);
      else if (plan?.hasSeatPrices && !this.seatRange()) p.push(`No seat price covers ${q} seats for this plan.`);
    }
    const d = this.discountPercent();
    const maxDiscount = this.options()?.maxDiscountPercent ?? 100;
    if (d === null || d < 0 || d > 100) p.push('Discount must be from 0 to 100%.');
    else if (d > maxDiscount) p.push(`The discount cannot be more than ${maxDiscount}% (company limit).`);
    const g = this.gstPercent();
    if (g === null || g < 0 || g > 28) p.push('GST must be from 0 to 28%.');
    const primary = this.primary();
    if (!primary.name.trim()) p.push("Enter the primary contact's name.");
    for (const [who, c] of [['primary contact', primary], ['billing contact', this.billing()]] as const) {
      if (c.email.trim() && !EMAIL.test(c.email.trim())) p.push(`Enter a valid email for the ${who}.`);
      if (c.phone.trim() && !PHONE.test(c.phone.replace(/[\s-]/g, ''))) p.push(`Enter a 10-digit contact number for the ${who}.`);
    }
    if (!this.paidMonthsChoice()) p.push('Choose the paid months (12, 24 or 36).');
    if (!this.startDate()) p.push('Enter the start date.');
    else if (this.startDate() !== this.originalStart && this.startDate() < this.earliestStart()) p.push('The start date is further back than the company allows.');
    if (!this.billingFrequencyId() || !this.billingMethodId() || !this.paymentMethodId() || !this.paymentTermId() || !this.currencyId())
      p.push('Choose billing frequency, billing method, payment method, payment term and currency.');
    if (this.trackingStatusId() === TRACKING_CANCELLED && !this.cancelReason().trim()) p.push('Enter the reason for cancelling the order.');
    return p;
  });

  constructor() {
    // Choosing a customer fills the primary contact and the bill-to address (the seller can still change them).
    // On the Edit page the saved order's customer keeps the saved contact and address.
    effect(() => {
      const c = this.customer();
      untracked(() => {
        if (!c || c.id === this.filledFor) return;
        this.filledFor = c.id;
        this.primary.set({ name: c.contactPerson ?? '', designation: '', email: c.email ?? '', phone: c.phone ?? '' });
        this.billToAddress = [c.address, c.city, c.state, c.country].filter((x) => !!x && x.trim()).join('\n');
      });
    });
    // Step 1: choosing a lead fills the company and contact from it; going back to "No lead" clears them.
    effect(() => {
      const lead = this.selectedLead();
      untracked(() => {
        if (this.editing() || (lead?.id ?? null) === this.filledFromLead) return;
        const had = this.filledFromLead;
        this.filledFromLead = lead?.id ?? null;
        if (lead) {
          this.fillFromLead(lead).catch((err) => this.saveError.set(errorText(err, 'Unable to load the lead\'s company.')));
        } else if (had) {
          this.filledFor = null;
          this.customer.set(null);
          this.primary.set(emptyContact());
          this.billToAddress = '';
        }
      });
    });
  }

  async ngOnInit(): Promise<void> {
    const id = this.id();
    if (id) {
      await this.loadOrder(id, false);
      return;
    }
    const copyFrom = this.copyFrom();
    if (copyFrom) {
      await this.loadOrder(copyFrom, true);
      return;
    }
    try {
      const [catalog, options] = await Promise.all([this.catalogService.catalog(), this.orderFormService.options()]);
      this.catalog.set(catalog);
      this.options.set(options);
      // Billing, payment and currency start on "Select": the seller chooses each (lists: Admin Panel > Settings > Order form lists).
      this.gstPercent.set(options.defaultGstPercent);
      if (catalog.length === 1) this.chooseCategory(catalog[0].id);
      // Lead Records > New Order: that lead starts chosen in the Lead box.
      const leadId = this.leadId();
      if (leadId) this.selectedLead.set(await this.leadsService.get(leadId));
    } catch (err) {
      this.loadError.set(errorText(err, 'Unable to load the order form.'));
    }
  }

  /** A lead was chosen: its company and client become the company and primary contact. */
  private async fillFromLead(lead: Lead): Promise<void> {
    const company = await this.customersService.get(lead.companyId);
    if (this.selectedLead()?.id !== lead.id) return; // another lead was chosen meanwhile
    this.filledFor = company.id;
    this.customer.set(company);
    this.primary.set({
      name: lead.clientName ?? company.contactPerson ?? '',
      designation: lead.clientDesignation ?? '',
      email: lead.clientEmail ?? company.email ?? '',
      phone: lead.clientPhone ?? company.phone ?? '',
    });
    this.billToAddress = [company.address, company.city, company.state, company.country].filter((x) => !!x && x.trim()).join('\n');
  }

  /**
   * Fills the form with a saved order.
   * Edit (copy = false): the order itself, priced with the seat prices of its order date's financial year.
   * Duplicate (copy = true): a new order with the same company, product, terms, contacts and wording, starting today as
   * Draft, priced with today's price list.
   */
  private async loadOrder(id: string, copy: boolean): Promise<void> {
    try {
      const [sale, details, options] = await Promise.all([this.salesService.get(id), this.orderFormService.details(id), this.orderFormService.options()]);
      if (!copy) this.saleNumber.set(sale.saleNumber);
      if (!details.hasOrderForm) {
        this.loadError.set(copy ? 'Only orders saved with the New order form can be duplicated.' : 'Only orders saved with the New order form can be edited.');
        return;
      }
      if (!copy && !details.canEdit) {
        this.loadError.set(sale.status === 'Cancelled'
          ? 'A cancelled order cannot be edited.'
          : 'This order has a cancellation request waiting for the manager. It can be edited once the request is decided.');
        return;
      }
      const [catalog, customer] = await Promise.all([
        this.catalogService.catalog(copy ? undefined : details.orderFiscalYear), this.customersService.get(sale.customerId)]);

      this.options.set(options);
      this.catalog.set(catalog);
      if (copy) this.copiedFrom.set(sale.saleNumber);
      else {
        this.linkedLeadNumber.set(sale.leadNumber);
        this.orderDate.set(localDate(sale.createdAtUtc));
        this.trackingStatusId.set(details.trackingStatusId ?? 1);
      }

      this.filledFor = customer.id;
      this.customer.set(customer);
      this.categoryId.set(details.categoryId);
      this.productId.set(details.productId);
      this.planId.set(details.planId);
      if (!this.plan()) {
        this.editNotice.set(`This order's plan (${sale.productName} ${sale.planName}) is no longer for sale. Choose another plan.`);
        this.categoryId.set(null);
        this.productId.set(null);
        this.planId.set(null);
      }
      this.includedItemQuantity.set(details.includedItemQuantity ?? this.plan()?.includedItemQuantity ?? null);
      this.quantity.set(sale.quantity);
      this.discountPercent.set(sale.discountPercent);
      if (!copy) this.keptDiscount.set({ list: sale.listAmount, percent: sale.discountPercent, amount: sale.discountAmount });
      this.gstPercent.set(details.gstPercent);

      const months = (sale.durationUnit === 'Year' ? 12 : sale.durationUnit === 'Quarter' ? 3 : 1) * sale.durationCount;
      this.paidMonthsChoice.set(months);
      this.durationUnit.set(sale.durationUnit);
      this.durationCount.set(sale.durationCount);
      if (!copy) {
        this.startDate.set(sale.startDate);
        this.originalStart = sale.startDate;
      }
      this.freeMonths.set(details.freeMonths);
      this.billingFrequencyId.set(details.billingFrequencyId);
      this.billingMethodId.set(details.billingMethodId);
      this.paymentMethodId.set(details.paymentMethodId);
      this.paymentTermId.set(details.paymentTermId);
      this.currencyId.set(details.currencyId);
      this.notes = sale.notes ?? '';

      const contact = (c: OrderContact | null): OrderContact => ({ name: c?.name ?? '', designation: c?.designation ?? '', email: c?.email ?? '', phone: c?.phone ?? '' });
      this.primary.set(contact(details.primaryContact));
      this.billing.set(contact(details.billingContact));
      this.billToAddress = details.billToAddress ?? '';

      // Sections the seller had changed stay changed; the others follow the standard text (and so the edited values).
      const saved = details.wording;
      if (saved) {
        const std = this.standardWording();
        const own: Partial<Record<keyof OrderWording, string>> = {};
        for (const key of Object.keys(std) as (keyof OrderWording)[]) {
          if (key === 'freeMonthNote' && details.freeMonths <= 0) continue;
          if (!sameRich(saved[key], std[key])) own[key] = saved[key] ?? '';
        }
        this.wordingOverrides.set(own);
      }
    } catch (err) {
      this.loadError.set(errorText(err, 'Unable to load the order.'));
    }
  }

  chooseCategory(id: number | null): void {
    this.categoryId.set(id);
    const products = this.category()?.products ?? [];
    this.chooseProduct(products.length === 1 ? products[0].id : null);
  }

  chooseProduct(id: number | null): void {
    this.productId.set(id);
    const plans = this.product()?.plans ?? [];
    this.choosePlan(plans.length === 1 ? plans[0].id : null);
  }

  choosePlan(id: number | null): void {
    this.planId.set(id);
    const plan = this.plan();
    this.includedItemQuantity.set(plan?.includedItemQuantity ?? null);
  }

  /** 12 / 24 / 36 paid months, saved as 1 / 2 / 3 years. */
  choosePaidMonths(months: number | null): void {
    this.paidMonthsChoice.set(months);
    this.durationUnit.set('Year');
    this.durationCount.set(months ? months / 12 : null);
  }

  setPrimary(field: keyof OrderContact, value: string): void {
    this.primary.update((c) => ({ ...c, [field]: value }));
  }

  setBilling(field: keyof OrderContact, value: string): void {
    this.billing.update((c) => ({ ...c, [field]: value }));
  }

  /** A section edited back to the standard text (same words and formatting) counts as unchanged again. */
  setWording(key: keyof OrderWording, value: string): void {
    if (sameRich(value, this.standardWording()[key])) {
      this.resetWording(key);
      return;
    }
    this.wordingOverrides.update((o) => ({ ...o, [key]: value }));
  }

  /** Back to the standard text for one section (or all when no key). */
  resetWording(key?: keyof OrderWording): void {
    if (!key) {
      this.wordingOverrides.set({});
      return;
    }
    this.wordingOverrides.update((o) => {
      const next = { ...o };
      delete next[key];
      return next;
    });
  }

  isChanged(key: keyof OrderWording): boolean {
    return key in this.wordingOverrides();
  }

  /** "Same as primary": copies the primary contact into the billing contact. */
  copyPrimaryToBilling(): void {
    this.billing.set({ ...this.primary() });
  }

  /** Download PDF / Download Word / Preview PDF: the form as filled in, printed by the API without saving it. */
  readonly fileBusy = signal<'pdf' | 'word' | 'preview' | null>(null);
  async printDraft(kind: 'pdf' | 'word' | 'preview'): Promise<void> {
    if (this.problems().length > 0 || this.fileBusy()) return;
    this.saveError.set(null);
    this.fileBusy.set(kind);
    const o = this.options();
    const c = this.primary();
    const body: OrderPreviewInput = {
      customerName: this.customer()?.name ?? '',
      productCode: this.product()?.code ?? null,
      orderDate: this.orderDate(),
      startDate: this.startDate(),
      endDate: this.endDate(),
      termMonths: this.paidMonths() + this.freeMonths(),
      freeMonths: this.freeMonths(),
      primaryContact: { name: c.name.trim(), designation: c.designation.trim(), email: c.email.trim(), phone: c.phone.trim() },
      billingFrequency: this.nameOf(o?.billingFrequencies, this.billingFrequencyId()),
      billingMethod: this.nameOf(o?.billingMethods, this.billingMethodId()),
      paymentMethod: this.nameOf(o?.paymentMethods, this.paymentMethodId()),
      paymentTerm: this.nameOf(o?.paymentTerms, this.paymentTermId()),
      currency: this.nameOf(o?.currencies, this.currencyId()),
      quantity: this.quantity() ?? 0,
      unitPrice: this.termUnitPrice(),
      listAmount: this.listAmount(),
      discountAmount: this.discountAmount(),
      saleAmount: this.saleAmount(),
      gstPercent: this.gstPercent() ?? 0,
      taxAmount: this.taxAmount(),
      totalAmount: this.totalAmount(),
      includedItemQuantity: this.wording().extraItemTitle && this.includedItemQuantity() ? Number(this.includedItemQuantity()) : null,
      wording: { ...this.wording(), freeMonthNote: this.freeMonths() > 0 ? this.wording().freeMonthNote : null },
    };
    try {
      if (kind === 'preview') await this.orderFormService.previewDraft(body);
      else await this.orderFormService.downloadDraft(body, kind);
    } catch (err) {
      this.saveError.set(errorText(err, 'The file could not be made. Please try again.'));
    } finally {
      this.fileBusy.set(null);
    }
  }

  async save(): Promise<void> {
    if (this.problems().length > 0) return;
    this.saveError.set(null);
    this.busy.set(true);
    const trim = (c: OrderContact): OrderContact => ({ name: c.name.trim(), designation: c.designation.trim(), email: c.email.trim(), phone: c.phone.trim() });
    try {
      const body = {
        customerId: this.customer()!.id,
        planId: this.planId()!,
        quantity: this.quantity()!,
        discountAmount: this.discountAmount(),
        durationUnit: this.durationUnit(),
        durationCount: this.durationCount()!,
        startDate: this.startDate(),
        notes: this.notes.trim() || undefined,
        gstPercent: this.gstPercent()!,
        freeMonths: this.freeMonths(),
        billingFrequencyId: this.billingFrequencyId()!,
        billingMethodId: this.billingMethodId()!,
        paymentMethodId: this.paymentMethodId()!,
        paymentTermId: this.paymentTermId()!,
        currencyId: this.currencyId()!,
        primaryContact: trim(this.primary()),
        billingContact: trim(this.billing()),
        billToAddress: this.billToAddress.trim() || undefined,
        wording: { ...this.wording(), freeMonthNote: this.freeMonths() > 0 ? this.wording().freeMonthNote : null },
        includedItemQuantity: this.wording().extraItemTitle && this.includedItemQuantity() ? Number(this.includedItemQuantity()) : undefined,
        leadId: this.id() ? undefined : this.selectedLead()?.id,
        trackingStatusId: this.trackingStatusId(),
        cancelReason: this.trackingStatusId() === TRACKING_CANCELLED ? this.cancelReason().trim() : undefined,
      };
      const id = this.id();
      const sale = id ? await this.orderFormService.update(id, body) : await this.orderFormService.create(body);
      const cancelled = id && this.trackingStatusId() === TRACKING_CANCELLED;
      const newLead = !id && !body.leadId && sale.leadNumber ? ` Lead ${sale.leadNumber} was created for ${sale.customerName}; fill in its details in Lead Records.` : '';
      this.messages.success(!id ? `Order ${sale.saleNumber} saved.${newLead}`
        : cancelled ? (this.isManager() ? `Order ${sale.saleNumber} updated and cancelled.` : `Order ${sale.saleNumber} updated. Cancellation requested from your manager.`)
        : `Order ${sale.saleNumber} updated. The order form PDF was made again.`);
      this.router.navigate(['/sales', sale.id]);
    } catch (err) {
      this.saveError.set(errorText(err));
    } finally {
      this.busy.set(false);
    }
  }
}
