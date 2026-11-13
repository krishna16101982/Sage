import { Routes } from '@angular/router';
import { PermissionGuard } from '../../services/permission/permission.guard';
import { Perm } from '../../shared/enumarations/permission-codes';

/** /settings — application settings (change values with Setting.Edit). */
export const settingRoutes: Routes = [
  {
    path: '',
    title: 'Settings - Sage Admin',
    data: { access_perms: [Perm.SettingView] },
    canActivate: [PermissionGuard],
    loadComponent: () => import('./settings-list/settings-list.component').then((m) => m.SettingsList),
  },
];
