import { Component, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { errorText } from '../core/api-client';
import { AlertComponent, ModalComponent, PASSWORD_HINT } from './ui';

/** Admin sets a temporary password for someone; they must change it at next sign-in. Used for admin and sales users. */
@Component({
  selector: 'app-reset-password-dialog',
  imports: [FormsModule, ModalComponent, AlertComponent],
  template: `
    <app-modal [title]="'Reset password: ' + name()" (closed)="closed.emit()">
      <form class="form" (ngSubmit)="submit()">
        @if (error()) { <app-alert kind="error">{{ error() }}</app-alert> }
        <label class="field-label">Temporary password
          <input class="field" name="password" type="password" autocomplete="new-password" required [(ngModel)]="password" />
        </label>
        <label class="field-label">Confirm password
          <input class="field" name="confirm" type="password" autocomplete="new-password" required [(ngModel)]="confirm" />
        </label>
        <p class="hint">{{ hint }} Their current sessions end and any lockout is cleared.</p>
        <button type="submit" class="btn btn-primary btn-block" [disabled]="busy()">{{ busy() ? 'Please wait...' : 'Set temporary password' }}</button>
      </form>
    </app-modal>
  `,
})
export class ResetPasswordDialog {
  /** The person's name, for the title and the success message. */
  readonly name = input.required<string>();
  /** Calls the API (admin users or sales users). */
  readonly reset = input.required<(password: string, confirm: string) => Promise<unknown>>();
  readonly closed = output<void>();
  /** Emits the success message. */
  readonly done = output<string>();

  protected readonly hint = PASSWORD_HINT;
  password = '';
  confirm = '';
  readonly error = signal<string | null>(null);
  readonly busy = signal(false);

  async submit(): Promise<void> {
    this.error.set(null);
    this.busy.set(true);
    try {
      await this.reset()(this.password, this.confirm);
      this.done.emit(`Temporary password set for ${this.name()}. They must change it at next sign-in.`);
    } catch (err) {
      this.error.set(errorText(err));
    } finally {
      this.busy.set(false);
    }
  }
}
