import {
  AngularNodeAppEngine,
  createNodeRequestHandler,
  isMainModule,
  writeResponseToNodeResponse,
} from '@angular/ssr/node';
import { createReadStream, statSync } from 'node:fs';
import { createServer, type IncomingMessage, type ServerResponse } from 'node:http';
import { extname, join, resolve, sep } from 'node:path';

/**
 * ===========================================================================
 * Misafir sitesi SSR sunucusu
 * ===========================================================================
 *
 * NEDEN EXPRESS YOK: Angular'in urettigi ornek sunucu Express kullanir, ama
 * burada Express'in yaptigi tek is `express.static` ile bir klasoru servis
 * etmektir. Karsiliginda ~60 dolayinda gecisli paket gelir. Production'da bu
 * surec zaten bir ters vekil/CDN arkasinda calisir ve statik dosyalari
 * cogunlukla o katman servis eder; gelistirmede ise `ng serve` statikleri
 * kendisi verir. Bu yuzden bagimlilik eklemek yerine `node:http` uzerinde
 * ~40 satirlik, kapsami acikca sinirli bir statik sunucu yazildi.
 * (Bagimlilik politikasi: gerekcesi olmayan paket eklenmez.)
 */

const browserDistFolder = resolve(import.meta.dirname, '../browser');

/**
 * SSRF korumasi (Angular 22): sunucu yalnizca bilinen `Host` basliklarina yanit
 * verir. Aksi halde saldirgan `Host: evil.example` gondererek uretilen mutlak
 * adreslerin (canonical, hreflang, form action) kendi alan adini gostermesini
 * saglayabilir — "absolute URL poisoning". Dagitimda gercek alan adlari
 * `SSR_ALLOWED_HOSTS` ile virgullu olarak verilir.
 */
const allowedHosts = (process.env['SSR_ALLOWED_HOSTS'] ?? 'localhost')
  .split(',')
  .map((host) => host.trim())
  .filter((host) => host.length > 0);

const angularApp = new AngularNodeAppEngine({ allowedHosts });

/** En sik kullanilan tipler; listede olmayan uzanti icin genel ikili tip. */
const CONTENT_TYPES: Readonly<Record<string, string>> = {
  '.css': 'text/css; charset=utf-8',
  '.html': 'text/html; charset=utf-8',
  '.ico': 'image/x-icon',
  '.jpg': 'image/jpeg',
  '.js': 'text/javascript; charset=utf-8',
  '.json': 'application/json; charset=utf-8',
  '.png': 'image/png',
  '.svg': 'image/svg+xml',
  '.txt': 'text/plain; charset=utf-8',
  '.webmanifest': 'application/manifest+json',
  '.webp': 'image/webp',
  '.woff2': 'font/woff2',
  '.xml': 'application/xml; charset=utf-8',
};

/**
 * `dist/guest-web/browser` altindaki dosyalari servis eder.
 * Guvenlik: cozulen mutlak yol bu klasorun **disina** cikarsa istek reddedilir
 * (`..%2f` gibi yol asimi denemeleri).
 */
function serveStatic(request: IncomingMessage, response: ServerResponse): boolean {
  if (request.method !== 'GET' && request.method !== 'HEAD') {
    return false;
  }

  let pathname: string;
  try {
    pathname = decodeURIComponent(new URL(request.url ?? '/', 'http://localhost').pathname);
  } catch {
    return false;
  }

  // Dizin istekleri Angular'a birakilir (rota olabilir).
  if (pathname.endsWith('/')) {
    return false;
  }

  const filePath = resolve(join(browserDistFolder, pathname));
  if (filePath !== browserDistFolder && !filePath.startsWith(browserDistFolder + sep)) {
    return false;
  }

  let size: number;
  try {
    const stats = statSync(filePath);
    if (!stats.isFile()) {
      return false;
    }
    size = stats.size;
  } catch {
    return false;
  }

  const extension = extname(filePath).toLowerCase();
  response.writeHead(200, {
    'Content-Type': CONTENT_TYPES[extension] ?? 'application/octet-stream',
    'Content-Length': size,
    // Icerik karmasi dosya adinda; uzun onbellek guvenli. index.html haric.
    'Cache-Control': extension === '.html' ? 'no-cache' : 'public, max-age=31536000, immutable',
    'X-Content-Type-Options': 'nosniff',
  });

  if (request.method === 'HEAD') {
    response.end();
    return true;
  }

  createReadStream(filePath).pipe(response);
  return true;
}

/**
 * Istek isleyici. `ng serve` bu varsayilan disa aktarimi kullanir (statikleri
 * dev-server kendisi verdigi icin orada `serveStatic` neredeyse hic calismaz).
 */
const handler = async (
  request: IncomingMessage,
  response: ServerResponse,
  next: (error?: unknown) => void,
): Promise<void> => {
  try {
    const rendered = await angularApp.handle(request);
    if (rendered) {
      await writeResponseToNodeResponse(rendered, response);
      return;
    }
  } catch (error) {
    next(error);
    return;
  }

  next();
};

/**
 * `ng serve` bu **adi** arar (`reqHandler`); bulamazsa kendi ic SSR ara katmanina
 * duser ve gelistirme ile uretim iki farkli yoldan servis edilir. Ayni deger iki
 * adla disa aktarilir: varsayilan disa aktarim uretim girisi icin korunur.
 */
export const reqHandler = createNodeRequestHandler(handler);

export default reqHandler;

/** Dogrudan calistirildiginda (production): kendi HTTP sunucusunu acar. */
if (isMainModule(import.meta.url)) {
  const port = Number(process.env['PORT'] ?? 4400);

  createServer((request, response) => {
    if (serveStatic(request, response)) {
      return;
    }

    void handler(request, response, (error) => {
      if (error) {
        console.error(error);
      }
      response.statusCode = error ? 500 : 404;
      response.end();
    });
  }).listen(port, () => {
    console.log(`Guest site (SSR) listening on http://localhost:${port}`);
  });
}
