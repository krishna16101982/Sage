import { Routes } from '@angular/router';
import { LoginLayout } from './modules/login/login-layout/login-layout';
import { PrimaryLayout } from './modules/layout/primary-layout/primary-layout';
import { authGuard, guestGuard } from './services/auth/auth.guard';

/**
 * Top-level routes. The sign-in page sits inside LoginLayout (centred card); signed-in screens sit inside
 * PrimaryLayout (navigation + header). Each feature (sales, customers) brings its own routes file.
 */
export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'home' },

  // Sign-in pages
  {
    path: '',
    component: LoginLayout,
    children: [
      { path: 'login', title: 'Sign in - Sage Sales', canActivate: [guestGuard], loadComponent: () => import('./modules/login/login-prompt/login-prompt').then((m) => m.LoginPrompt) },
    ],
  },

  // Signed-in area
  {
    path: '',
    component: PrimaryLayout,
    canActivate: [authGuard],
    canActivateChild: [authGuard],
    children: [
      { path: 'home', title: 'Home - Sage Sales', loadComponent: () => import('./modules/home/home').then((m) => m.Home) },
      { path: 'sales', loadChildren: () => import('./modules/sales/sales.routes').then((m) => m.saleRoutes) },
      { path: 'customers', loadChildren: () => import('./modules/customers/customers.routes').then((m) => m.customerRoutes) },
      { path: 'team', loadChildren: () => import('./modules/team/team.routes').then((m) => m.teamRoutes) },
      { path: 'change-password', title: 'Change password - Sage Sales', loadComponent: () => import('./modules/changepassword/changepassword').then((m) => m.Changepassword) },
    ],
  },

  { path: '**', redirectTo: 'home' },
];
