import { Routes } from '@angular/router';
import { managerGuard } from '../../services/auth/auth.guard';

/** /team — sales managers: their team members, who may request cancellations, and the requests waiting. */
export const teamRoutes: Routes = [
  {
    path: '',
    title: 'My team - Sage Sales',
    canActivate: [managerGuard],
    loadComponent: () => import('./team-page/team-page.component').then((m) => m.TeamPage),
  },
];
