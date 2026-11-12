import { Component, OnInit, inject, input, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Alert } from '../../../component/alert/alert';
import { PageHeader } from '../../../component/page-header/page-header';
import { MessageService } from '../../../services/message/message.service';
import { ProductsService } from '../../../services/products/products.service';
import { errorText } from '../../../services/rest/global-rest-service';
import type { Product, ProductCategory } from '../../../shared/interfaces/product.interface';
import { ProductsForm } from '../products-form/products-form.component';

/** /products/:id/edit — edit a product. */
@Component({
  selector: 'app-products-edit',
  imports: [Alert, PageHeader, ProductsForm],
  templateUrl: './products-edit.component.html',
})
export class ProductsEdit implements OnInit {
  /** From the route (:id). */
  readonly id = input.required<string>();
  private readonly service = inject(ProductsService);
  private readonly router = inject(Router);
  private readonly messages = inject(MessageService);
  readonly categories = signal<ProductCategory[]>([]);
  readonly product = signal<Product | null>(null);
  readonly error = signal<string | null>(null);

  async ngOnInit(): Promise<void> {
    try {
      const catalog = await this.service.catalog(true);
      const p = catalog.flatMap((c) => c.products).find((x) => x.id === Number(this.id()));
      if (!p) {
        this.error.set('Product not found.');
        return;
      }
      this.categories.set(catalog);
      this.product.set(p);
    } catch (err) {
      this.error.set(errorText(err, 'Unable to load the product.'));
    }
  }

  saved(name: string): void {
    this.messages.success(`Product ${name} saved.`);
    this.router.navigateByUrl('/products/list');
  }
}
