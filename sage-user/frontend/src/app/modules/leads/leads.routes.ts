import { Routes } from '@angular/router';

/** /leads — Lead Records (every sales user). */
export const leadRoutes: Routes = [
  {
    path: '',
    title: 'Lead Records - Sage Sales',
    loadComponent: () => import('./leads-list/leads-list.component').then((m) => m.LeadsList),
  },
];
