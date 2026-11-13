import { Routes } from '@angular/router';

/** /customers/... — shared customer list and Add customer. */
export const customerRoutes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'list' },
  {
    path: 'list',
    title: 'Customers - Sage Sales',
    loadComponent: () => import('./customers-list/customers-list.component').then((m) => m.CustomersList),
  },
  {
    path: 'add',
    title: 'Add customer - Sage Sales',
    loadComponent: () => import('./customers-add/customers-add.component').then((m) => m.CustomersAdd),
  },
];
