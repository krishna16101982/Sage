import { Routes } from '@angular/router';
import { authGuard, guestGuard } from './core/guards';
import { ShellComponent } from './layout/shell.component';

/** One route per screen. Signed-in screens sit inside the shell (navigation + header). */
export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'home' },
  { path: 'login', title: 'Sign in - Sage Sales', canActivate: [guestGuard], loadComponent: () => import('./pages/auth/login.page').then((m) => m.LoginPage) },
  {
    path: '',
    component: ShellComponent,
    canActivate: [authGuard],
    canActivateChild: [authGuard],
    children: [
      { path: 'home', title: 'Home - Sage Sales', loadComponent: () => import('./pages/home.page').then((m) => m.HomePage) },
      { path: 'change-password', title: 'Change password - Sage Sales', loadComponent: () => import('./pages/auth/change-password.page').then((m) => m.ChangePasswordPage) },
    ],
  },
  { path: '**', redirectTo: 'home' },
];
