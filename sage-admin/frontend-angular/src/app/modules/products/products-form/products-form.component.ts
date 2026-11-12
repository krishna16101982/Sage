import { Component, OnInit, computed, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Alert } from '../../../component/alert/alert';
import { ProductsService } from '../../../services/products/products.service';
import { errorText } from '../../../services/rest/global-rest-service';
import type { Product, ProductCategory } from '../../../shared/interfaces/product.interface';

/** Product form: create a product in `categoryId`, or edit `product` (which may move to another active category). */
@Component({
  selector: 'app-products-form',
  imports: [FormsModule, RouterLink, Alert],
  templateUrl: './products-form.component.html',
})
export class ProductsForm implements OnInit {
  readonly product = input<Product>();
  readonly categoryId = input<number>(0);
  readonly categories = input.required<ProductCategory[]>();
  /** Emits the product name after saving. */
  readonly saved = output<string>();
  private readonly service = inject(ProductsService);
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
    this.categoryIdValue = p?.categoryId ?? (this.categoryId() || this.categoryOptions()[0]?.id || 0);
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
      if (p) await this.service.updateProduct(p.id, { ...base, isActive: this.active });
      else await this.service.createProduct({ ...base, code: this.code });
      this.saved.emit(this.name);
    } catch (err) {
      this.error.set(errorText(err));
    } finally {
      this.busy.set(false);
    }
  }
}
