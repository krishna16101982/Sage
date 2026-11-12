import { Component, OnInit, computed, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { GroupsApi } from '../../core/admin-api';
import { errorText } from '../../core/api-client';
import { AuthService } from '../../core/auth.service';
import type { GroupDetail, GroupItem, GroupMatrixModule, GroupMatrixPermission } from '../../core/models/group';
import { Perm } from '../../core/permissionCodes';
import { AlertComponent, ModalComponent } from '../../shared/ui';

/** Common actions first, then anything else alphabetically. */
const ACTION_ORDER = ['View', 'Add', 'Edit', 'Delete', 'Export'];
const actionRank = (a: string) => (ACTION_ORDER.indexOf(a) === -1 ? ACTION_ORDER.length : ACTION_ORDER.indexOf(a));

/**
 * Permission matrix for one group: modules as rows, actions as columns. Everything shown comes from the API (SQL).
 * Without Group.AssignPermissions the matrix is read-only. An inactive permission the group still holds can be removed
 * but not granted again.
 */
@Component({
  selector: 'app-group-permissions-dialog',
  imports: [FormsModule, ModalComponent, AlertComponent],
  template: `
    <app-modal [title]="'Permissions: ' + group().name" size="xl" (closed)="closed.emit()">
      <div class="stack-sm text-sm">
        @if (error()) { <app-alert kind="error">{{ error() }}</app-alert> }
        @if (savedOk() && !dirty()) { <app-alert kind="success">Permissions saved. Members get the new access on their next request.</app-alert> }
        @if (group().status === 'Inactive') { <app-alert kind="error">This group is inactive, so it grants nothing until it is activated again.</app-alert> }
        @if (!canAssign()) { <p class="text-muted">Read-only: you do not have permission to change group permissions.</p> }

        <div class="toolbar">
          <input class="field field-inline w-56" aria-label="Filter modules" placeholder="Filter modules" [ngModel]="filter()" (ngModelChange)="filter.set($event)" />
          <span class="text-muted">{{ selected().size }} selected{{ dirty() ? ' (' + added() + ' added, ' + removed() + ' removed)' : '' }}</span>
        </div>

        @if (!detail() && !error()) { <p class="table-empty">Loading...</p> }

        @if (detail()) {
          <div class="table-wrap">
            <table class="data-table">
              <thead>
                <tr>
                  <th>Module</th>
                  <th class="center">All</th>
                  @for (a of actions(); track a) {
                    <th class="center">
                      <label class="stack-xs" style="align-items: center">
                        <span>{{ a }}</span>
                        @if (canAssign()) {
                          <input type="checkbox" [attr.aria-label]="'All ' + a + ' permissions'"
                                 [checked]="allOn(grantable(columnPermissions(a)))" [disabled]="grantable(columnPermissions(a)).length === 0"
                                 (change)="toggleMany(grantable(columnPermissions(a)), $any($event.target).checked)" />
                        }
                      </label>
                    </th>
                  }
                </tr>
              </thead>
              <tbody>
                @for (cat of byCategory(); track cat.category) {
                  <tr class="group-row"><td [attr.colspan]="actions().length + 2">{{ cat.category }}</td></tr>
                  @for (m of cat.modules; track m.id) {
                    <tr>
                      <td>
                        {{ m.name }}
                        @if (!m.isActive) { <span class="badge">inactive module</span> }
                      </td>
                      <td class="center">
                        @if (canAssign()) {
                          <input type="checkbox" [attr.aria-label]="'All ' + m.name + ' permissions'"
                                 [checked]="allOn(grantable(m.permissions))" [disabled]="grantable(m.permissions).length === 0"
                                 (change)="toggleMany(grantable(m.permissions), $any($event.target).checked)" />
                        }
                      </td>
                      @for (a of actions(); track a) {
                        <td class="center">
                          @if (permissionFor(m, a); as p) {
                            <input type="checkbox" [attr.aria-label]="p.code" [class.dim]="!p.isActive"
                                   [title]="p.isActive ? (p.description ?? p.name) : p.name + ' (inactive: can be removed, not granted again)'"
                                   [checked]="selected().has(p.id)" [disabled]="!canToggle(p)"
                                   (change)="toggleMany([p], $any($event.target).checked)" />
                          }
                        </td>
                      }
                    </tr>
                  }
                } @empty {
                  <tr><td [attr.colspan]="actions().length + 2" class="table-empty">No modules match.</td></tr>
                }
              </tbody>
            </table>
          </div>
        }

        <div class="modal-footer">
          <button type="button" class="btn btn-secondary" (click)="closed.emit()">{{ canAssign() && dirty() ? 'Cancel' : 'Close' }}</button>
          @if (canAssign()) {
            <button type="button" class="btn btn-secondary" [disabled]="!dirty() || busy()" (click)="reset()">Reset</button>
            <button type="button" class="btn btn-primary" [disabled]="!dirty() || busy()" (click)="save()">{{ busy() ? 'Saving...' : 'Save permissions' }}</button>
          }
        </div>
      </div>
    </app-modal>
  `,
})
export class GroupPermissionsDialog implements OnInit {
  readonly group = input.required<GroupItem>();
  readonly closed = output<void>();
  readonly saved = output<void>();
  private readonly api = inject(GroupsApi);
  private readonly auth = inject(AuthService);

  readonly canAssign = computed(() => this.auth.can(Perm.GroupAssignPermissions));
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
    this.api.get(this.group().id).then(
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
      this.apply(await this.api.setPermissions(this.group().id, [...this.selected()]));
      this.savedOk.set(true);
      this.saved.emit();
    } catch (err) {
      this.error.set(errorText(err));
    } finally {
      this.busy.set(false);
    }
  }
}
