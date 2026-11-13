import { Routes } from '@angular/router';
import { PermissionGuard } from '../../services/permission/permission.guard';
import { Perm } from '../../shared/enumarations/permission-codes';

/** /customers/... — customers (shared with the User Panel). */
export const customerRoutes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'list' },
  {
    path: 'list',
    title: 'Customers - Sage Admin',
    data: { access_perms: [Perm.CustomerView] },
    canActivate: [PermissionGuard],
    loadComponent: () => import('./customers-list/customers-list.component').then((m) => m.CustomersList),
  },
  {
    path: 'add',
    title: 'Add customer - Sage Admin',
    data: { access_perms: [Perm.CustomerAdd] },
    canActivate: [PermissionGuard],
    loadComponent: () => import('./customers-add/customers-add.component').then((m) => m.CustomersAdd),
  },
  {
    path: ':id/edit',
    title: 'Edit customer - Sage Admin',
    data: { access_perms: [Perm.CustomerEdit] },
    canActivate: [PermissionGuard],
    loadComponent: () => import('./customers-edit/customers-edit.component').then((m) => m.CustomersEdit),
  },
];
