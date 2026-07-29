import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';

/** 404 sayfasi. */
@Component({
  selector: 'hc-not-found',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, TranslatePipe],
  template: `
    <div class="mx-auto max-w-2xl border border-rule bg-paper-raised px-6 py-12 text-center">
      <p class="eyebrow">404</p>
      <h1 class="mt-3 font-serif text-4xl text-ink">
        {{ 'errors.pageNotFound.title' | translate }}
      </h1>
      <p class="mx-auto mt-3 max-w-prose text-sm text-ink-muted">
        {{ 'errors.pageNotFound.description' | translate }}
      </p>
      <a
        routerLink="/dashboard"
        class="mt-6 inline-flex min-h-touch items-center border border-navy bg-navy px-4 label-mono text-paper hover:bg-ink hover:border-ink"
      >
        {{ 'nav.dashboard' | translate }}
      </a>
    </div>
  `,
})
export class NotFoundPage {}
