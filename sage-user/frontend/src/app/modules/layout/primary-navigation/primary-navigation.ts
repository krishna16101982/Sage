import { Component, computed, inject } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { Icon, initials } from '../../../component/icon/icon';
import { AuthService } from '../../../services/auth/auth.service';

export interface NavItem {
  href: string;
  label: string;
  icon: string;
  section: string;
  managerOnly: boolean;
}

/**
 * Menu items, in order, grouped by section. Team order forms is shown to sales managers only.
 * The simple "New sale" page (/sales/add) is hidden: sales are recorded with the New order form, which opens from the
 * "+ New order form" button on Order forms (and Home, Lead Records > New Order), not from the menu.
 */
export const NAV_ITEMS: NavItem[] = [
  { href: '/home', label: 'Home', icon: 'home', section: 'Overview', managerOnly: false },
  { href: '/leads', label: 'Lead Records', icon: 'trending-up', section: 'Sales', managerOnly: false },
  { href: '/sales/list', label: 'Order forms', icon: 'list', section: 'Sales', managerOnly: false },
  { href: '/sales/team', label: 'Team order forms', icon: 'users', section: 'Sales', managerOnly: true },
  { href: '/team', label: 'My team', icon: 'user-check', section: 'Sales', managerOnly: true },
  { href: '/team/add', label: 'Add sales person', icon: 'user-plus', section: 'Sales', managerOnly: true },
  { href: '/customers', label: 'Companies', icon: 'briefcase', section: 'Sales', managerOnly: false },
  { href: '/change-password', label: 'Change password', icon: 'lock', section: 'Account', managerOnly: false },
];

/** Left-hand navigation (dark sidebar). */
@Component({
  selector: 'app-primary-navigation',
  imports: [RouterLink, RouterLinkActive, Icon],
  templateUrl: './primary-navigation.html',
})
export class PrimaryNavigation {
  protected readonly auth = inject(AuthService);

  protected readonly sections = computed(() => {
    const visible = NAV_ITEMS.filter((i) => !i.managerOnly || this.auth.isManager());
    const sections: { name: string; items: NavItem[] }[] = [];
    for (const item of visible) {
      const last = sections[sections.length - 1];
      if (last && last.name === item.section) last.items.push(item);
      else sections.push({ name: item.section, items: [item] });
    }
    return sections;
  });

  protected readonly initials = computed(() => initials(this.auth.user()?.firstName, this.auth.user()?.lastName));
}
