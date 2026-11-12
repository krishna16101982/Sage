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
  template: `
    <div class="stack">
      <div class="row">
        <h1 class="page-title">Products</h1>
        <div class="spacer"></div>
        <label class="checkbox-label">
          <input type="checkbox" [ngModel]="showInactive()" (ngModelChange)="showInactive.set($event); load()" /> Show inactive
        </label>
        <button *appCan="perm.ProductAdd" type="button" class="btn btn-primary" (click)="dialog.set({ kind: 'category' })">Add category</button>
      </div>

      @if (error()) { <app-alert kind="error">{{ error() }}</app-alert> }
      @if (!catalog() && !error()) { <p class="text-sm text-muted">Loading...</p> }
      @if (catalog()?.length === 0) { <p class="text-sm text-muted">No categories yet.</p> }

      @for (c of catalog() ?? []; track c.id) {
        <section class="card">
          <header class="card-header">
            <div>
              <h2 class="section-title">{{ c.name }} @if (!c.isActive) { <span class="badge">inactive</span> }</h2>
              @if (c.description) { <p class="hint">{{ c.description }}</p> }
            </div>
            <div class="spacer"></div>
            <button *appCan="perm.ProductEdit" type="button" class="link-button text-sm" (click)="dialog.set({ kind: 'category', category: c })">Edit category</button>
            @if (c.isActive) {
              <button *appCan="perm.ProductAdd" type="button" class="link-button text-sm" (click)="dialog.set({ kind: 'product', categoryId: c.id })">Add product</button>
            }
          </header>

          @if (c.products.length === 0) {
            <p class="card-body text-sm text-muted">No products in this category.</p>
          } @else {
            <ul class="card-list">
              @for (p of c.products; track p.id) {
                <li>
                  <div class="row">
                    <button type="button" class="link-button row-tight" style="text-decoration: none" [attr.aria-expanded]="isOpen(p.id)" (click)="toggle(p.id)">
                      <span class="text-muted">{{ isOpen(p.id) ? '▾' : '▸' }}</span>
                      <span class="font-medium">{{ p.name }}</span>
                      <span class="code hint">{{ p.code }}</span>
                      @if (!p.isActive) { <span class="badge">inactive</span> }
                    </button>
                    <span class="hint">{{ p.plans.length }} plan{{ p.plans.length === 1 ? '' : 's' }}</span>
                    <div class="spacer"></div>
                    <button *appCan="perm.ProductEdit" type="button" class="link-button" (click)="dialog.set({ kind: 'product', categoryId: c.id, product: p })">Edit</button>
                    @if (p.isActive) {
                      <button *appCan="perm.ProductAdd" type="button" class="link-button" (click)="dialog.set({ kind: 'plan', product: p })">Add plan</button>
                    }
                  </div>

                  @if (isOpen(p.id)) {
                    <div class="table-wrap mt-2 ml-5">
                      <table class="data-table">
                        <thead>
                          <tr><th>Plan</th><th>Duration</th><th class="amount">Price</th><th class="amount">Cost</th><th class="amount">Margin</th><th></th></tr>
                        </thead>
                        <tbody>
                          @for (plan of p.plans; track plan.id) {
                            <tr>
                              <td>
                                {{ plan.name }} @if (!plan.isActive) { <span class="badge">inactive</span> }
                                @if (plan.description) { <div class="hint">{{ plan.description }}</div> }
                              </td>
                              <td>{{ duration(plan.durationCount, plan.durationUnit) }}</td>
                              <td class="amount">{{ inr(plan.price) }}</td>
                              <td class="amount">{{ inr(plan.cost) }}</td>
                              <td class="amount" [class.text-danger]="plan.price - plan.cost < 0">{{ inr(plan.price - plan.cost) }}</td>
                              <td class="actions">
                                <button *appCan="perm.ProductEdit" type="button" class="link-button" (click)="dialog.set({ kind: 'plan', product: p, plan: plan })">Edit</button>
                              </td>
                            </tr>
                          } @empty {
                            <tr><td colspan="6" class="table-empty">No plans yet. A product needs at least one active plan before it can be sold.</td></tr>
                          }
                        </tbody>
                      </table>
                    </div>
                  }
                </li>
              }
            </ul>
          }
        </section>
      }
    </div>

    @switch (dialog().kind) {
      @case ('category') { <app-category-form-dialog [category]="dialogCategory()" (closed)="closeDialog()" (saved)="saved()" /> }
      @case ('product') {
        <app-product-form-dialog [product]="dialogProduct()" [categoryId]="dialogCategoryId()" [categories]="catalog() ?? []"
                                 (closed)="closeDialog()" (saved)="saved()" />
      }
      @case ('plan') { <app-plan-form-dialog [product]="dialogPlanProduct()!" [plan]="dialogPlan()" (closed)="closeDialog()" (saved)="saved()" /> }
    }
  `,
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
