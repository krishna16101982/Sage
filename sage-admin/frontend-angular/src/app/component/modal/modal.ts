import { Component, HostListener, input, output } from '@angular/core';

/** Dialog over the page (used for confirmations); closes on Escape or a click on the backdrop. */
@Component({
  selector: 'app-modal',
  templateUrl: './modal.html',
})
export class Modal {
  readonly title = input.required<string>();
  readonly size = input<'md' | 'xl'>('md');
  readonly closed = output<void>();

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.closed.emit();
  }
}
