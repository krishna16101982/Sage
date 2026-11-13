import { Component, computed, inject } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { AuthService } from '../../../services/auth/auth.service';
import type { MenuItem } from '../../../shared/interfaces/auth.interface';

/**
 * Left-hand navigation, built from the Modules table (Permissions > Modules): every active module with a menu link that
 * the user can view, in sort order, under its category. Nothing here is hard-coded.
 */
@Component({
  selector: 'app-primary-navigation',
  imports: [RouterLink, RouterLinkActive],
  templateUrl: './primary-navigation.html',
})
export class PrimaryNavigation {
  private readonly auth = inject(AuthService);

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
}
