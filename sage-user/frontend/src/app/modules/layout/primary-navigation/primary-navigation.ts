import { Component } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';

/** Menu items. Sales screens (new sale, my sales, team sales) are added in Sprint 9. */
const ITEMS = [
  { href: '/home', label: 'Home' },
  { href: '/change-password', label: 'Change password' },
];

/** Left-hand navigation. */
@Component({
  selector: 'app-primary-navigation',
  imports: [RouterLink, RouterLinkActive],
  templateUrl: './primary-navigation.html',
})
export class PrimaryNavigation {
  protected readonly items = ITEMS;
}
