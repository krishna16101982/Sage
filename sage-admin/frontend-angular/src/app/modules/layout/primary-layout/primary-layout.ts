import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { PrimaryHeader } from '../primary-header/primary-header';
import { PrimaryNavigation } from '../primary-navigation/primary-navigation';

/** Signed-in layout: navigation on the left, header on top, the current page in the middle. */
@Component({
  selector: 'app-primary-layout',
  imports: [RouterOutlet, PrimaryHeader, PrimaryNavigation],
  templateUrl: './primary-layout.html',
})
export class PrimaryLayout {}
