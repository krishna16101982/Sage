import { Component, OnInit, inject, signal } from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Alert } from '../../../component/alert/alert';
import { LeadListsService } from '../../../services/lead-lists/lead-lists.service';
import { PermissionService } from '../../../services/permission/permission.service';
import { errorText } from '../../../services/rest/global-rest-service';
import { Perm } from '../../../shared/enumarations/permission-codes';
import type { LeadList, LeadListValue, LeadListValueInput } from '../../../shared/interfaces/lead-list.interface';

/** The row being edited or added: an existing value's id, or null for a new one. */
interface Draft extends LeadListValueInput {
  id: number | null;
}

/**
 * /settings/lead-lists — products, lead sources and billing cycles of the User Panel's lead form (one table each).
 * A value in use is turned off, never removed, so old leads keep showing it. Lead status and lead type are fixed.
 */
@Component({
  selector: 'app-lead-lists',
  imports: [FormsModule, NgTemplateOutlet, RouterLink, Alert],
  templateUrl: './lead-lists.component.html',
})
export class LeadLists implements OnInit {
  private readonly service = inject(LeadListsService);
  readonly canEdit = inject(PermissionService).has(Perm.SettingEdit);

  readonly lists = signal<LeadList[] | null>(null);
  readonly error = signal<string | null>(null);
  readonly notice = signal<string | null>(null);
  readonly draft = signal<Draft | null>(null);
  readonly draftError = signal<string | null>(null);
  readonly saving = signal(false);

  async ngOnInit(): Promise<void> {
    await this.load();
  }

  private async load(): Promise<void> {
    try {
      this.lists.set(await this.service.lists());
    } catch (err) {
      this.error.set(errorText(err, 'Unable to load the lists.'));
    }
  }

  isEditing(list: LeadList, value: LeadListValue | null): boolean {
    const d = this.draft();
    return !!d && d.list === list.list && d.id === (value?.id ?? null);
  }

  edit(value: LeadListValue): void {
    this.notice.set(null);
    this.draftError.set(null);
    this.draft.set({ id: value.id, list: value.list, name: value.name, sortOrder: value.sortOrder, isActive: value.isActive });
  }

  add(list: LeadList): void {
    this.notice.set(null);
    this.draftError.set(null);
    const next = list.values.reduce((max, v) => Math.max(max, v.sortOrder), 0) + 1;
    this.draft.set({ id: null, list: list.list, name: '', sortOrder: next, isActive: true });
  }

  setDraft<K extends keyof Draft>(field: K, value: Draft[K]): void {
    this.draft.update((d) => (d ? { ...d, [field]: value } : d));
  }

  async save(): Promise<void> {
    const d = this.draft();
    if (!d) return;
    this.saving.set(true);
    this.draftError.set(null);
    const body: LeadListValueInput = { list: d.list, name: d.name.trim(), sortOrder: d.sortOrder ?? 0, isActive: d.isActive };
    try {
      const saved = d.id === null ? await this.service.create(body) : await this.service.update(d.id, body);
      this.draft.set(null);
      this.notice.set(`${saved.name} saved.`);
      await this.load();
    } catch (err) {
      this.draftError.set(errorText(err));
    } finally {
      this.saving.set(false);
    }
  }
}
