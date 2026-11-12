import { Component, OnInit, inject, input, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Alert } from '../../../component/alert/alert';
import { PageHeader } from '../../../component/page-header/page-header';
import { MessageService } from '../../../services/message/message.service';
import { ProductsService } from '../../../services/products/products.service';
import { errorText } from '../../../services/rest/global-rest-service';
import type { ProductCategory } from '../../../shared/interfaces/product.interface';
import { ProductsForm } from '../products-form/products-form.component';

/** /products/add?categoryId= — create a product (the category is preselected from the list's "Add product" link). */
@Component({
  selector: 'app-products-add',
  imports: [Alert, PageHeader, ProductsForm],
  templateUrl: './products-add.component.html',
})
export class ProductsAdd implements OnInit {
  /** From the query string (?categoryId=). */
  readonly categoryId = input<string>();
  private readonly service = inject(ProductsService);
  private readonly router = inject(Router);
  private readonly messages = inject(MessageService);
  readonly categories = signal<ProductCategory[] | null>(null);
  readonly error = signal<string | null>(null);

  async ngOnInit(): Promise<void> {
    try {
      this.categories.set(await this.service.catalog(true));
    } catch (err) {
      this.error.set(errorText(err, 'Unable to load categories.'));
    }
  }

  saved(name: string): void {
    this.messages.success(`Product ${name} created.`);
    this.router.navigateByUrl('/products/list');
  }
}
