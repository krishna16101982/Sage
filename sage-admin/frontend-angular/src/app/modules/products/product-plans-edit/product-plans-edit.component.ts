import { Component, OnInit, inject, input, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Alert } from '../../../component/alert/alert';
import { PageHeader } from '../../../component/page-header/page-header';
import { MessageService } from '../../../services/message/message.service';
import { ProductsService } from '../../../services/products/products.service';
import { errorText } from '../../../services/rest/global-rest-service';
import type { Product, ProductPlan } from '../../../shared/interfaces/product.interface';
import { ProductPlansForm } from '../product-plans-form/product-plans-form.component';

/** /products/plans/:id/edit — edit a plan's details, price and cost. */
@Component({
  selector: 'app-product-plans-edit',
  imports: [Alert, PageHeader, ProductPlansForm],
  templateUrl: './product-plans-edit.component.html',
})
export class ProductPlansEdit implements OnInit {
  /** From the route (:id). */
  readonly id = input.required<string>();
  private readonly service = inject(ProductsService);
  private readonly router = inject(Router);
  private readonly messages = inject(MessageService);
  readonly found = signal<{ product: Product; plan: ProductPlan } | null>(null);
  readonly error = signal<string | null>(null);

  async ngOnInit(): Promise<void> {
    try {
      const f = await this.service.findPlan(Number(this.id()));
      if (f) this.found.set(f);
      else this.error.set('Plan not found.');
    } catch (err) {
      this.error.set(errorText(err, 'Unable to load the plan.'));
    }
  }

  saved(name: string): void {
    this.messages.success(`Plan ${name} saved.`);
    this.router.navigateByUrl('/products/list');
  }
}
