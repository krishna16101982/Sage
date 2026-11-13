import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { Alert } from '../../../component/alert/alert';
import { CatalogService } from '../../../services/catalog/catalog.service';
import { MessageService } from '../../../services/message/message.service';
import { errorText } from '../../../services/rest/global-rest-service';
import { SalesService } from '../../../services/sales/sales.service';
import { DURATION_UNITS, type CatalogCategory, type DurationUnit, type SalesRules } from '../../../shared/interfaces/catalog.interface';
import type { Customer } from '../../../shared/interfaces/customer.interface';
import type { SaleInput } from '../../../shared/interfaces/sale.interface';
import { addDays, formatDate, renewalDate, today } from '../../../shared/utils/dates';
import { formatDuration, formatInr } from '../../../shared/utils/format';
import { CustomerPicker } from './customer-picker/customer-picker.component';

/** Rounds to paise (2 decimals). */
const r2 = (n: number) => Math.round(n * 100) / 100;

/**
 * /sales/add — the sale page.
 * Category > Product > Plan fills price, cost and duration (read-only). The seller enters EITHER the sale price OR the
 * discount (amount or %); the other is calculated. Renewal date = start + duration, editable. The server recalculates
 * every amount from the price list when the sale is saved.
 */
@Component({
  selector: 'app-sales-add',
  imports: [FormsModule, RouterLink, Alert, CustomerPicker],
  templateUrl: './sales-add.component.html',
})
export class SalesAdd implements OnInit {
  private readonly catalogService = inject(CatalogService);
  private readonly salesService = inject(SalesService);
  private readonly router = inject(Router);
  private readonly messages = inject(MessageService);
  protected readonly inr = formatInr;
  protected readonly date = formatDate;
  protected readonly durationText = formatDuration;
  protected readonly units = DURATION_UNITS;

  readonly catalog = signal<CatalogCategory[] | null>(null);
  /** Company rules (Admin Panel > Settings); the server checks them again when saving. */
  readonly rules = signal<SalesRules>({ maxDiscountPercent: 100, maxBackdateDays: 365, saleNumberPrefix: 'S' });
  /** Earliest start date the company allows. */
  readonly earliestStart = computed(() => addDays(today(), -this.rules().maxBackdateDays));
  readonly loadError = signal<string | null>(null);
  readonly saveError = signal<string | null>(null);
  readonly busy = signal(false);

  // ---- What is sold ----
  readonly categoryId = signal<number | null>(null);
  readonly productId = signal<number | null>(null);
  readonly planId = signal<number | null>(null);
  readonly category = computed(() => this.catalog()?.find((c) => c.id === this.categoryId()) ?? null);
  readonly product = computed(() => this.category()?.products.find((p) => p.id === this.productId()) ?? null);
  readonly plan = computed(() => this.product()?.plans.find((p) => p.id === this.planId()) ?? null);

  // ---- Who buys ----
  readonly customer = signal<Customer | null>(null);

  // ---- Amounts ----
  readonly quantity = signal<number | null>(1);
  /** Which field the seller typed last: the other one is calculated. */
  readonly mode = signal<'sale' | 'discount'>('sale');
  readonly saleInput = signal<number | null>(null);
  readonly discountInput = signal<number | null>(null);

  readonly listAmount = computed(() => r2((this.plan()?.price ?? 0) * (this.quantity() ?? 0)));
  readonly costAmount = computed(() => r2((this.plan()?.cost ?? 0) * (this.quantity() ?? 0)));
  /** null until the seller has entered a sale price or a discount. */
  readonly saleAmount = computed<number | null>(() => {
    if (this.mode() === 'sale') return this.saleInput() === null ? null : r2(this.saleInput()!);
    return this.discountInput() === null ? null : r2(this.listAmount() - this.discountInput()!);
  });
  readonly discountAmount = computed<number | null>(() => (this.saleAmount() === null ? null : r2(this.listAmount() - this.saleAmount()!)));
  readonly discountPercent = computed<number | null>(() =>
    this.discountAmount() === null || this.listAmount() === 0 ? null : r2((this.discountAmount()! / this.listAmount()) * 100),
  );
  readonly margin = computed<number | null>(() => (this.saleAmount() === null ? null : r2(this.saleAmount()! - this.costAmount())));

  // ---- Term ----
  readonly durationUnit = signal<DurationUnit>('Year');
  readonly durationCount = signal<number | null>(1);
  readonly startDate = signal(today());
  /** Set when the seller changes the renewal date by hand. */
  readonly renewalOverride = signal<string | null>(null);
  readonly calculatedRenewal = computed(() => renewalDate(this.startDate(), this.durationUnit(), this.durationCount() ?? 0));
  readonly renewal = computed(() => this.renewalOverride() ?? this.calculatedRenewal());
  notes = '';

  /** Problems shown under the form; the Save button stays disabled while there are any. */
  readonly problems = computed(() => {
    const p: string[] = [];
    if (!this.plan()) p.push('Choose a category, product and plan.');
    if (!this.customer()) p.push('Choose a customer, or add a new one.');
    const q = this.quantity();
    if (q === null || q < 1 || q > 100000 || !Number.isInteger(q)) p.push('Quantity must be a whole number from 1 to 100000.');
    const sale = this.saleAmount();
    if (sale === null) p.push('Enter the sale price or the discount.');
    else if (sale < 0) p.push('The discount cannot be more than the list amount.');
    else if (sale > this.listAmount()) p.push('The sale price cannot be more than the list amount.');
    else if (this.rules().maxDiscountPercent < 100 && (this.discountPercent() ?? 0) > this.rules().maxDiscountPercent)
      p.push(`The discount cannot be more than ${this.rules().maxDiscountPercent}% of the list amount (company limit).`);
    const count = this.durationCount();
    if (count === null || count < 1 || count > 120) p.push('Duration must be from 1 to 120.');
    if (!this.startDate()) p.push('Enter the start date.');
    else if (this.startDate() < this.earliestStart())
      p.push(this.rules().maxBackdateDays === 0 ? 'The start date cannot be in the past (company setting).' : `The start date cannot be more than ${this.rules().maxBackdateDays} days in the past (company setting).`);
    else if (!this.renewal() || this.renewal() <= this.startDate()) p.push('The renewal date must be after the start date.');
    return p;
  });

  async ngOnInit(): Promise<void> {
    try {
      this.catalogService.rules().then((r) => this.rules.set(r), () => undefined);
      const catalog = await this.catalogService.catalog();
      this.catalog.set(catalog);
      if (catalog.length === 1) this.chooseCategory(catalog[0].id);
    } catch (err) {
      this.loadError.set(errorText(err, 'Unable to load the products.'));
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

  /** A new plan brings its own price and duration: amounts already typed are cleared. */
  choosePlan(id: number | null): void {
    this.planId.set(id);
    const plan = this.plan();
    if (plan) {
      this.durationUnit.set(plan.durationUnit);
      this.durationCount.set(plan.durationCount);
    }
    this.renewalOverride.set(null);
    this.saleInput.set(null);
    this.discountInput.set(null);
    this.mode.set('sale');
  }

  setSale(value: number | null): void {
    this.mode.set('sale');
    this.saleInput.set(value);
  }

  setDiscount(value: number | null): void {
    this.mode.set('discount');
    this.discountInput.set(value);
  }

  setDiscountPercent(value: number | null): void {
    this.setDiscount(value === null ? null : r2((this.listAmount() * value) / 100));
  }

  noDiscount(): void {
    this.setSale(this.listAmount());
  }

  setRenewal(value: string): void {
    this.renewalOverride.set(value && value !== this.calculatedRenewal() ? value : null);
  }

  async save(): Promise<void> {
    if (this.problems().length > 0) return;
    this.saveError.set(null);
    this.busy.set(true);
    const body: SaleInput = {
      customerId: this.customer()!.id,
      planId: this.planId()!,
      quantity: this.quantity()!,
      durationUnit: this.durationUnit(),
      durationCount: this.durationCount()!,
      startDate: this.startDate(),
      renewalDate: this.renewalOverride() ?? undefined,
      notes: this.notes.trim() || undefined,
    };
    // Send only the amount the seller typed; the server calculates the other one from the price list.
    if (this.mode() === 'sale') body.saleAmount = this.saleAmount()!;
    else body.discountAmount = r2(this.discountInput()!);
    try {
      const sale = await this.salesService.create(body);
      this.messages.success(`Sale ${sale.saleNumber} recorded.`);
      this.router.navigate(['/sales', sale.id]);
    } catch (err) {
      this.saveError.set(errorText(err));
    } finally {
      this.busy.set(false);
    }
  }
}
