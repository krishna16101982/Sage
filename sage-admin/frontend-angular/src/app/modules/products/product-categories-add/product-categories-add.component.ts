import { Component, inject } from '@angular/core';
import { Router } from '@angular/router';
import { PageHeader } from '../../../component/page-header/page-header';
import { MessageService } from '../../../services/message/message.service';
import { ProductCategoriesForm } from '../product-categories-form/product-categories-form.component';

/** /products/categories/add — create a product category. */
@Component({
  selector: 'app-product-categories-add',
  imports: [PageHeader, ProductCategoriesForm],
  templateUrl: './product-categories-add.component.html',
})
export class ProductCategoriesAdd {
  private readonly router = inject(Router);
  private readonly messages = inject(MessageService);

  saved(name: string): void {
    this.messages.success(`Category ${name} created.`);
    this.router.navigateByUrl('/products/list');
  }
}
