import { bootstrapApplication, type BootstrapContext } from '@angular/platform-browser';

import { App } from './app/app';
import { config } from './app/app.config.server';

/**
 * Sunucu tarafi bootstrap — `@angular/ssr` bu varsayilan disa aktarimi cagirir.
 *
 * `BootstrapContext` Angular 22'de zorunludur ve ILETILMELIDIR: sunucuda her
 * istek kendi platform ornegini kullanir. Baglam gecilmezse render "No platform
 * exists" (NG0401) ile duser.
 */
const bootstrap = (context: BootstrapContext) => bootstrapApplication(App, config, context);

export default bootstrap;
