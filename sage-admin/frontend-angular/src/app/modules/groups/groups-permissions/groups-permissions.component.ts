import { Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { GroupsService } from '../../../services/groups/groups.service';
import { errorText } from '../../../services/rest/global-rest-service';
import { PermissionService } from '../../../services/permission/permission.service';
import type { GroupDetail, GroupMatrixModule, GroupMatrixPermission } from '../../../shared/interfaces/group.interface';
import { Perm } from '../../../shared/enumarations/permission-codes';
import { Alert } from '../../../component/alert/alert';
import { PageHeader } from '../../../component/page-header/page-header';

/** Common actions first, then anything else alphabetically. */
const ACTION_ORDER = ['View', 'Add', 'Edit', 'Delete', 'Export'];
const actionRank = (a: string) => (ACTION_ORDER.indexOf(a) === -1 ? ACTION_ORDER.length : ACTION_ORDER.indexOf(a));

/**
 * Permission matrix for one group: modules as rows, actions as columns. Everything shown comes from the API (SQL).
 * Without Group.AssignPermissions the matrix is read-only. An inactive permission the group still holds can be removed
 * but not granted again.
 */
@Component({
  selector: 'app-groups-permissions',
  imports: [FormsModule, RouterLink, Alert, PageHeader],
  templateUrl: './groups-permissions.component.html',
})
export class GroupsPermissions implements OnInit {
  /** From the route (:id). */
  readonly id = input.required<string>();
  private readonly api = inject(GroupsService);
  private readonly permissions = inject(PermissionService);

  readonly canAssign = computed(() => this.permissions.has(Perm.GroupAssignPermissions));
  readonly detail = signal<GroupDetail | null>(null);
  readonly selected = signal<Set<number>>(new Set());
  readonly original = signal<Set<number>>(new Set());
  readonly filter = signal('');
  readonly error = signal<string | null>(null);
  readonly savedOk = signal(false);
  readonly busy = signal(false);

  readonly actions = computed(() => {
    const all = new Set((this.detail()?.modules ?? []).flatMap((m) => m.permissions.map((p) => p.action)));
    return [...all].sort((a, b) => actionRank(a) - actionRank(b) || a.localeCompare(b));
  });

  readonly visibleModules = computed(() => {
    const q = this.filter().trim().toLowerCase();
    const mods = this.detail()?.modules ?? [];
    return q ? mods.filter((m) => `${m.name} ${m.code} ${m.category ?? ''}`.toLowerCase().includes(q)) : mods;
  });

  /** Rows grouped under their category, keeping the API's order. */
  readonly byCategory = computed(() => {
    const groups: { category: string; modules: GroupMatrixModule[] }[] = [];
    for (const m of this.visibleModules()) {
      const category = m.category ?? 'Other';
      const last = groups[groups.length - 1];
      if (last && last.category === category) last.modules.push(m);
      else groups.push({ category, modules: [m] });
    }
    return groups;
  });

  readonly added = computed(() => [...this.selected()].filter((id) => !this.original().has(id)).length);
  readonly removed = computed(() => [...this.original()].filter((id) => !this.selected().has(id)).length);
  readonly dirty = computed(() => this.added() + this.removed() > 0);

  ngOnInit(): void {
    this.api.get(this.id()).then(
      (d) => this.apply(d),
      (err) => this.error.set(errorText(err, "Unable to load the group's permissions.")),
    );
  }

  private apply(d: GroupDetail): void {
    const assigned = new Set(d.modules.flatMap((m) => m.permissions.filter((p) => p.assigned).map((p) => p.id)));
    this.detail.set(d);
    this.selected.set(assigned);
    this.original.set(new Set(assigned));
  }

  permissionFor(m: GroupMatrixModule, action: string): GroupMatrixPermission | undefined {
    return m.permissions.find((p) => p.action === action);
  }

  columnPermissions(action: string): GroupMatrixPermission[] {
    return this.visibleModules().flatMap((m) => m.permissions.filter((p) => p.action === action));
  }

  private editable(p: GroupMatrixPermission): boolean {
    return this.canAssign() && (p.isActive || this.original().has(p.id));
  }

  canToggle(p: GroupMatrixPermission): boolean {
    return this.editable(p) && (p.isActive || this.selected().has(p.id));
  }

  /** Permissions a row/column "All" checkbox may change. */
  grantable(perms: GroupMatrixPermission[]): GroupMatrixPermission[] {
    return perms.filter((p) => this.canToggle(p));
  }

  allOn(perms: GroupMatrixPermission[]): boolean {
    return perms.length > 0 && perms.every((p) => this.selected().has(p.id));
  }

  toggleMany(perms: GroupMatrixPermission[], on: boolean): void {
    this.savedOk.set(false);
    const next = new Set(this.selected());
    perms.forEach((p) => (on ? next.add(p.id) : next.delete(p.id)));
    this.selected.set(next);
  }

  reset(): void {
    const d = this.detail();
    if (d) this.apply(d);
  }

  async save(): Promise<void> {
    this.error.set(null);
    this.busy.set(true);
    try {
      this.apply(await this.api.setPermissions(this.id(), [...this.selected()]));
      this.savedOk.set(true);
    } catch (err) {
      this.error.set(errorText(err));
    } finally {
      this.busy.set(false);
    }
  }
}
