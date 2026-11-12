import { Component, OnInit, computed, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Alert } from '../../../component/alert/alert';
import { ProductsService } from '../../../services/products/products.service';
import { errorText } from '../../../services/rest/global-rest-service';
import { DURATION_UNITS, type DurationUnit, type Product, type ProductPlan } from '../../../shared/interfaces/product.interface';
import { formatInr } from '../../../shared/utils/format';

/** Plan form: create a plan for `product`, or edit `plan`. Price and cost are per unit (INR) for one plan term. */
@Component({
  selector: 'app-product-plans-form',
  imports: [FormsModule, RouterLink, Alert],
  templateUrl: './product-plans-form.component.html',
})
export class ProductPlansForm implements OnInit {
  readonly product = input.required<Product>();
  readonly plan = input<ProductPlan>();
  /** Emits the plan name after saving. */
  readonly saved = output<string>();
  private readonly service = inject(ProductsService);
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
      if (p) await this.service.updatePlan(p.id, { ...data, isActive: this.active });
      else await this.service.createPlan(this.product().id, data);
      this.saved.emit(this.name);
    } catch (err) {
      this.error.set(errorText(err));
    } finally {
      this.busy.set(false);
    }
  }
}
