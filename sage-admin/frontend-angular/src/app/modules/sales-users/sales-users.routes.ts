import { Routes } from '@angular/router';
import { PermissionGuard } from '../../services/permission/permission.guard';
import { Perm } from '../../shared/enumarations/permission-codes';

/** /sales-users/... — User Panel accounts (sales persons and sales managers). */
export const salesUserRoutes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'list' },
  {
    path: 'list',
    title: 'Sales Users - Sage Admin',
    data: { access_perms: [Perm.SalesUserView] },
    canActivate: [PermissionGuard],
    loadComponent: () => import('./sales-users-list/sales-users-list.component').then((m) => m.SalesUsersList),
  },
  {
    path: 'add',
    title: 'Add sales user - Sage Admin',
    data: { access_perms: [Perm.SalesUserAdd] },
    canActivate: [PermissionGuard],
    loadComponent: () => import('./sales-users-add/sales-users-add.component').then((m) => m.SalesUsersAdd),
  },
  {
    path: ':id',
    children: [
      {
        path: 'edit',
        title: 'Edit sales user - Sage Admin',
        data: { access_perms: [Perm.SalesUserEdit] },
        canActivate: [PermissionGuard],
        loadComponent: () => import('./sales-users-edit/sales-users-edit.component').then((m) => m.SalesUsersEdit),
      },
      {
        path: 'reset-password',
        title: 'Reset password - Sage Admin',
        data: { access_perms: [Perm.SalesUserResetPassword] },
        canActivate: [PermissionGuard],
        loadComponent: () => import('./sales-users-reset-password/sales-users-reset-password.component').then((m) => m.SalesUsersResetPassword),
      },
    ],
  },
];
