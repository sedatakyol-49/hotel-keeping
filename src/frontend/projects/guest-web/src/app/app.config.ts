import { provideHttpClient, withFetch } from '@angular/common/http';
import { provideBrowserGlobalErrorListeners, type ApplicationConfig } from '@angular/core';
import { provideClientHydration, withEventReplay } from '@angular/platform-browser';
import { provideRouter, withComponentInputBinding, withInMemoryScrolling } from '@angular/router';
import { provideTranslateService } from '@ngx-translate/core';

import { DEFAULT_LANGUAGE } from '@hotelcore/shared';

import { routes } from './app.routes';
import { BundledTranslateLoader } from './core/i18n/bundled-translate.loader';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),

    provideRouter(
      routes,
      // Rota parametreleri (`:lang`, `:slug`) ve `data` dogrudan girdilere baglanir.
      withComponentInputBinding(),
      withInMemoryScrolling({ scrollPositionRestoration: 'enabled', anchorScrolling: 'enabled' }),
    ),

    /*
     * Hidrasyon: sunucudan gelen DOM yeniden olusturulmaz, devralinir.
     * `withEventReplay` sunucu HTML'i gorunur olduktan sonra ama JavaScript
     * hazir olmadan once yapilan tiklamalari kuyruklar — misafir sitesinde
     * ilk tik cogunlukla "Jetzt buchen"dir, kaybedilmemeli.
     */
    provideClientHydration(withEventReplay()),

    provideHttpClient(withFetch()),

    /*
     * Ceviriler pakete gomulur (bkz. BundledTranslateLoader): SSR ciktisinda
     * metin bulunmasi SEO'nun on kosuludur.
     */
    provideTranslateService({
      lang: DEFAULT_LANGUAGE,
      fallbackLang: DEFAULT_LANGUAGE,
      loader: BundledTranslateLoader,
    }),
  ],
};
