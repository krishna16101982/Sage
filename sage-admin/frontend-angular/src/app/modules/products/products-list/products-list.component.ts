import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Alert } from '../../../component/alert/alert';
import { CanDirective } from '../../../directives/can.directive';
import { MessageService } from '../../../services/message/message.service';
import { ProductsService } from '../../../services/products/products.service';
import { errorText } from '../../../services/rest/global-rest-service';
import type { ProductCategory } from '../../../shared/interfaces/product.interface';
import { Perm } from '../../../shared/enumarations/permission-codes';
import { formatDuration, formatInr } from '../../../shared/utils/format';

/** /products/list — the catalog: categories, their products and each product's plans (price list). */
@Component({
  selector: 'app-products-list',
  imports: [FormsModule, RouterLink, CanDirective, Alert],
  templateUrl: './products-list.component.html',
})
export class ProductsList implements OnInit {
  private readonly service = inject(ProductsService);
  protected readonly perm = Perm;
  protected readonly inr = formatInr;
  protected readonly duration = formatDuration;

  readonly showInactive = signal(true);
  readonly catalog = signal<ProductCategory[] | null>(null);
  readonly expanded = signal<Set<number>>(new Set());
  readonly error = signal<string | null>(null);
  readonly notice = signal<string | null>(inject(MessageService).take());

  ngOnInit(): void {
    this.load();
  }

  async load(): Promise<void> {
    try {
      this.catalog.set(await this.service.catalog(this.showInactive()));
      this.error.set(null);
    } catch (err) {
      this.error.set(errorText(err, 'Unable to load the product catalog.'));
    }
  }

  isOpen(productId: number): boolean {
    return this.expanded().has(productId);
  }

  toggle(productId: number): void {
    const next = new Set(this.expanded());
    if (next.has(productId)) next.delete(productId);
    else next.add(productId);
    this.expanded.set(next);
  }
}
