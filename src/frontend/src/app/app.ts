import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';

/**
 * Kok bilesen — yalnizca router cikisini barindirir.
 * Yerlesim `layout/shell` tarafindan saglanir (login gibi kabuksuz rotalar haric).
 */
@Component({
  selector: 'hc-root',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterOutlet],
  template: '<router-outlet />',
})
export class App {}
