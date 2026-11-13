import { Routes } from '@angular/router';
import { PermissionGuard } from '../../services/permission/permission.guard';
import { Perm } from '../../shared/enumarations/permission-codes';

/** /audit-logs/... — the audit log (list with filters, one entry). */
export const auditLogRoutes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'list' },
  {
    path: 'list',
    title: 'Audit Logs - Sage Admin',
    data: { access_perms: [Perm.AuditLogView] },
    canActivate: [PermissionGuard],
    loadComponent: () => import('./audit-logs-list/audit-logs-list.component').then((m) => m.AuditLogsList),
  },
  {
    path: ':id',
    title: 'Audit entry - Sage Admin',
    data: { access_perms: [Perm.AuditLogView] },
    canActivate: [PermissionGuard],
    loadComponent: () => import('./audit-logs-view/audit-logs-view.component').then((m) => m.AuditLogsView),
  },
];
