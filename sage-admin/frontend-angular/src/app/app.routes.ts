import { Routes } from '@angular/router';
import { authGuard, guestGuard } from './core/guards';
import { ShellComponent } from './layout/shell.component';

/** One route per screen. Admin screens sit inside the shell (sidebar + header) and need a signed-in user. */
export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'dashboard' },

  // Sign-in pages
  { path: 'login', title: 'Sign in - Sage Admin', canActivate: [guestGuard], loadComponent: () => import('./pages/auth/login.page').then((m) => m.LoginPage) },
  { path: 'forgot-password', title: 'Forgot password - Sage Admin', loadComponent: () => import('./pages/auth/forgot-password.page').then((m) => m.ForgotPasswordPage) },
  { path: 'reset-password', title: 'Reset password - Sage Admin', loadComponent: () => import('./pages/auth/reset-password.page').then((m) => m.ResetPasswordPage) },

  // Admin area
  {
    path: '',
    component: ShellComponent,
    canActivate: [authGuard],
    canActivateChild: [authGuard],
    children: [
      { path: 'dashboard', title: 'Dashboard - Sage Admin', loadComponent: () => import('./pages/placeholder.page').then((m) => m.DashboardPage) },
      { path: 'users', title: 'Admin Users - Sage Admin', loadComponent: () => import('./pages/users/admin-users.page').then((m) => m.AdminUsersPage) },
      { path: 'groups', title: 'Groups - Sage Admin', loadComponent: () => import('./pages/groups/groups.page').then((m) => m.GroupsPage) },
      { path: 'permissions', title: 'Permissions - Sage Admin', loadComponent: () => import('./pages/permissions/permissions.page').then((m) => m.PermissionsPage) },
      { path: 'products', title: 'Products - Sage Admin', loadComponent: () => import('./pages/products/products.page').then((m) => m.ProductsPage) },
      { path: 'sales-users', title: 'Sales Users - Sage Admin', loadComponent: () => import('./pages/sales-users/sales-users.page').then((m) => m.SalesUsersPage) },
      { path: 'audit-logs', title: 'Audit Logs - Sage Admin', data: { title: 'Audit Logs' }, loadComponent: () => import('./pages/placeholder.page').then((m) => m.PlaceholderPage) },
      { path: 'profile', title: 'My Profile - Sage Admin', data: { title: 'My Profile' }, loadComponent: () => import('./pages/placeholder.page').then((m) => m.PlaceholderPage) },
      { path: 'settings', title: 'Settings - Sage Admin', data: { title: 'Settings' }, loadComponent: () => import('./pages/placeholder.page').then((m) => m.PlaceholderPage) },
      { path: 'change-password', title: 'Change password - Sage Admin', loadComponent: () => import('./pages/auth/change-password.page').then((m) => m.ChangePasswordPage) },
    ],
  },

  { path: '**', redirectTo: 'dashboard' },
];
