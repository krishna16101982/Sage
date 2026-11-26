import { Component, computed, input } from '@angular/core';
import { cleanLines } from '../../shared/utils/rich-text';

/** Shows order form wording with its formatting (bold, italic, underline, colour, highlight), read-only. */
@Component({
  selector: 'app-rich-text-view',
  template: `@for (line of lines(); track $index) {@if ($index > 0) {<br />}@for (run of line; track $index) {<span
    [style.font-weight]="run.bold ? 700 : null" [style.font-style]="run.italic ? 'italic' : null"
    [style.text-decoration]="run.underline ? 'underline' : null" [style.color]="run.color"
    [style.background-color]="run.background">{{ run.text }}</span>}}`,
})
export class RichTextView {
  readonly value = input<string | null | undefined>('');
  protected readonly lines = computed(() => cleanLines(this.value()));
}
