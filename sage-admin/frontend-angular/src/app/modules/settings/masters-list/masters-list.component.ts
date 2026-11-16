import { Component, OnInit, inject, signal } from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Alert } from '../../../component/alert/alert';
import { MastersService } from '../../../services/masters/masters.service';
import { PermissionService } from '../../../services/permission/permission.service';
import { errorText } from '../../../services/rest/global-rest-service';
import { Perm } from '../../../shared/enumarations/permission-codes';
import type { MasterList, MasterValue, MasterValueInput } from '../../../shared/interfaces/master.interface';

/** The row being edited or added: an existing value's id, or 'new' + list. */
interface Draft extends MasterValueInput {
  id: number | null;
}

/**
 * /settings/masters — the order-form lists (billing frequency, billing method, payment method, payment term, currency).
 * Every drop-down on the User Panel's New order form comes from here; the default is filled in on a new order. A value in
 * use is turned off, never removed, so old orders keep showing it.
 */
@Component({
  selector: 'app-masters-list',
  imports: [FormsModule, NgTemplateOutlet, RouterLink, Alert],
  templateUrl: './masters-list.component.html',
})
export class MastersList implements OnInit {
  private readonly service = inject(MastersService);
  readonly canEdit = inject(PermissionService).has(Perm.SettingEdit);

  readonly lists = signal<MasterList[] | null>(null);
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

  isEditing(list: MasterList, value: MasterValue | null): boolean {
    const d = this.draft();
    return !!d && d.masterType === list.masterType && d.id === (value?.id ?? null);
  }

  edit(value: MasterValue): void {
    this.notice.set(null);
    this.draftError.set(null);
    this.draft.set({ id: value.id, masterType: value.masterType, code: value.code, name: value.name, sortOrder: value.sortOrder, isDefault: value.isDefault, isActive: value.isActive });
  }

  add(list: MasterList): void {
    this.notice.set(null);
    this.draftError.set(null);
    const next = list.values.reduce((max, v) => Math.max(max, v.sortOrder), 0) + 10;
    this.draft.set({ id: null, masterType: list.masterType, code: '', name: '', sortOrder: next, isDefault: false, isActive: true });
  }

  setDraft<K extends keyof Draft>(field: K, value: Draft[K]): void {
    this.draft.update((d) => (d ? { ...d, [field]: value } : d));
  }

  async save(): Promise<void> {
    const d = this.draft();
    if (!d) return;
    this.saving.set(true);
    this.draftError.set(null);
    const body: MasterValueInput = {
      masterType: d.masterType, code: d.code.trim().toUpperCase(), name: d.name.trim(), sortOrder: d.sortOrder ?? 0,
      isDefault: d.isDefault, isActive: d.isDefault ? true : d.isActive,
    };
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
