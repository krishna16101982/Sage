import { Component, ElementRef, OnDestroy, inject, input, model, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { LeadsService } from '../../../../services/leads/leads.service';
import { errorText } from '../../../../services/rest/global-rest-service';
import type { Lead } from '../../../../shared/interfaces/lead.interface';

/**
 * The order form's Lead box: "No lead (enter company manually)" or one lead no order was made from yet. Searches the
 * database as you type (lead number, company, client or person; 20 at a time), newest leads first. Two-way: [(lead)].
 */
@Component({
  selector: 'app-lead-picker',
  imports: [FormsModule],
  host: { '(document:mousedown)': 'outside($event)' },
  styles: [`
    :host { position: relative; display: block; }
    .lead-list { position: absolute; z-index: 60; left: 0; right: 0; top: calc(100% + 2px); max-height: 18rem; overflow-y: auto;
                 margin: 0; padding: 0.25rem 0; list-style: none; background: var(--color-surface);
                 border: 1px solid var(--color-border); border-radius: 0.5rem; box-shadow: 0 8px 24px rgb(15 23 42 / 0.12); }
    .lead-item { padding: 0.4rem 0.75rem; cursor: pointer; font-size: 0.875rem; }
    .lead-item:hover, .lead-item.is-active { background: var(--color-primary-soft); }
    .lead-item.is-none { color: var(--color-text-muted); }
    .lead-note { padding: 0.4rem 0.75rem; font-size: 0.8125rem; color: var(--color-text-muted); }
  `],
  template: `
    <input class="field" type="text" autocomplete="off" role="combobox" aria-label="Lead" [attr.aria-expanded]="open()"
           [disabled]="disabled()" [placeholder]="lead() ? '' : 'No lead (enter company manually) — type to search leads'"
           [ngModel]="text()" (ngModelChange)="typed($event)" (focus)="openList()" (keydown)="key($event)" />
    @if (open() && !disabled()) {
      <ul class="lead-list" role="listbox">
        <li role="option" class="lead-item is-none" [class.is-active]="active() === -1" (mousedown)="choose(null, $event)">No lead (enter company manually)</li>
        @if (searching()) { <li class="lead-note">Searching...</li> }
        @if (error()) { <li class="lead-note">{{ error() }}</li> }
        @for (l of results(); track l.id; let i = $index) {
          <li role="option" class="lead-item" [class.is-active]="i === active()" (mousedown)="choose(l, $event)">{{ label(l) }}</li>
        }
        @if (!searching() && results().length === 0 && !error()) { <li class="lead-note">No lead without an order matches.</li> }
        @if (more()) { <li class="lead-note">Showing the newest 20 — type to narrow down.</li> }
      </ul>
    }
  `,
})
export class LeadPicker implements OnDestroy {
  readonly lead = model<Lead | null>(null);
  readonly disabled = input(false);
  private readonly service = inject(LeadsService);
  private readonly host = inject(ElementRef<HTMLElement>);

  readonly open = signal(false);
  readonly active = signal(-1);
  readonly results = signal<Lead[]>([]);
  readonly more = signal(false);
  readonly searching = signal(false);
  readonly error = signal<string | null>(null);
  /** What is typed while the list is open; null shows the chosen lead. */
  private readonly query = signal<string | null>(null);
  private timer: ReturnType<typeof setTimeout> | undefined;
  private requestNo = 0;

  /** "LD-0046 · Glacier Solutions Pvt Ltd (Open)". */
  label(l: Lead): string {
    return `${l.leadNumber} · ${l.companyName} (${l.statusName})`;
  }

  text(): string {
    const q = this.query();
    if (q !== null) return q;
    const l = this.lead();
    return l ? this.label(l) : '';
  }

  openList(): void {
    if (this.open()) return;
    this.open.set(true);
    this.query.set('');
    this.search('');
  }

  typed(text: string): void {
    this.query.set(text);
    this.open.set(true);
    clearTimeout(this.timer);
    this.timer = setTimeout(() => this.search(text.trim()), 250);
  }

  private async search(term: string): Promise<void> {
    const no = ++this.requestNo;
    this.searching.set(true);
    this.error.set(null);
    try {
      const page = await this.service.list({ search: term || undefined, withoutOrder: true, page: 1, pageSize: 20 });
      if (no !== this.requestNo) return;
      this.results.set(page.items);
      this.more.set(page.totalCount > page.items.length);
      this.active.set(page.items.length > 0 && term ? 0 : -1);
    } catch (err) {
      if (no === this.requestNo) this.error.set(errorText(err, 'Unable to search leads.'));
    } finally {
      if (no === this.requestNo) this.searching.set(false);
    }
  }

  choose(l: Lead | null, event?: Event): void {
    event?.preventDefault();
    this.lead.set(l);
    this.close();
  }

  key(event: KeyboardEvent): void {
    const list = this.results();
    if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
      event.preventDefault();
      if (!this.open()) { this.openList(); return; }
      const step = event.key === 'ArrowDown' ? 1 : -1;
      this.active.set(Math.min(Math.max(this.active() + step, -1), list.length - 1));
    } else if (event.key === 'Enter') {
      if (this.open()) {
        event.preventDefault();
        this.choose(this.active() === -1 ? null : list[this.active()] ?? null);
      }
    } else if (event.key === 'Escape' || event.key === 'Tab') {
      if (this.open() && event.key === 'Escape') event.stopPropagation();
      this.close();
    }
  }

  outside(event: MouseEvent): void {
    if (this.open() && !this.host.nativeElement.contains(event.target as Node)) this.close();
  }

  private close(): void {
    this.open.set(false);
    this.query.set(null);
    clearTimeout(this.timer);
  }

  ngOnDestroy(): void {
    clearTimeout(this.timer);
  }
}
