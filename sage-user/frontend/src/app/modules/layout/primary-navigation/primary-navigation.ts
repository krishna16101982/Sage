import { Component, computed, inject } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { AuthService } from '../../../services/auth/auth.service';

/** Menu items. Team sales is shown to sales managers only. */
const ITEMS = [
  { href: '/home', label: 'Home', managerOnly: false },
  { href: '/sales/add', label: 'New sale', managerOnly: false },
  { href: '/sales/list', label: 'My sales', managerOnly: false },
  { href: '/sales/team', label: 'Team sales', managerOnly: true },
  { href: '/customers', label: 'Customers', managerOnly: false },
  { href: '/change-password', label: 'Change password', managerOnly: false },
];

/** Left-hand navigation. */
@Component({
  selector: 'app-primary-navigation',
  imports: [RouterLink, RouterLinkActive],
  templateUrl: './primary-navigation.html',
})
export class PrimaryNavigation {
  private readonly auth = inject(AuthService);
  protected readonly items = computed(() => ITEMS.filter((i) => !i.managerOnly || this.auth.isManager()));
}
