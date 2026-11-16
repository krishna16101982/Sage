import { Routes } from '@angular/router';
import { PermissionGuard } from '../../services/permission/permission.guard';
import { Perm } from '../../shared/enumarations/permission-codes';

/** /settings — application settings, and /settings/masters — the order-form lists (change both with Setting.Edit). */
export const settingRoutes: Routes = [
  {
    path: '',
    title: 'Settings - Sage Admin',
    data: { access_perms: [Perm.SettingView] },
    canActivate: [PermissionGuard],
    loadComponent: () => import('./settings-list/settings-list.component').then((m) => m.SettingsList),
  },
  {
    path: 'masters',
    title: 'Order form lists - Sage Admin',
    data: { access_perms: [Perm.SettingView], title: 'Order form lists' },
    canActivate: [PermissionGuard],
    loadComponent: () => import('./masters-list/masters-list.component').then((m) => m.MastersList),
  },
];
