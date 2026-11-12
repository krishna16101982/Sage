import { Routes } from '@angular/router';
import { PermissionGuard } from '../../services/permission/permission.guard';
import { Perm } from '../../shared/enumarations/permission-codes';

/** /products/... — the catalog (categories > products > plans) and an Add/Edit page for each level. */
export const productRoutes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'list' },
  {
    path: 'list',
    title: 'Products - Sage Admin',
    data: { access_perms: [Perm.ProductView] },
    canActivate: [PermissionGuard],
    loadComponent: () => import('./products-list/products-list.component').then((m) => m.ProductsList),
  },

  // Categories
  {
    path: 'categories/add',
    title: 'Add category - Sage Admin',
    data: { access_perms: [Perm.ProductAdd] },
    canActivate: [PermissionGuard],
    loadComponent: () => import('./product-categories-add/product-categories-add.component').then((m) => m.ProductCategoriesAdd),
  },
  {
    path: 'categories/:id/edit',
    title: 'Edit category - Sage Admin',
    data: { access_perms: [Perm.ProductEdit] },
    canActivate: [PermissionGuard],
    loadComponent: () => import('./product-categories-edit/product-categories-edit.component').then((m) => m.ProductCategoriesEdit),
  },

  // Plans
  {
    path: 'plans/:id/edit',
    title: 'Edit plan - Sage Admin',
    data: { access_perms: [Perm.ProductEdit] },
    canActivate: [PermissionGuard],
    loadComponent: () => import('./product-plans-edit/product-plans-edit.component').then((m) => m.ProductPlansEdit),
  },

  // Products (add takes ?categoryId=)
  {
    path: 'add',
    title: 'Add product - Sage Admin',
    data: { access_perms: [Perm.ProductAdd] },
    canActivate: [PermissionGuard],
    loadComponent: () => import('./products-add/products-add.component').then((m) => m.ProductsAdd),
  },
  {
    path: ':id/edit',
    title: 'Edit product - Sage Admin',
    data: { access_perms: [Perm.ProductEdit] },
    canActivate: [PermissionGuard],
    loadComponent: () => import('./products-edit/products-edit.component').then((m) => m.ProductsEdit),
  },
  {
    path: ':productId/plans/add',
    title: 'Add plan - Sage Admin',
    data: { access_perms: [Perm.ProductAdd] },
    canActivate: [PermissionGuard],
    loadComponent: () => import('./product-plans-add/product-plans-add.component').then((m) => m.ProductPlansAdd),
  },
];
