import { Component, ElementRef, computed, inject, input, model, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

/** One choice of a combo box. */
export interface ComboOption {
  id: string;
  label: string;
  /** Small text after the label, e.g. "Manager". */
  hint?: string;
}

/**
 * Searchable drop-down: type to filter the choices, pick with the mouse or the arrow keys and Enter. Two-way:
 * [(value)] is the chosen option's id (null = none). Leaving the box with text that is not a choice keeps the old choice.
 */
@Component({
  selector: 'app-combo-box',
  imports: [FormsModule],
  host: { '(document:mousedown)': 'outside($event)' },
  styles: [`
    :host { position: relative; display: block; }
    .combo-list { position: absolute; z-index: 60; left: 0; right: 0; top: calc(100% + 2px); max-height: 15rem; overflow-y: auto;
                  margin: 0; padding: 0.25rem 0; list-style: none; background: var(--color-surface);
                  border: 1px solid var(--color-border); border-radius: 0.5rem; box-shadow: 0 8px 24px rgb(15 23 42 / 0.12); }
    .combo-item { padding: 0.375rem 0.75rem; cursor: pointer; font-size: 0.875rem; }
    .combo-item.is-active, .combo-item:hover { background: var(--color-primary-soft); }
    .combo-item.is-selected { font-weight: 600; }
    .combo-hint { margin-left: 0.375rem; font-size: 0.75rem; color: var(--color-text-muted); }
    .combo-empty { padding: 0.375rem 0.75rem; font-size: 0.8125rem; color: var(--color-text-muted); }
  `],
  template: `
    <input class="field" type="text" autocomplete="off" role="combobox" [attr.aria-label]="label()" [attr.aria-expanded]="open()"
           [placeholder]="placeholder()" [disabled]="disabled()" [ngModel]="text()" (ngModelChange)="typed($event)"
           (focus)="openList()" (keydown)="key($event)" />
    @if (open() && !disabled()) {
      <ul class="combo-list" role="listbox">
        @for (o of matches(); track o.id; let i = $index) {
          <li role="option" class="combo-item" [class.is-active]="i === active()" [class.is-selected]="o.id === value()"
              [attr.aria-selected]="o.id === value()" (mousedown)="choose(o, $event)">
            {{ o.label }}@if (o.hint) { <span class="combo-hint">{{ o.hint }}</span> }
          </li>
        } @empty {
          <li class="combo-empty">No match</li>
        }
      </ul>
    }
  `,
})
export class ComboBox {
  readonly options = input<ComboOption[]>([]);
  readonly value = model<string | null>(null);
  readonly placeholder = input('— Select —');
  readonly label = input('');
  readonly disabled = input(false);

  private readonly host = inject(ElementRef<HTMLElement>);
  readonly open = signal(false);
  readonly active = signal(0);
  /** What the user typed while the list is open; null = show the chosen option's label. */
  private readonly query = signal<string | null>(null);

  private readonly selected = computed(() => this.options().find((o) => o.id === this.value()) ?? null);
  readonly text = computed(() => this.query() ?? this.selected()?.label ?? '');
  readonly matches = computed(() => {
    const q = (this.query() ?? '').trim().toLowerCase();
    const all = this.options();
    return (q ? all.filter((o) => o.label.toLowerCase().includes(q)) : all).slice(0, 100);
  });

  openList(): void {
    this.open.set(true);
    this.active.set(Math.max(0, this.matches().findIndex((o) => o.id === this.value())));
  }

  typed(text: string): void {
    this.query.set(text);
    this.open.set(true);
    this.active.set(0);
  }

  choose(o: ComboOption, event?: Event): void {
    event?.preventDefault();
    this.value.set(o.id);
    this.close();
  }

  key(event: KeyboardEvent): void {
    const list = this.matches();
    if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
      event.preventDefault();
      if (!this.open()) { this.openList(); return; }
      const step = event.key === 'ArrowDown' ? 1 : -1;
      this.active.set(Math.min(Math.max(this.active() + step, 0), Math.max(list.length - 1, 0)));
    } else if (event.key === 'Enter') {
      if (this.open() && list[this.active()]) {
        event.preventDefault();
        this.choose(list[this.active()]);
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
  }
}
