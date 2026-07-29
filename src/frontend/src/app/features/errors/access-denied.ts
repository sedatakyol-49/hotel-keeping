import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';

/** 403 — izin anahtari eksik oldugunda `permissionGuard` buraya yonlendirir. */
@Component({
  selector: 'hc-access-denied',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, TranslatePipe],
  template: `
    <div class="mx-auto max-w-2xl border border-rule bg-paper-raised px-6 py-12 text-center">
      <p class="eyebrow">403</p>
      <h1 class="mt-3 font-serif text-4xl text-ink">
        {{ 'errors.accessDenied.title' | translate }}
      </h1>
      <p class="mx-auto mt-3 max-w-prose text-sm text-ink-muted">
        {{ 'errors.accessDenied.description' | translate }}
      </p>
      <a
        routerLink="/dashboard"
        class="mt-6 inline-flex min-h-touch items-center border border-rule-strong px-4 label-mono text-ink hover:bg-paper-sunken"
      >
        {{ 'nav.dashboard' | translate }}
      </a>
    </div>
  `,
})
export class AccessDeniedPage {}
