import { provideHttpClient, withInterceptors } from '@angular/common/http';
import {
  ApplicationConfig,
  inject,
  isDevMode,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
} from '@angular/core';
import {
  TitleStrategy,
  provideRouter,
  withComponentInputBinding,
  withInMemoryScrolling,
} from '@angular/router';
import { provideServiceWorker } from '@angular/service-worker';
import { provideTranslateService } from '@ngx-translate/core';
import { provideTranslateHttpLoader } from '@ngx-translate/http-loader';

import { environment } from '../environments/environment';
import { routes } from './app.routes';
import { HTTP_INTERCEPTORS_IN_ORDER } from './core/interceptors';
import { DEFAULT_LANGUAGE } from './core/models/language.model';
import { AuthService } from './core/services/auth.service';
import { LanguageService } from './core/services/language.service';
import { TranslatedTitleStrategy } from './core/services/translated-title.strategy';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),

    provideRouter(
      routes,
      withComponentInputBinding(),
      withInMemoryScrolling({ scrollPositionRestoration: 'enabled', anchorScrolling: 'enabled' }),
    ),
    { provide: TitleStrategy, useClass: TranslatedTitleStrategy },

    provideHttpClient(withInterceptors([...HTTP_INTERCEPTORS_IN_ORDER])),

    // ngx-translate: ceviriler `public/i18n/*.json` altindan yuklenir.
    provideTranslateService({
      lang: DEFAULT_LANGUAGE,
      fallbackLang: DEFAULT_LANGUAGE,
      loader: provideTranslateHttpLoader({ prefix: 'i18n/', suffix: '.json' }),
    }),

    provideServiceWorker('ngsw-worker.js', {
      enabled: environment.enableServiceWorker && !isDevMode(),
      registrationStrategy: 'registerWhenStable:30000',
    }),

    /**
     * Acilis sirasi onemlidir:
     * 1) dil belirlenir (localStorage -> tarayici dili -> `de`) ve ceviriler yuklenir,
     * 2) refresh token varsa oturum sessizce geri yuklenir.
     * Ikisi de tamamlanmadan router guard'lari calismaz; boylece `authGuard`
     * senkron kalabilir.
     */
    provideAppInitializer(async () => {
      const languageService = inject(LanguageService);
      const authService = inject(AuthService);

      languageService.initialize();
      await authService.restoreSession();
    }),
  ],
};
