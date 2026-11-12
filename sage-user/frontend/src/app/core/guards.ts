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

/** Sign-in pages: an already signed-in user goes straight to the home page. */
export const guestGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  return auth.user() ? inject(Router).parseUrl('/home') : true;
};
