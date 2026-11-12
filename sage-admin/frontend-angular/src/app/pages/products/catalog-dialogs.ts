import { Component, OnInit, computed, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ProductsApi } from '../../core/admin-api';
import { errorText } from '../../core/api-client';
import { formatInr } from '../../core/format';
import { DURATION_UNITS, type DurationUnit, type Product, type ProductCategory, type ProductPlan } from '../../core/models/product';
import { AlertComponent, ModalComponent } from '../../shared/ui';

/** Create (no `category`) or edit a category. Code only on create. */
@Component({
  selector: 'app-category-form-dialog',
  imports: [FormsModule, ModalComponent, AlertComponent],
  templateUrl: './category-form.dialog.html',
})
export class CategoryFormDialog implements OnInit {
  readonly category = input<ProductCategory>();
  readonly closed = output<void>();
  readonly saved = output<void>();
  private readonly api = inject(ProductsApi);
  code = '';
  name = '';
  description = '';
  sortOrder = 0;
  active = true;
  readonly error = signal<string | null>(null);
  readonly busy = signal(false);

  ngOnInit(): void {
    const c = this.category();
    if (c) {
      this.code = c.code;
      this.name = c.name;
      this.description = c.description ?? '';
      this.sortOrder = c.sortOrder;
      this.active = c.isActive;
    }
  }

  async submit(): Promise<void> {
    this.error.set(null);
    this.busy.set(true);
    try {
      const c = this.category();
      const base = { name: this.name, description: this.description || undefined, sortOrder: Number(this.sortOrder) || 0 };
      if (c) await this.api.updateCategory(c.id, { ...base, isActive: this.active });
      else await this.api.createCategory({ ...base, code: this.code });
      this.saved.emit();
    } catch (err) {
      this.error.set(errorText(err));
    } finally {
      this.busy.set(false);
    }
  }
}

/** Create a product in `categoryId`, or edit `product` (which may move to another active category). */
@Component({
  selector: 'app-product-form-dialog',
  imports: [FormsModule, ModalComponent, AlertComponent],
  templateUrl: './product-form.dialog.html',
})
export class ProductFormDialog implements OnInit {
  readonly product = input<Product>();
  readonly categoryId = input.required<number>();
  readonly categories = input.required<ProductCategory[]>();
  readonly closed = output<void>();
  readonly saved = output<void>();
  private readonly api = inject(ProductsApi);
  categoryIdValue = 0;
  code = '';
  name = '';
  description = '';
  sortOrder = 0;
  active = true;
  readonly error = signal<string | null>(null);
  readonly busy = signal(false);
  /** Active categories, plus the current one even if it is inactive. */
  readonly categoryOptions = computed(() => this.categories().filter((c) => c.isActive || c.id === (this.product()?.categoryId ?? this.categoryId())));

  ngOnInit(): void {
    const p = this.product();
    this.categoryIdValue = p?.categoryId ?? this.categoryId();
    if (p) {
      this.code = p.code;
      this.name = p.name;
      this.description = p.description ?? '';
      this.sortOrder = p.sortOrder;
      this.active = p.isActive;
    }
  }

  async submit(): Promise<void> {
    this.error.set(null);
    this.busy.set(true);
    try {
      const p = this.product();
      const base = { categoryId: this.categoryIdValue, name: this.name, description: this.description || undefined, sortOrder: Number(this.sortOrder) || 0 };
      if (p) await this.api.updateProduct(p.id, { ...base, isActive: this.active });
      else await this.api.createProduct({ ...base, code: this.code });
      this.saved.emit();
    } catch (err) {
      this.error.set(errorText(err));
    } finally {
      this.busy.set(false);
    }
  }
}

/** Create a plan for `product`, or edit `plan`. Price and cost are per unit (INR) for one plan term. */
@Component({
  selector: 'app-plan-form-dialog',
  imports: [FormsModule, ModalComponent, AlertComponent],
  templateUrl: './plan-form.dialog.html',
})
export class PlanFormDialog implements OnInit {
  readonly product = input.required<Product>();
  readonly plan = input<ProductPlan>();
  readonly closed = output<void>();
  readonly saved = output<void>();
  private readonly api = inject(ProductsApi);
  protected readonly units = DURATION_UNITS;
  name = '';
  description = '';
  durationUnit: DurationUnit = 'Year';
  durationCount = 1;
  sortOrder = 0;
  active = true;
  readonly price = signal<number | null>(null);
  readonly cost = signal<number | null>(null);
  readonly error = signal<string | null>(null);
  readonly busy = signal(false);

  /** Live margin preview while typing price and cost. */
  readonly margin = computed(() => {
    const price = this.price();
    const cost = this.cost();
    if (price === null || cost === null || `${price}` === '' || `${cost}` === '') return null;
    const value = Number(price) - Number(cost);
    return { value, text: formatInr(value), percent: Number(price) > 0 ? ((value / Number(price)) * 100).toFixed(1) : null };
  });

  ngOnInit(): void {
    const p = this.plan();
    if (p) {
      this.name = p.name;
      this.description = p.description ?? '';
      this.durationUnit = p.durationUnit;
      this.durationCount = p.durationCount;
      this.price.set(p.price);
      this.cost.set(p.cost);
      this.sortOrder = p.sortOrder;
      this.active = p.isActive;
    }
  }

  async submit(): Promise<void> {
    this.error.set(null);
    this.busy.set(true);
    try {
      const data = {
        name: this.name,
        description: this.description || undefined,
        durationUnit: this.durationUnit,
        durationCount: Number(this.durationCount) || 0,
        price: Number(this.price()),
        cost: Number(this.cost()),
        sortOrder: Number(this.sortOrder) || 0,
      };
      const p = this.plan();
      if (p) await this.api.updatePlan(p.id, { ...data, isActive: this.active });
      else await this.api.createPlan(this.product().id, data);
      this.saved.emit();
    } catch (err) {
      this.error.set(errorText(err));
    } finally {
      this.busy.set(false);
    }
  }
}
