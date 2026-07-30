import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';

import { LanguageStore } from '@hotelcore/shared';

import { languagePath } from '../../core/i18n/language-url';
import { PageIntro } from '../../shared/ui/page-intro/page-intro';

/**
 * 404 — dil on ekinin **icinde** yasar (`/de/olmayan-sayfa`), boylece hatali
 * bir adres bile kabugu, dogru dili ve hukuki baglantilari korur.
 */
@Component({
  selector: 'hcg-not-found-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, TranslatePipe, PageIntro],
  template: `
    <div class="hcg-shell py-12">
      <hcg-page-intro
        [eyebrow]="'errors.notFound.code' | translate"
        [heading]="'errors.notFound.title' | translate"
        [lede]="'errors.notFound.lede' | translate"
      />
      <a [routerLink]="homePath()" class="hcg-action mt-10" data-testid="not-found-home">
        {{ 'errors.notFound.home' | translate }}
      </a>
    </div>
  `,
})
export class NotFoundPage {
  private readonly language = inject(LanguageStore);
  protected readonly homePath = computed(() => languagePath(this.language.current()));
}
