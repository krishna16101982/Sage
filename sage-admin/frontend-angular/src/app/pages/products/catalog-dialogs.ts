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
  template: `
    <app-modal [title]="category() ? 'Edit category' : 'Add category'" (closed)="closed.emit()">
      <form class="form" (ngSubmit)="submit()">
        @if (error()) { <app-alert kind="error">{{ error() }}</app-alert> }
        <label class="field-label">Code <input class="field" name="code" required maxlength="50" placeholder="e.g. CyberSecurity" [disabled]="!!category()" [(ngModel)]="code" /></label>
        <label class="field-label">Name <input class="field" name="name" required maxlength="100" [(ngModel)]="name" /></label>
        <label class="field-label">Description <textarea class="field" name="description" rows="2" maxlength="500" [(ngModel)]="description"></textarea></label>
        <label class="field-label">Sort order <input class="field" name="sortOrder" type="number" [(ngModel)]="sortOrder" /></label>
        @if (category()) {
          <label class="checkbox-label"><input type="checkbox" name="active" [(ngModel)]="active" /> Active <span class="hint">(inactive categories are hidden from the sale page)</span></label>
        }
        <button type="submit" class="btn btn-primary btn-block" [disabled]="busy()">{{ busy() ? 'Please wait...' : category() ? 'Save changes' : 'Create category' }}</button>
      </form>
    </app-modal>
  `,
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
  template: `
    <app-modal [title]="product() ? 'Edit product' : 'Add product'" (closed)="closed.emit()">
      <form class="form" (ngSubmit)="submit()">
        @if (error()) { <app-alert kind="error">{{ error() }}</app-alert> }
        <label class="field-label">Category
          <select class="field" name="categoryId" [(ngModel)]="categoryIdValue">
            @for (c of categoryOptions(); track c.id) { <option [ngValue]="c.id">{{ c.name }}</option> }
          </select>
        </label>
        <label class="field-label">Code <input class="field" name="code" required maxlength="50" placeholder="e.g. GreytHR" [disabled]="!!product()" [(ngModel)]="code" /></label>
        <label class="field-label">Name <input class="field" name="name" required maxlength="100" [(ngModel)]="name" /></label>
        <label class="field-label">Description <textarea class="field" name="description" rows="2" maxlength="500" [(ngModel)]="description"></textarea></label>
        <label class="field-label">Sort order <input class="field" name="sortOrder" type="number" [(ngModel)]="sortOrder" /></label>
        @if (product()) {
          <label class="checkbox-label"><input type="checkbox" name="active" [(ngModel)]="active" /> Active <span class="hint">(inactive products cannot be sold)</span></label>
        }
        <button type="submit" class="btn btn-primary btn-block" [disabled]="busy()">{{ busy() ? 'Please wait...' : product() ? 'Save changes' : 'Create product' }}</button>
      </form>
    </app-modal>
  `,
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
  template: `
    <app-modal [title]="(plan() ? 'Edit plan: ' : 'Add plan: ') + product().name" (closed)="closed.emit()">
      <form class="form" (ngSubmit)="submit()">
        @if (error()) { <app-alert kind="error">{{ error() }}</app-alert> }
        <label class="field-label">Plan name <input class="field" name="name" required maxlength="100" placeholder="e.g. Business Yearly" [(ngModel)]="name" /></label>
        <label class="field-label">Description <textarea class="field" name="description" rows="2" maxlength="500" [(ngModel)]="description"></textarea></label>
        <div class="form-grid-2">
          <label class="field-label">Duration <input class="field" name="durationCount" type="number" min="1" max="120" required [(ngModel)]="durationCount" /></label>
          <label class="field-label">Unit
            <select class="field" name="durationUnit" [(ngModel)]="durationUnit">
              @for (u of units; track u) { <option [value]="u">{{ u }}</option> }
            </select>
          </label>
          <label class="field-label">Price (₹ per unit)
            <input class="field" name="price" type="number" min="0" step="0.01" required [ngModel]="price()" (ngModelChange)="price.set($event)" />
          </label>
          <label class="field-label">Cost (₹ per unit)
            <input class="field" name="cost" type="number" min="0" step="0.01" required [ngModel]="cost()" (ngModelChange)="cost.set($event)" />
          </label>
        </div>
        @if (margin(); as m) {
          <p class="text-sm" [class.text-danger]="m.value < 0" [class.text-muted]="m.value >= 0">
            Margin per unit: {{ m.text }}{{ m.percent !== null ? ' (' + m.percent + '%)' : '' }}
          </p>
        }
        @if (plan()) { <p class="hint">Changing price or cost affects new sales only. Existing sales keep the price they were sold at.</p> }
        <label class="field-label">Sort order <input class="field" name="sortOrder" type="number" [(ngModel)]="sortOrder" /></label>
        @if (plan()) {
          <label class="checkbox-label"><input type="checkbox" name="active" [(ngModel)]="active" /> Active <span class="hint">(inactive plans cannot be sold)</span></label>
        }
        <button type="submit" class="btn btn-primary btn-block" [disabled]="busy()">{{ busy() ? 'Please wait...' : plan() ? 'Save changes' : 'Create plan' }}</button>
      </form>
    </app-modal>
  `,
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
