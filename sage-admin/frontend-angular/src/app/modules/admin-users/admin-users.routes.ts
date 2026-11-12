import { Routes } from '@angular/router';
import { PermissionGuard } from '../../services/permission/permission.guard';
import { Perm } from '../../shared/enumarations/permission-codes';

/** /users/... — Admin Panel accounts. */
export const adminUserRoutes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'list' },
  {
    path: 'list',
    title: 'Admin Users - Sage Admin',
    data: { access_perms: [Perm.AdminUserView] },
    canActivate: [PermissionGuard],
    loadComponent: () => import('./admin-users-list/admin-users-list.component').then((m) => m.AdminUsersList),
  },
  {
    path: 'add',
    title: 'Add admin user - Sage Admin',
    data: { access_perms: [Perm.AdminUserAdd] },
    canActivate: [PermissionGuard],
    loadComponent: () => import('./admin-users-add/admin-users-add.component').then((m) => m.AdminUsersAdd),
  },
  {
    path: ':id',
    children: [
      {
        path: 'edit',
        title: 'Edit admin user - Sage Admin',
        data: { access_perms: [Perm.AdminUserEdit] },
        canActivate: [PermissionGuard],
        loadComponent: () => import('./admin-users-edit/admin-users-edit.component').then((m) => m.AdminUsersEdit),
      },
      {
        path: 'reset-password',
        title: 'Reset password - Sage Admin',
        data: { access_perms: [Perm.AdminUserResetPassword] },
        canActivate: [PermissionGuard],
        loadComponent: () => import('./admin-users-reset-password/admin-users-reset-password.component').then((m) => m.AdminUsersResetPassword),
      },
    ],
  },
];
