import { Component, OnInit, inject, input, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Alert } from '../../../component/alert/alert';
import { PageHeader } from '../../../component/page-header/page-header';
import { MessageService } from '../../../services/message/message.service';
import { ProductsService } from '../../../services/products/products.service';
import { errorText } from '../../../services/rest/global-rest-service';
import type { ProductCategory } from '../../../shared/interfaces/product.interface';
import { ProductCategoriesForm } from '../product-categories-form/product-categories-form.component';

/** /products/categories/:id/edit — edit a category. */
@Component({
  selector: 'app-product-categories-edit',
  imports: [Alert, PageHeader, ProductCategoriesForm],
  templateUrl: './product-categories-edit.component.html',
})
export class ProductCategoriesEdit implements OnInit {
  /** From the route (:id). */
  readonly id = input.required<string>();
  private readonly service = inject(ProductsService);
  private readonly router = inject(Router);
  private readonly messages = inject(MessageService);
  readonly category = signal<ProductCategory | null>(null);
  readonly error = signal<string | null>(null);

  async ngOnInit(): Promise<void> {
    try {
      const c = await this.service.findCategory(Number(this.id()));
      if (c) this.category.set(c);
      else this.error.set('Category not found.');
    } catch (err) {
      this.error.set(errorText(err, 'Unable to load the category.'));
    }
  }

  saved(name: string): void {
    this.messages.success(`Category ${name} saved.`);
    this.router.navigateByUrl('/products/list');
  }
}
