import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';

/** Centred card that holds the sign-in pages (login, forgot password, reset password). */
@Component({
  selector: 'app-login-layout',
  imports: [RouterOutlet],
  templateUrl: './login-layout.html',
})
export class LoginLayout {}
