import { inject } from '@angular/core';
import { type CanActivateFn, Router } from '@angular/router';
import { PermissionService } from './permission.service';

/**
 * Blocks a route unless the user holds every permission in route.data['access_perms'] (sends them to the dashboard).
 * Example: { path: 'add', data: { access_perms: ['AdminUser.Add'] }, canActivate: [PermissionGuard], ... }
 */
export const PermissionGuard: CanActivateFn = (route) => {
  const required = (route.data?.['access_perms'] as string[] | undefined) ?? [];
  return inject(PermissionService).hasAll(required) ? true : inject(Router).parseUrl('/dashboard');
};
