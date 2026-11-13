import { Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { ThemeService } from '../../../services/theme/theme.service';

/** Centred card that holds the sign-in pages (login, forgot password, reset password), with the brand above it. */
@Component({
  selector: 'app-login-layout',
  imports: [RouterOutlet],
  templateUrl: './login-layout.html',
})
export class LoginLayout {
  constructor() {
    inject(ThemeService); // applies the saved light / dark choice
  }
}
