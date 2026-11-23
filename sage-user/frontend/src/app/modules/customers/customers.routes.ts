import { Routes } from '@angular/router';

/** /customers/... — shared company list, Add company, and one company (view / edit; the name is read-only). */
export const customerRoutes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'list' },
  {
    path: 'list',
    title: 'Companies - Sage Sales',
    loadComponent: () => import('./customers-list/customers-list.component').then((m) => m.CustomersList),
  },
  {
    path: 'add',
    title: 'Add company - Sage Sales',
    loadComponent: () => import('./customers-add/customers-add.component').then((m) => m.CustomersAdd),
  },
  {
    path: ':id/edit',
    title: 'Edit company - Sage Sales',
    data: { mode: 'edit' },
    loadComponent: () => import('./customers-edit/customers-edit.component').then((m) => m.CustomersEdit),
  },
  {
    path: ':id',
    title: 'Company - Sage Sales',
    data: { mode: 'view' },
    loadComponent: () => import('./customers-edit/customers-edit.component').then((m) => m.CustomersEdit),
  },
];
