import { Component, OnInit, computed, effect, inject, signal, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { Alert } from '../../../component/alert/alert';
import { CatalogService } from '../../../services/catalog/catalog.service';
import { MessageService } from '../../../services/message/message.service';
import { OrderFormService } from '../../../services/order-form/order-form.service';
import { errorText } from '../../../services/rest/global-rest-service';
import { DURATION_UNITS, type CatalogCategory, type DurationUnit } from '../../../shared/interfaces/catalog.interface';
import type { Customer } from '../../../shared/interfaces/customer.interface';
import type { MasterOption, OrderContact, OrderFormOptions, OrderWording } from '../../../shared/interfaces/order-form.interface';
import { WORDING_SECTIONS, longDate } from '../../../shared/utils/order-wording';
import { findSeatRange, fyLabel, seatRangeLabel } from '../../../shared/utils/seat-prices';
import { addDays, addMonths, formatDate, today } from '../../../shared/utils/dates';
import { formatDuration, formatInr } from '../../../shared/utils/format';
import { CustomerPicker } from '../sales-add/customer-picker/customer-picker.component';

/** Rounds to paise (2 decimals). */
const r2 = (n: number) => Math.round(n * 100) / 100;
const emptyContact = (): OrderContact => ({ name: '', designation: '', email: '', phone: '' });
const EMAIL = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
const PHONE = /^\+?[0-9\s\-()]{7,20}$/;

/**
 * /sales/order-form — the New order form: customer, product and pricing (with GST), contacts and bill-to address, order
 * terms (free months, billing and payment from the master lists), and a running order summary. Saved through
 * POST /api/user/order-form as an ordinary sale plus its order details. The simple New sale page is separate.
 */
@Component({
  selector: 'app-sales-order-form',
  imports: [FormsModule, RouterLink, Alert, CustomerPicker],
  templateUrl: './sales-order-form.component.html',
})
export class SalesOrderForm implements OnInit {
  private readonly catalogService = inject(CatalogService);
  private readonly orderFormService = inject(OrderFormService);
  private readonly router = inject(Router);
  private readonly messages = inject(MessageService);
  protected readonly inr = formatInr;
  protected readonly date = formatDate;
  protected readonly durationText = formatDuration;
  protected readonly units = DURATION_UNITS;
  protected readonly orderDate = today();
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
  readonly listAmount = computed(() => r2(this.unitPrice() * (this.quantity() ?? 0)));
  readonly discountAmount = computed(() => r2((this.listAmount() * (this.discountPercent() ?? 0)) / 100));
  readonly saleAmount = computed(() => r2(this.listAmount() - this.discountAmount()));
  readonly taxAmount = computed(() => r2((this.saleAmount() * (this.gstPercent() ?? 0)) / 100));
  readonly totalAmount = computed(() => r2(this.saleAmount() + this.taxAmount()));

  // ---- 3. Contacts and billing address ----
  readonly primary = signal<OrderContact>(emptyContact());
  readonly billing = signal<OrderContact>(emptyContact());
  billToAddress = '';

  // ---- 4. Order terms ----
  readonly startDate = signal(today());
  readonly durationUnit = signal<DurationUnit>('Year');
  readonly durationCount = signal<number | null>(1);
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
  readonly endDate = computed(() => (this.startDate() && this.paidMonths() > 0 ? addMonths(this.startDate(), this.paidMonths() + this.freeMonths()) : ''));
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
  /** Standard text with this order's values filled in. */
  readonly standardWording = computed<OrderWording>(() => {
    const std = this.options()?.wording;
    const free = this.freeMonths();
    const end = this.endDate();
    const lastDay = end ? addDays(end, -1) : '';
    return {
      signReturnLine: std?.signReturnLine ?? '',
      signReturnInstruction: std?.signReturnInstruction ?? '',
      orderNotes: std?.orderNotes ?? '',
      freeMonthNote: (std?.freeMonthNote ?? '').replace(/\{freeMonths\}/g, String(free)),
      extraItemTitle: '',
      extraItemDescription: '',
      productTitle: this.product()?.name ?? '',
      productDescription: this.product()?.description ?? this.plan()?.description ?? '',
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
    if (!this.customer()) p.push('Choose a customer, or add a new one.');
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
      if (c.phone.trim() && !PHONE.test(c.phone.trim())) p.push(`Enter a valid contact number for the ${who}.`);
    }
    const count = this.durationCount();
    if (count === null || count < 1 || count > 120) p.push('Paid duration must be from 1 to 120.');
    if (!this.startDate()) p.push('Enter the start date.');
    else if (this.startDate() < this.earliestStart()) p.push('The start date is further back than the company allows.');
    if (!this.billingFrequencyId() || !this.billingMethodId() || !this.paymentMethodId() || !this.paymentTermId() || !this.currencyId())
      p.push('Choose billing frequency, billing method, payment method, payment term and currency.');
    return p;
  });

  constructor() {
    // Choosing a customer fills the primary contact and the bill-to address (the seller can still change them).
    effect(() => {
      const c = this.customer();
      untracked(() => {
        if (!c) return;
        this.primary.set({ name: c.contactPerson ?? '', designation: '', email: c.email ?? '', phone: c.phone ?? '' });
        this.billToAddress = [c.address, c.city, c.state, c.country].filter((x) => !!x && x.trim()).join('\n');
      });
    });
  }

  async ngOnInit(): Promise<void> {
    try {
      const [catalog, options] = await Promise.all([this.catalogService.catalog(), this.orderFormService.options()]);
      this.catalog.set(catalog);
      this.options.set(options);
      // Every drop-down starts on its list's default (Admin Panel > Settings > Order form lists).
      const def = (list: MasterOption[]) => (list.find((o) => o.isDefault) ?? list[0])?.id ?? null;
      this.billingFrequencyId.set(def(options.billingFrequencies));
      this.billingMethodId.set(def(options.billingMethods));
      this.paymentMethodId.set(def(options.paymentMethods));
      this.paymentTermId.set(def(options.paymentTerms));
      this.currencyId.set(def(options.currencies));
      this.gstPercent.set(options.defaultGstPercent);
      if (catalog.length === 1) this.chooseCategory(catalog[0].id);
    } catch (err) {
      this.loadError.set(errorText(err, 'Unable to load the order form.'));
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
    if (plan) {
      this.durationUnit.set(plan.durationUnit);
      this.durationCount.set(plan.durationCount);
    }
  }

  setPrimary(field: keyof OrderContact, value: string): void {
    this.primary.update((c) => ({ ...c, [field]: value }));
  }

  setBilling(field: keyof OrderContact, value: string): void {
    this.billing.update((c) => ({ ...c, [field]: value }));
  }

  setWording(key: keyof OrderWording, value: string): void {
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

  async save(): Promise<void> {
    if (this.problems().length > 0) return;
    this.saveError.set(null);
    this.busy.set(true);
    const trim = (c: OrderContact): OrderContact => ({ name: c.name.trim(), designation: c.designation.trim(), email: c.email.trim(), phone: c.phone.trim() });
    try {
      const sale = await this.orderFormService.create({
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
      });
      this.messages.success(`Order ${sale.saleNumber} saved.`);
      this.router.navigate(['/sales', sale.id]);
    } catch (err) {
      this.saveError.set(errorText(err));
    } finally {
      this.busy.set(false);
    }
  }
}
