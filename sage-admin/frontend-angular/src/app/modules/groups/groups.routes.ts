import { Routes } from '@angular/router';
import { PermissionGuard } from '../../services/permission/permission.guard';
import { Perm } from '../../shared/enumarations/permission-codes';

/** /groups/... — permission groups and their permission matrix. */
export const groupRoutes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'list' },
  {
    path: 'list',
    title: 'Groups - Sage Admin',
    data: { access_perms: [Perm.GroupView] },
    canActivate: [PermissionGuard],
    loadComponent: () => import('./groups-list/groups-list.component').then((m) => m.GroupsList),
  },
  {
    path: 'add',
    title: 'Add group - Sage Admin',
    data: { access_perms: [Perm.GroupAdd] },
    canActivate: [PermissionGuard],
    loadComponent: () => import('./groups-add/groups-add.component').then((m) => m.GroupsAdd),
  },
  {
    path: ':id',
    children: [
      {
        path: 'edit',
        title: 'Edit group - Sage Admin',
        data: { access_perms: [Perm.GroupEdit] },
        canActivate: [PermissionGuard],
        loadComponent: () => import('./groups-edit/groups-edit.component').then((m) => m.GroupsEdit),
      },
      {
        // Viewing needs Group.View; saving changes needs Group.AssignPermissions (read-only otherwise).
        path: 'permissions',
        title: 'Group permissions - Sage Admin',
        data: { access_perms: [Perm.GroupView] },
        canActivate: [PermissionGuard],
        loadComponent: () => import('./groups-permissions/groups-permissions.component').then((m) => m.GroupsPermissions),
      },
    ],
  },
];
