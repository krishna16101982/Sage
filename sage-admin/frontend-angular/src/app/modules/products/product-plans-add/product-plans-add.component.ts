import { Component, OnInit, inject, input, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Alert } from '../../../component/alert/alert';
import { PageHeader } from '../../../component/page-header/page-header';
import { MessageService } from '../../../services/message/message.service';
import { ProductsService } from '../../../services/products/products.service';
import { errorText } from '../../../services/rest/global-rest-service';
import type { Product } from '../../../shared/interfaces/product.interface';
import { ProductPlansForm } from '../product-plans-form/product-plans-form.component';

/** /products/:productId/plans/add — add a plan (price list entry) to a product. */
@Component({
  selector: 'app-product-plans-add',
  imports: [Alert, PageHeader, ProductPlansForm],
  templateUrl: './product-plans-add.component.html',
})
export class ProductPlansAdd implements OnInit {
  /** From the route (:productId). */
  readonly productId = input.required<string>();
  private readonly service = inject(ProductsService);
  private readonly router = inject(Router);
  private readonly messages = inject(MessageService);
  readonly product = signal<Product | null>(null);
  readonly error = signal<string | null>(null);

  async ngOnInit(): Promise<void> {
    try {
      const p = await this.service.findProduct(Number(this.productId()));
      if (p) this.product.set(p);
      else this.error.set('Product not found.');
    } catch (err) {
      this.error.set(errorText(err, 'Unable to load the product.'));
    }
  }

  saved(name: string): void {
    this.messages.success(`Plan ${name} created.`);
    this.router.navigateByUrl('/products/list');
  }
}
