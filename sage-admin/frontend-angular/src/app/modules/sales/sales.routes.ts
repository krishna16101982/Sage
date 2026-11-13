import { Routes } from '@angular/router';
import { PermissionGuard } from '../../services/permission/permission.guard';
import { Perm } from '../../shared/enumarations/permission-codes';

/** /sales/... — sales recorded in the User Panel (read-only): list, summary by person / manager, one sale. */
export const saleRoutes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'list' },
  {
    path: 'list',
    title: 'Sales - Sage Admin',
    data: { access_perms: [Perm.SalesView] },
    canActivate: [PermissionGuard],
    loadComponent: () => import('./sales-list/sales-list.component').then((m) => m.SalesList),
  },
  {
    path: 'summary',
    title: 'Sales summary - Sage Admin',
    data: { access_perms: [Perm.SalesView] },
    canActivate: [PermissionGuard],
    loadComponent: () => import('./sales-summary/sales-summary.component').then((m) => m.SalesSummary),
  },
  {
    path: ':id',
    title: 'Sale - Sage Admin',
    data: { access_perms: [Perm.SalesView] },
    canActivate: [PermissionGuard],
    loadComponent: () => import('./sales-view/sales-view.component').then((m) => m.SalesView),
  },
];
