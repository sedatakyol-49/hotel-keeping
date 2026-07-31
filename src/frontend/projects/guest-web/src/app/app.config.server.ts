import { mergeApplicationConfig, type ApplicationConfig } from '@angular/core';
import { provideServerRendering, withRoutes } from '@angular/ssr';

import { appConfig } from './app.config';
import { serverRoutes } from './app.routes.server';
import { provideLegalSnapshot } from './core/legal/legal-snapshot.server';

/**
 * Sunucu yapilandirmasi: tarayici yapilandirmasi + rota basina render modu +
 * derleme ani hukuki icerik.
 *
 * Anlik goruntu **burada** saglanir, `app.config.ts`'te degil: JSON yalnizca
 * sunucu paketine girer, misafirin indirdigi pakete degil.
 */
const serverConfig: ApplicationConfig = {
  providers: [provideServerRendering(withRoutes(serverRoutes)), provideLegalSnapshot()],
};

export const config = mergeApplicationConfig(appConfig, serverConfig);
