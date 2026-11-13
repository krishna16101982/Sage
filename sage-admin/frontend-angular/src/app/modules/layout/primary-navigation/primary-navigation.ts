import { Component, computed, inject } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { Icon, initials } from '../../../component/icon/icon';
import { AuthService } from '../../../services/auth/auth.service';
import type { MenuItem } from '../../../shared/interfaces/auth.interface';

/** Icon per module code. A module added later without an entry here gets a neutral icon. */
export const MODULE_ICONS: Record<string, string | undefined> = {
  Dashboard: 'grid',
  AdminUser: 'users',
  Group: 'shield',
  Permission: 'key',
  AuditLog: 'file-text',
  Setting: 'sliders',
  Product: 'box',
  SalesUser: 'user-check',
  Customer: 'briefcase',
  Sales: 'trending-up',
};

/**
 * Left-hand navigation (dark sidebar), built from the Modules table (Permissions > Modules): every active module with a
 * menu link that the user can view, in sort order, under its category. Nothing here is hard-coded except the icons.
 */
@Component({
  selector: 'app-primary-navigation',
  imports: [RouterLink, RouterLinkActive, Icon],
  templateUrl: './primary-navigation.html',
})
export class PrimaryNavigation {
  protected readonly auth = inject(AuthService);
  protected readonly icons = MODULE_ICONS;

  /** Consecutive items with the same category form one section. */
  protected readonly sections = computed(() => {
    const sections: { category: string | null; items: MenuItem[] }[] = [];
    for (const item of this.auth.user()?.menu ?? []) {
      const last = sections[sections.length - 1];
      if (last && last.category === item.category) last.items.push(item);
      else sections.push({ category: item.category, items: [item] });
    }
    return sections;
  });

  protected readonly initials = computed(() => initials(this.auth.user()?.firstName, this.auth.user()?.lastName));
}
