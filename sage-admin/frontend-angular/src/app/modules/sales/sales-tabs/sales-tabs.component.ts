import { Component } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';

/** "All sales | Summary" tabs above the sales screens. */
@Component({
  selector: 'app-sales-tabs',
  imports: [RouterLink, RouterLinkActive],
  templateUrl: './sales-tabs.component.html',
})
export class SalesTabs {}
