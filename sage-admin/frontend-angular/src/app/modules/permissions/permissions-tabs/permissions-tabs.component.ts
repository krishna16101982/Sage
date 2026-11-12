import { Component } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';

/** "Permissions | Modules" tabs above both permission lists. */
@Component({
  selector: 'app-permissions-tabs',
  imports: [RouterLink, RouterLinkActive],
  templateUrl: './permissions-tabs.component.html',
})
export class PermissionsTabs {}
