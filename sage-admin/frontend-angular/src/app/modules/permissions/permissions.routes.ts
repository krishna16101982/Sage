import { Routes } from '@angular/router';
import { PermissionGuard } from '../../services/permission/permission.guard';
import { Perm } from '../../shared/enumarations/permission-codes';

/** /permissions/... — permissions and the modules they belong to. */
export const permissionRoutes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'list' },
  {
    path: 'list',
    title: 'Permissions - Sage Admin',
    data: { access_perms: [Perm.PermissionView] },
    canActivate: [PermissionGuard],
    loadComponent: () => import('./permissions-list/permissions-list.component').then((m) => m.PermissionsList),
  },
  {
    path: 'add',
    title: 'Add permission - Sage Admin',
    data: { access_perms: [Perm.PermissionAdd] },
    canActivate: [PermissionGuard],
    loadComponent: () => import('./permissions-add/permissions-add.component').then((m) => m.PermissionsAdd),
  },
  {
    path: 'modules',
    children: [
      {
        path: '',
        title: 'Permission modules - Sage Admin',
        data: { access_perms: [Perm.PermissionView] },
        canActivate: [PermissionGuard],
        loadComponent: () => import('./permission-modules-list/permission-modules-list.component').then((m) => m.PermissionModulesList),
      },
      {
        path: 'add',
        title: 'Add module - Sage Admin',
        data: { access_perms: [Perm.PermissionAdd] },
        canActivate: [PermissionGuard],
        loadComponent: () => import('./permission-modules-add/permission-modules-add.component').then((m) => m.PermissionModulesAdd),
      },
      {
        path: ':id/edit',
        title: 'Edit module - Sage Admin',
        data: { access_perms: [Perm.PermissionEdit] },
        canActivate: [PermissionGuard],
        loadComponent: () => import('./permission-modules-edit/permission-modules-edit.component').then((m) => m.PermissionModulesEdit),
      },
    ],
  },
  {
    path: ':id/edit',
    title: 'Edit permission - Sage Admin',
    data: { access_perms: [Perm.PermissionEdit] },
    canActivate: [PermissionGuard],
    loadComponent: () => import('./permissions-edit/permissions-edit.component').then((m) => m.PermissionsEdit),
  },
];
