import { inject } from '@angular/core';
import { type CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';

/** User Panel: must be signed in; a user who must change their password is sent to Change password first. */
export const authGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const user = auth.user();
  if (!user) return router.parseUrl('/login');
  if (user.mustChangePassword && state.url !== '/change-password') return router.parseUrl('/change-password');
  return true;
};

/** Sales-manager screens (Team sales). Anyone else goes to their own sales. The API checks this again. */
export const managerGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  return auth.isManager() ? true : inject(Router).parseUrl('/sales/list');
};

/** Sign-in pages: an already signed-in user goes straight to the home page. */
export const guestGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  return auth.user() ? inject(Router).parseUrl('/home') : true;
};
