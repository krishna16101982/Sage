import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Alert } from '../../../component/alert/alert';
import { PermissionService } from '../../../services/permission/permission.service';
import { errorText } from '../../../services/rest/global-rest-service';
import { SettingsService } from '../../../services/settings/settings.service';
import type { Setting } from '../../../shared/interfaces/audit-setting.interface';
import { Perm } from '../../../shared/enumarations/permission-codes';
import { formatDateTime } from '../../../shared/utils/format';

/**
 * /settings — every setting, grouped by category. Each one shows what it does and its limits; with Setting.Edit the
 * value can be changed and saved on its own row (the API checks it again, and the change is audited).
 */
@Component({
  selector: 'app-settings-list',
  imports: [FormsModule, RouterLink, Alert],
  templateUrl: './settings-list.component.html',
})
export class SettingsList implements OnInit {
  private readonly service = inject(SettingsService);
  protected readonly dateTime = formatDateTime;
  readonly canEdit = inject(PermissionService).has(Perm.SettingEdit);
  readonly canSeeAudit = inject(PermissionService).has(Perm.AuditLogView);

  readonly settings = signal<Setting[] | null>(null);
  /** Values being edited, by key. */
  readonly drafts = signal<Record<string, string>>({});
  readonly saving = signal<string | null>(null);
  readonly rowError = signal<{ key: string; message: string } | null>(null);
  readonly error = signal<string | null>(null);
  readonly notice = signal<string | null>(null);

  readonly groups = computed(() => {
    const groups: { category: string; items: Setting[] }[] = [];
    for (const s of this.settings() ?? []) {
      const g = groups.find((x) => x.category === s.category);
      if (g) g.items.push(s);
      else groups.push({ category: s.category, items: [s] });
    }
    return groups;
  });

  async ngOnInit(): Promise<void> {
    try {
      const list = await this.service.list();
      this.settings.set(list);
      this.drafts.set(Object.fromEntries(list.map((s) => [s.key, s.value ?? ''])));
    } catch (err) {
      this.error.set(errorText(err, 'Unable to load settings.'));
    }
  }

  draft(key: string): string {
    return this.drafts()[key] ?? '';
  }

  setDraft(key: string, value: unknown): void {
    this.drafts.update((d) => ({ ...d, [key]: value === null || value === undefined ? '' : String(value) }));
    if (this.rowError()?.key === key) this.rowError.set(null);
  }

  isChanged(s: Setting): boolean {
    return this.draft(s.key).trim() !== (s.value ?? '');
  }

  /** "Whole number from 0 to 100", "Up to 5 characters", ... */
  limits(s: Setting): string {
    if (s.valueType === 'int') {
      if (s.minValue !== null && s.maxValue !== null) return `Whole number from ${s.minValue} to ${s.maxValue}`;
      if (s.minValue !== null) return `Whole number, at least ${s.minValue}`;
      if (s.maxValue !== null) return `Whole number, at most ${s.maxValue}`;
      return 'Whole number';
    }
    if (s.valueType === 'bool') return 'On or off';
    if (s.valueType === 'json') return 'JSON';
    return s.maxLength ? `Up to ${s.maxLength} characters` : 'Text';
  }

  reset(s: Setting): void {
    this.setDraft(s.key, s.value ?? '');
  }

  async save(s: Setting): Promise<void> {
    this.rowError.set(null);
    this.notice.set(null);
    this.saving.set(s.key);
    try {
      const updated = await this.service.update(s.key, this.draft(s.key));
      this.settings.update((list) => (list ?? []).map((x) => (x.key === s.key ? updated : x)));
      this.setDraft(s.key, updated.value ?? '');
      this.notice.set(`${updated.label} saved.`);
    } catch (err) {
      this.rowError.set({ key: s.key, message: errorText(err) });
    } finally {
      this.saving.set(null);
    }
  }
}
