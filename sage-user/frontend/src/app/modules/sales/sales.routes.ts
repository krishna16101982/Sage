import { Routes } from '@angular/router';
import { managerGuard } from '../../services/auth/auth.guard';

/** /sales/... — New sale, New order form, Order forms, Team order forms (managers) and one sale's details. */
export const saleRoutes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'list' },
  {
    path: 'add',
    title: 'New sale - Sage Sales',
    loadComponent: () => import('./sales-add/sales-add.component').then((m) => m.SalesAdd),
  },
  {
    path: 'order-form',
    title: 'New order form - Sage Sales',
    loadComponent: () => import('./sales-order-form/sales-order-form.component').then((m) => m.SalesOrderForm),
  },
  {
    path: 'list',
    title: 'Order forms - Sage Sales',
    data: { scope: 'mine' },
    loadComponent: () => import('./sales-list/sales-list.component').then((m) => m.SalesList),
  },
  {
    path: 'team',
    title: 'Team order forms - Sage Sales',
    data: { scope: 'team' },
    canActivate: [managerGuard],
    loadComponent: () => import('./sales-list/sales-list.component').then((m) => m.SalesList),
  },
  {
    // Edit an order saved with the order form (the seller or their manager; the API checks it may still be edited).
    path: ':id/edit',
    title: 'Edit order - Sage Sales',
    loadComponent: () => import('./sales-order-form/sales-order-form.component').then((m) => m.SalesOrderForm),
  },
  {
    path: ':id',
    title: 'Sale - Sage Sales',
    loadComponent: () => import('./sales-view/sales-view.component').then((m) => m.SalesView),
  },
];
