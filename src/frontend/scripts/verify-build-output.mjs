#!/usr/bin/env node
/**
 * ===========================================================================
 * DERLEME CIKTISI DENETIMI — "basarili derleme" bos sayfa demek olmasin
 * ===========================================================================
 *
 * Angular derlemesi, prerender sirasinda bir HTTP istegi duserse bunu **stderr'e
 * bir satir yazip devam eder** ve cikis kodu 0 kalir. Gercekten oldu: ana sayfa
 * prerender ediliyordu, `GET /hotels/{slug}` ve `/room-types` derleme aninda
 * dusuyordu ve dagitilan HTML'de tek bir oda adi ya da fiyat yoktu. Derleme
 * "yesil"di. Bu betik o sinifin sessiz kalmasini engeller.
 *
 * UC KAPI:
 *
 *  1) IKI UYGULAMA + SSR PAKETI: iki `dist` cikti agaci ve SSR sunucusu var mi.
 *
 *  2) PRERENDER KURALI VE ICERIGI:
 *     - Prerender edilen sayfa kumesi **yalnizca hukuki sayfalardir**. Fiyat
 *       tasiyan bir sayfa (ana sayfa, oda tipi, arama) prerender'a geri
 *       konursa burada kirilir — bayat bir "ab 139 €" PAngV/UWG acisindan
 *       yanlis bir fiyat iddiasidir (gerekce: app.routes.server.ts).
 *     - Her hukuki sayfa GERCEK metin tasir: kunye tuzel kisi adini, AGB ve
 *       aydinlatma metni kendi govdesini. Hicbiri hata paneli icermez.
 *       (§5 DDG: JavaScript calistirmayan ziyaretci de gormeli.)
 *
 *  3) SSR SMOKE (hermetik): uretilen SSR sunucusu gercekten calisiyor mu ve
 *     ana sayfa katalogu HTML'e basiyor mu? Sahte bir "origin" sunucusu
 *     `/api/...` icin SABIT bir fikstur dondurur, gerisini SSR sunucusuna
 *     iletir; boylece backend, veritabani ve ag gerekmez. Denetlenen sey
 *     KABLOLAMADIR: goreli adresin sunucuda mutlaklastirilmasi, Node'da fetch,
 *     ve verinin HTML'e girmesi. Fikstur **uretim HTML'ine girmez**.
 *
 * Calistirma:  npm run verify:build        (npm run build'den SONRA)
 */
import { spawn } from 'node:child_process';
import { createServer, request as httpRequest } from 'node:http';
import { connect } from 'node:net';
import { existsSync, readFileSync, readdirSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const HERE = dirname(fileURLToPath(import.meta.url));
const ROOT = resolve(HERE, '..');
const GUEST_BROWSER = join(ROOT, 'dist/guest-web/browser');
const SSR_ENTRY = join(ROOT, 'dist/guest-web/server/server.mjs');
const SNAPSHOT = join(ROOT, 'projects/guest-web/src/generated/legal-snapshot.json');

const LANGUAGES = ['de', 'en', 'tr'];
const LEGAL_SLUGS = ['imprint', 'privacy', 'terms'];
const HOTEL_SLUG = process.env.GUEST_HOTEL_SLUG ?? 'berlin-mitte';

const failures = [];

function fail(message) {
  failures.push(message);
  console.error(`  FAIL  ${message}`);
}

function pass(message) {
  console.log(`  ok    ${message}`);
}

/** Etiketleri ve satir sonlarini sadelestirir: metin karsilastirmasi icin. */
function textOf(html) {
  return html
    .replace(/<script[\s\S]*?<\/script>/g, ' ')
    .replace(/<style[\s\S]*?<\/style>/g, ' ')
    .replace(/<[^>]*>/g, ' ')
    .replace(/&amp;/g, '&')
    .replace(/\s+/g, ' ');
}

// ---------------------------------------------------------------------------
// 1) Cikti agaclari
// ---------------------------------------------------------------------------
console.log('\n[1/3] Cikti agaclari');

for (const file of ['dist/hotelcore-web/browser/index.html', 'dist/guest-web/server/server.mjs']) {
  existsSync(join(ROOT, file)) ? pass(file) : fail(`${file} yok — iki uygulama da derlenmeli`);
}

// ---------------------------------------------------------------------------
// 2) Prerender kumesi ve icerigi
// ---------------------------------------------------------------------------
console.log('\n[2/3] Prerender kumesi ve icerigi');

/** `dist/guest-web/browser` altindaki tum `index.html` sayfalarinin yollari. */
function prerenderedPages(directory = GUEST_BROWSER, prefix = '') {
  const pages = [];
  for (const entry of readdirSync(directory, { withFileTypes: true })) {
    const next = join(directory, entry.name);
    if (entry.isDirectory()) {
      pages.push(...prerenderedPages(next, `${prefix}/${entry.name}`));
    } else if (entry.name === 'index.html') {
      pages.push(prefix === '' ? '/' : prefix);
    }
  }
  return pages;
}

const expected = LANGUAGES.flatMap((lang) => LEGAL_SLUGS.map((slug) => `/${lang}/legal/${slug}`));
const actual = prerenderedPages().sort();
const unexpected = actual.filter((page) => !expected.includes(page));
const missing = expected.filter((page) => !actual.includes(page));

if (unexpected.length > 0) {
  fail(
    `Prerender edilmemesi gereken sayfalar uretilmis: ${unexpected.join(', ')}\n` +
      '        Fiyat/musaitlik tasiyan sayfalar SSR olmalidir (app.routes.server.ts).',
  );
}
if (missing.length > 0) {
  fail(`Prerender edilmesi gereken hukuki sayfalar eksik: ${missing.join(', ')}`);
}
if (unexpected.length === 0 && missing.length === 0) {
  pass(`prerender kumesi tam ve yalnizca hukuki sayfalar (${actual.length})`);
}

let snapshot = null;
try {
  snapshot = JSON.parse(readFileSync(SNAPSHOT, 'utf8'));
} catch (error) {
  fail(`legal-snapshot.json okunamadi: ${error.message}`);
}

if (snapshot !== null) {
  for (const lang of LANGUAGES) {
    const documents = snapshot.documents[lang] ?? snapshot.documents.de;
    const entity = documents.imprint.legalEntityName;

    for (const slug of LEGAL_SLUGS) {
      const file = join(GUEST_BROWSER, lang, 'legal', slug, 'index.html');
      if (!existsSync(file)) {
        continue; // eksiklik yukarida raporlandi
      }

      const html = readFileSync(file, 'utf8');
      const text = textOf(html);

      if (html.includes('data-testid="error-panel"')) {
        fail(`/${lang}/legal/${slug}: sayfada HATA PANELI var — icerik derleme aninda dusmus`);
        continue;
      }

      if (slug === 'imprint') {
        text.includes(entity)
          ? pass(`/${lang}/legal/${slug}: kunye dolu`)
          : fail(`/${lang}/legal/${slug}: kunye (Impressum) YOK — JS'siz ziyaretci bos sayfa gorur`);
        continue;
      }

      // AGB / aydinlatma: belgenin kendi govdesinden taninabilir bir parca.
      const document = documents.documents.find((entry) => entry.key === slug);
      if (document === undefined) {
        fail(`Anlik goruntude "${lang}" icin "${slug}" belgesi yok`);
        continue;
      }

      const needle = textOf(document.bodyHtml).trim().slice(0, 40);
      text.includes(needle)
        ? pass(`/${lang}/legal/${slug}: belge govdesi dolu`)
        : fail(`/${lang}/legal/${slug}: belge govdesi YOK ("${needle}…" bulunamadi)`);
    }
  }
}

// ---------------------------------------------------------------------------
// 3) SSR smoke — ana sayfa katalogu HTML'e giriyor mu
// ---------------------------------------------------------------------------
console.log('\n[3/3] SSR smoke (sahte origin, backend gerekmez)');

/**
 * SAHTE VERI. Gercek otel/fiyat DEGILDIR ve uretim HTML'ine hicbir kosulda
 * girmez; yalnizca SSR kablolamasini dogrular. Sekil, canli API yanitindan
 * alinmistir (sozlesme §2.2 ve §3.1).
 */
const FIXTURE_ROOM_NAME = 'Verifikationszimmer';
const FIXTURE_PRICE = 123.45;

const HOTEL_FIXTURE = {
  slug: HOTEL_SLUG,
  brandName: 'SSR Smoke',
  name: 'SSR Smoke Hotel',
  description: null,
  addressLine: 'Teststrasse 1',
  postalCode: '10115',
  city: 'Berlin',
  country: 'DE',
  phone: '+49 30 0000000',
  email: 'smoke@example.invalid',
  currency: 'EUR',
  timeZoneId: 'Europe/Berlin',
  defaultCulture: 'de',
  supportedCultures: ['de', 'en', 'tr'],
  checkInFromLocal: '15:00',
  checkOutUntilLocal: '11:00',
  images: [],
  amenities: ['wifi'],
  booking: {
    minNights: 1,
    maxNights: 21,
    maxAdvanceDays: 365,
    minAdvanceHours: 0,
    maxAdults: 4,
    maxChildren: 3,
    confirmationMode: 'Instant',
  },
  cityTax: {
    applies: true,
    perPersonNight: 3.5,
    currency: 'EUR',
    childrenExempt: false,
    childAgeLimit: null,
    chargedOnlyIfStayTakesPlace: true,
  },
  cancellationPolicy: {
    type: 'Flexible',
    freeCancellationDaysBeforeArrival: 3,
    cutoffLocalTime: '18:00',
    lateCancellationFeePercent: 90,
    noShowFeePercent: 90,
    appliesToAccommodationOnly: true,
  },
  paymentOptions: [{ method: 'PayAtProperty', requiresGuarantee: false, description: null }],
};

const ROOM_TYPES_FIXTURE = [
  {
    code: 'VER',
    name: FIXTURE_ROOM_NAME,
    shortDescription: 'Nur fuer den Build-Test.',
    capacity: 2,
    sizeSqm: 20,
    amenities: ['wifi'],
    image: null,
    fromPrice: { amount: FIXTURE_PRICE, currency: 'EUR', basis: 'BasePrice' },
  },
];

const SSR_PORT = 41731;
const ORIGIN_PORT = 41732;

let ssr = null;
let origin = null;

try {
  ssr = spawn(process.execPath, [SSR_ENTRY], {
    env: { ...process.env, PORT: String(SSR_PORT), SSR_ALLOWED_HOSTS: `localhost` },
    stdio: ['ignore', 'pipe', 'pipe'],
  });
  ssr.stdout.on('data', () => {});
  ssr.stderr.on('data', (chunk) => console.error(`  [ssr] ${String(chunk).trim()}`));

  origin = createServer((incoming, response) => {
    const url = incoming.url ?? '/';

    if (url.startsWith('/api/')) {
      const body = url.endsWith('/room-types')
        ? ROOM_TYPES_FIXTURE
        : url.endsWith(`/hotels/${HOTEL_SLUG}`)
          ? HOTEL_FIXTURE
          : null;

      response.writeHead(body === null ? 404 : 200, { 'Content-Type': 'application/json' });
      response.end(body === null ? '{}' : JSON.stringify(body));
      return;
    }

    // Geri kalan her sey SSR sunucusuna; `Host` KORUNUR ki uygulama kendi
    // origin'ini bizim adresimiz sansin ve `/api` cagrilari buraya donsun.
    const proxied = httpRequest(
      {
        host: '127.0.0.1',
        port: SSR_PORT,
        path: url,
        method: incoming.method,
        headers: { ...incoming.headers, host: `localhost:${ORIGIN_PORT}` },
      },
      (proxyResponse) => {
        response.writeHead(proxyResponse.statusCode ?? 500, proxyResponse.headers);
        proxyResponse.pipe(response);
      },
    );
    proxied.on('error', (error) => {
      response.writeHead(502);
      response.end(String(error));
    });
    incoming.pipe(proxied);
  });

  await new Promise((done) => origin.listen(ORIGIN_PORT, '127.0.0.1', done));

  // SSR sunucusunun acilmasini bekle. Yoklama SOKET seviyesindedir: vekil
  // uzerinden yoklamak, SSR daha ayaga kalkmamisken 502'yi "hazir" sayardi;
  // HTTP ile yoklamak ise `Host` basligi degistirilemedigi icin sunucunun
  // SSRF korumasini tetikleyip derleme log'una sahte bir hata satiri yazardi.
  let ready = false;
  for (let attempt = 0; attempt < 80 && !ready; attempt++) {
    ready = await new Promise((done) => {
      const socket = connect({ host: '127.0.0.1', port: SSR_PORT });
      socket.setTimeout(1000);
      socket.on('connect', () => {
        socket.destroy();
        done(true);
      });
      socket.on('error', () => done(false));
      socket.on('timeout', () => {
        socket.destroy();
        done(false);
      });
    });
    if (!ready) {
      await new Promise((done) => setTimeout(done, 500));
    }
  }

  if (!ready) {
    fail('SSR sunucusu ayaga kalkmadi');
  } else {
    const response = await fetch(`http://localhost:${ORIGIN_PORT}/de`);
    const html = await response.text();
    const text = textOf(html);

    response.status === 200 ? pass('GET /de -> 200') : fail(`GET /de -> ${response.status}`);

    html.includes('data-testid="room-teaser"')
      ? pass('ana sayfa katalog kartlarini SUNUCUDA basiyor')
      : fail('ana sayfada katalog kartı YOK — SSR verisi HTML e girmemis');

    text.includes(FIXTURE_ROOM_NAME)
      ? pass('oda tipi adi HTML de')
      : fail(`oda tipi adi ("${FIXTURE_ROOM_NAME}") HTML de YOK`);

    // PAngV: "ab" fiyati crawler'a gorunmeli. Bicim dile gore degisir (de: 123,45).
    /123[.,]45/.test(text)
      ? pass('"ab" fiyati HTML de')
      : fail('"ab" fiyati HTML de YOK — katalog fiyatsiz uretilmis');

    html.includes('data-testid="error-panel"')
      ? fail('ana sayfada hata paneli var')
      : pass('hata paneli yok');
  }
} catch (error) {
  fail(`SSR smoke calistirilamadi: ${error.message}`);
} finally {
  origin?.close();
  ssr?.kill();
}

// ---------------------------------------------------------------------------

if (failures.length > 0) {
  console.error(`\nDerleme ciktisi denetimi BASARISIZ (${failures.length} bulgu).`);
  process.exit(1);
}

console.log('\nDerleme ciktisi denetimi tamam.');
