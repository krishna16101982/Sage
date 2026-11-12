import { Component, OnInit, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Alert } from '../../../component/alert/alert';
import { ProductsService } from '../../../services/products/products.service';
import { errorText } from '../../../services/rest/global-rest-service';
import type { ProductCategory } from '../../../shared/interfaces/product.interface';

/** Category form used by the Add page (no `category`) and the Edit page. The code is fixed after creation. */
@Component({
  selector: 'app-product-categories-form',
  imports: [FormsModule, RouterLink, Alert],
  templateUrl: './product-categories-form.component.html',
})
export class ProductCategoriesForm implements OnInit {
  readonly category = input<ProductCategory>();
  /** Emits the category name after saving. */
  readonly saved = output<string>();
  private readonly service = inject(ProductsService);
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
      if (c) await this.service.updateCategory(c.id, { ...base, isActive: this.active });
      else await this.service.createCategory({ ...base, code: this.code });
      this.saved.emit(this.name);
    } catch (err) {
      this.error.set(errorText(err));
    } finally {
      this.busy.set(false);
    }
  }
}
