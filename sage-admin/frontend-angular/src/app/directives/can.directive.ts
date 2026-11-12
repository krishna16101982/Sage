import { Directive, TemplateRef, ViewContainerRef, effect, inject, input } from '@angular/core';
import { AuthService } from '../services/auth/auth.service';
import { PermissionService } from '../services/permission/permission.service';

/**
 * Shows the element only if the signed-in user holds the permission. Hides buttons/menus (UX only; the API enforces).
 * Usage: <button *appCan="perm.GroupAdd">Add group</button>
 */
@Directive({ selector: '[appCan]' })
export class CanDirective {
  readonly appCan = input.required<string>();
  private readonly auth = inject(AuthService);
  private readonly permissions = inject(PermissionService);
  private readonly template = inject(TemplateRef<unknown>);
  private readonly container = inject(ViewContainerRef);
  private shown = false;

  constructor() {
    effect(() => {
      this.auth.user(); // re-check when the user changes
      const allowed = this.permissions.has(this.appCan());
      if (allowed && !this.shown) this.container.createEmbeddedView(this.template);
      if (!allowed && this.shown) this.container.clear();
      this.shown = allowed;
    });
  }
}
