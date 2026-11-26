import { Component, ElementRef, HostListener, effect, input, output, signal, viewChild } from '@angular/core';
import { readEditor, sameRich, serializeRich, writeEditor } from '../../shared/utils/rich-text';

/** Text colours and highlight colours offered in the toolbar. */
const TEXT_COLOURS = [
  { name: 'Red', hex: '#c62828' }, { name: 'Orange', hex: '#e65100' }, { name: 'Green', hex: '#2e7d32' },
  { name: 'Blue', hex: '#1565c0' }, { name: 'Purple', hex: '#6a1b9a' }, { name: 'Grey', hex: '#616161' }, { name: 'Black', hex: '#000000' },
];
const HIGHLIGHTS = [
  { name: 'Yellow', hex: '#fff176' }, { name: 'Green', hex: '#c8e6c9' }, { name: 'Blue', hex: '#bbdefb' },
  { name: 'Pink', hex: '#f8bbd0' }, { name: 'Orange', hex: '#ffe0b2' },
];

/**
 * A small text box with a toolbar: Bold, Italic, Underline, text colour, highlight and Clear (removes the formatting of
 * the selected words). The value is the wording markup of shared/utils/rich-text.ts (only those formats); pasted text
 * comes in plain. Ctrl+B / Ctrl+I / Ctrl+U work too.
 */
@Component({
  selector: 'app-rich-text-editor',
  templateUrl: './rich-text-editor.html',
  styleUrl: './rich-text-editor.css',
})
export class RichTextEditor {
  readonly value = input<string | null>('');
  /** Height of the box in text lines. */
  readonly rows = input(2);
  /** false = one line only (Enter does nothing). */
  readonly multiline = input(true);
  readonly label = input('');
  readonly valueChange = output<string>();

  protected readonly textColours = TEXT_COLOURS;
  protected readonly highlights = HIGHLIGHTS;
  protected readonly palette = signal<'color' | 'background' | null>(null);
  /** Formats at the caret, for the pressed look of B / I / U. */
  protected readonly active = signal({ bold: false, italic: false, underline: false });

  private readonly box = viewChild<ElementRef<HTMLDivElement>>('box');
  private readonly host = viewChild<ElementRef<HTMLDivElement>>('wrap');
  /** The markup the box shows now (what it last sent or was given). */
  private shown: string | null = null;

  constructor() {
    // A new value from outside (load, Reset, standard text that follows the form) replaces the box's content; the value
    // it sent itself does not, so the caret stays where it is while typing.
    effect(() => {
      const value = this.value() ?? '';
      const el = this.box()?.nativeElement;
      if (!el) return;
      if (this.shown !== null && (value === this.shown || sameRich(value, this.shown))) {
        this.shown = value;
        return;
      }
      writeEditor(el, value);
      this.shown = value;
    });
  }

  protected onInput(): void {
    const el = this.box()?.nativeElement;
    if (!el) return;
    const markup = serializeRich(readEditor(el));
    this.shown = markup;
    this.valueChange.emit(markup);
  }

  /** Toolbar buttons keep the selection: they act on mousedown without taking the focus. */
  protected keepSelection(event: MouseEvent): void {
    event.preventDefault();
  }

  protected format(command: 'bold' | 'italic' | 'underline' | 'removeFormat'): void {
    this.run(command);
    this.palette.set(null);
  }

  protected togglePalette(which: 'color' | 'background'): void {
    this.palette.set(this.palette() === which ? null : which);
  }

  protected colour(hex: string): void {
    this.run(this.palette() === 'background' ? 'hiliteColor' : 'foreColor', hex);
    this.palette.set(null);
  }

  protected onKeydown(event: KeyboardEvent): void {
    if (event.key === 'Enter' && !this.multiline()) event.preventDefault();
    if (event.key === 'Escape') this.palette.set(null);
  }

  /** Pasted text comes in plain (no fonts or styles from other pages). */
  protected onPaste(event: ClipboardEvent): void {
    event.preventDefault();
    let text = event.clipboardData?.getData('text/plain') ?? '';
    if (!this.multiline()) text = text.replace(/\s*\r?\n\s*/g, ' ');
    document.execCommand('insertText', false, text);
  }

  @HostListener('document:selectionchange')
  protected onSelectionChange(): void {
    const el = this.box()?.nativeElement;
    const sel = document.getSelection();
    if (!el || !sel?.anchorNode || !el.contains(sel.anchorNode)) return;
    this.active.set({
      bold: document.queryCommandState('bold'),
      italic: document.queryCommandState('italic'),
      underline: document.queryCommandState('underline'),
    });
  }

  @HostListener('document:mousedown', ['$event'])
  protected onDocumentMousedown(event: MouseEvent): void {
    if (this.palette() && !this.host()?.nativeElement.contains(event.target as Node)) this.palette.set(null);
  }

  /** document.execCommand is the one way to format a content-editable selection in every browser. */
  private run(command: string, arg?: string): void {
    const el = this.box()?.nativeElement;
    if (!el) return;
    const sel = document.getSelection();
    if (!sel || !sel.anchorNode || !el.contains(sel.anchorNode)) el.focus();
    document.execCommand('styleWithCSS', false, 'true');
    document.execCommand(command, false, arg);
    if (command === 'removeFormat') document.execCommand('hiliteColor', false, 'transparent');
    this.onInput();
    this.onSelectionChange();
  }
}
