import { Routes } from '@angular/router';
import { managerGuard } from '../../services/auth/auth.guard';

/** /sales/... — New sale, New order form, My sales, Team sales (managers) and one sale's details. */
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
    title: 'My sales - Sage Sales',
    data: { scope: 'mine' },
    loadComponent: () => import('./sales-list/sales-list.component').then((m) => m.SalesList),
  },
  {
    path: 'team',
    title: 'Team sales - Sage Sales',
    data: { scope: 'team' },
    canActivate: [managerGuard],
    loadComponent: () => import('./sales-list/sales-list.component').then((m) => m.SalesList),
  },
  {
    path: ':id',
    title: 'Sale - Sage Sales',
    loadComponent: () => import('./sales-view/sales-view.component').then((m) => m.SalesView),
  },
];
