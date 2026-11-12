import { Routes } from '@angular/router';
import { LoginLayout } from './modules/login/login-layout/login-layout';
import { PrimaryLayout } from './modules/layout/primary-layout/primary-layout';
import { authGuard, guestGuard } from './services/auth/auth.guard';
import { PermissionGuard } from './services/permission/permission.guard';
import { Perm } from './shared/enumarations/permission-codes';

const comingSoon = () => import('./component/coming-soon/coming-soon').then((m) => m.ComingSoon);

/**
 * Top-level routes. Sign-in pages sit inside LoginLayout (centred card); admin screens sit inside PrimaryLayout
 * (sidebar + header) and need a signed-in user. Each feature brings its own routes file (list / add / :id/edit).
 */
export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'dashboard' },

  // Sign-in pages
  {
    path: '',
    component: LoginLayout,
    children: [
      { path: 'login', title: 'Sign in - Sage Admin', canActivate: [guestGuard], loadComponent: () => import('./modules/login/login-prompt/login-prompt').then((m) => m.LoginPrompt) },
      { path: 'forgot-password', title: 'Forgot password - Sage Admin', loadComponent: () => import('./modules/login/forgot-password/forgot-password').then((m) => m.ForgotPassword) },
      { path: 'reset-password', title: 'Reset password - Sage Admin', loadComponent: () => import('./modules/login/reset-password/reset-password').then((m) => m.ResetPassword) },
    ],
  },

  // Admin area
  {
    path: '',
    component: PrimaryLayout,
    canActivate: [authGuard],
    canActivateChild: [authGuard],
    children: [
      { path: 'dashboard', title: 'Dashboard - Sage Admin', loadComponent: () => import('./modules/dashboard/dashboard').then((m) => m.Dashboard) },
      { path: 'users', loadChildren: () => import('./modules/admin-users/admin-users.routes').then((m) => m.adminUserRoutes) },
      { path: 'groups', loadChildren: () => import('./modules/groups/groups.routes').then((m) => m.groupRoutes) },
      { path: 'permissions', loadChildren: () => import('./modules/permissions/permissions.routes').then((m) => m.permissionRoutes) },
      { path: 'products', loadChildren: () => import('./modules/products/products.routes').then((m) => m.productRoutes) },
      { path: 'sales-users', loadChildren: () => import('./modules/sales-users/sales-users.routes').then((m) => m.salesUserRoutes) },
      {
        path: 'audit-logs',
        title: 'Audit Logs - Sage Admin',
        data: { title: 'Audit Logs', access_perms: [Perm.AuditLogView] },
        canActivate: [PermissionGuard],
        loadComponent: comingSoon,
      },
      { path: 'profile', title: 'My Profile - Sage Admin', data: { title: 'My Profile' }, loadComponent: comingSoon },
      {
        path: 'settings',
        title: 'Settings - Sage Admin',
        data: { title: 'Settings', access_perms: [Perm.SettingView] },
        canActivate: [PermissionGuard],
        loadComponent: comingSoon,
      },
      { path: 'change-password', title: 'Change password - Sage Admin', loadComponent: () => import('./modules/changepassword/changepassword').then((m) => m.Changepassword) },
    ],
  },

  { path: '**', redirectTo: 'dashboard' },
];
