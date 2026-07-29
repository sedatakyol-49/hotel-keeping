import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

import { AuthStore } from '../../core/state/auth.store';
import { LanguagePicker } from '../../layout/language-picker/language-picker';
import { Card } from '../../shared/ui/card/card';
import { EmptyState } from '../../shared/ui/empty-state/empty-state';
import { PageHeader } from '../../shared/ui/page-header/page-header';

/**
 * Ayarlar. Dil secimi bu fazda tamamen calisir; otel/vergi profili ayarlari
 * backend uc noktalari hazir oldugunda eklenecektir.
 */
@Component({
  selector: 'hc-settings',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe, PageHeader, Card, EmptyState, LanguagePicker],
  template: `
    <div class="space-y-6">
      <hc-page-header
        titleKey="settings.title"
        subtitleKey="settings.subtitle"
        eyebrowKey="nav.section.system"
      />

      <hc-card titleKey="settings.appearance" headingId="settings-appearance">
        <div class="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <p class="label-mono text-ink">{{ 'language.label' | translate }}</p>
            <p class="mt-1 max-w-prose text-sm text-ink-muted">
              {{ 'settings.languageHint' | translate }}
            </p>
          </div>
          <hc-language-picker />
        </div>
      </hc-card>

      @if (authStore.hotels().length > 0) {
        <hc-card titleKey="hotel.label" headingId="settings-hotels">
          <ul class="divide-y divide-rule">
            @for (hotel of authStore.hotels(); track hotel.id) {
              <li class="flex items-baseline justify-between gap-4 py-2">
                <span class="text-sm text-ink">{{ hotel.name }}</span>
                <span class="numeric text-xs text-ink-muted">
                  {{ hotel.city }} {{ hotel.country }}
                </span>
              </li>
            }
          </ul>
        </hc-card>
      }

      <hc-empty-state
        titleKey="settings.empty.title"
        descriptionKey="settings.empty.description"
        eyebrowKey="common.underDevelopment"
      />
    </div>
  `,
})
export class SettingsPage {
  protected readonly authStore = inject(AuthStore);
}
