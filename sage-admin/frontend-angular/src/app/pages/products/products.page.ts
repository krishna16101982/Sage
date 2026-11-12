import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ProductsApi } from '../../core/admin-api';
import { errorText } from '../../core/api-client';
import { CanDirective } from '../../core/can.directive';
import { formatDuration, formatInr } from '../../core/format';
import type { Product, ProductCategory, ProductPlan } from '../../core/models/product';
import { Perm } from '../../core/permissionCodes';
import { AlertComponent } from '../../shared/ui';
import { CategoryFormDialog, PlanFormDialog, ProductFormDialog } from './catalog-dialogs';

type Dialog =
  | { kind: 'none' }
  | { kind: 'category'; category?: ProductCategory }
  | { kind: 'product'; categoryId: number; product?: Product }
  | { kind: 'plan'; product: Product; plan?: ProductPlan };

/** Product catalog: categories, their products and each product's plans (price list). */
@Component({
  selector: 'app-products-page',
  imports: [FormsModule, CanDirective, AlertComponent, CategoryFormDialog, ProductFormDialog, PlanFormDialog],
  templateUrl: './products.page.html',
})
export class ProductsPage implements OnInit {
  private readonly api = inject(ProductsApi);
  protected readonly perm = Perm;
  protected readonly inr = formatInr;
  protected readonly duration = formatDuration;

  readonly showInactive = signal(true);
  readonly catalog = signal<ProductCategory[] | null>(null);
  readonly expanded = signal<Set<number>>(new Set());
  readonly error = signal<string | null>(null);
  readonly dialog = signal<Dialog>({ kind: 'none' });

  ngOnInit(): void {
    this.load();
  }

  async load(): Promise<void> {
    try {
      this.catalog.set(await this.api.catalog(this.showInactive()));
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

  closeDialog(): void {
    this.dialog.set({ kind: 'none' });
  }

  saved(): void {
    this.closeDialog();
    this.load();
  }

  dialogCategory(): ProductCategory | undefined {
    const d = this.dialog();
    return d.kind === 'category' ? d.category : undefined;
  }

  dialogProduct(): Product | undefined {
    const d = this.dialog();
    return d.kind === 'product' ? d.product : undefined;
  }

  dialogCategoryId(): number {
    const d = this.dialog();
    return d.kind === 'product' ? d.categoryId : 0;
  }

  dialogPlanProduct(): Product | undefined {
    const d = this.dialog();
    return d.kind === 'plan' ? d.product : undefined;
  }

  dialogPlan(): ProductPlan | undefined {
    const d = this.dialog();
    return d.kind === 'plan' ? d.plan : undefined;
  }
}
