#!/usr/bin/env node
/**
 * ===========================================================================
 * HUKUKI METIN ANLIK GORUNTUSU (§5 DDG)
 * ===========================================================================
 *
 * `GET /api/v1/public/hotels/{slug}/legal` yanitini uc dil icin diske yazar.
 * Ciktiyi PRERENDER kullanir: hukuki sayfalar derleme aninda uretilir ve
 * icerik HTML'in ICINE gomulur, boylece JavaScript calistirmayan bir ziyaretci
 * (ve Impressum'u denetleyen bir makam) sayfayi bos gormez. §5 DDG kunyenin
 * "unmittelbar erreichbar" olmasini ister; istemcide doldurulan bir sayfa bu
 * kosulu saglamaz.
 *
 * NEDEN ANLIK GORUNTU, NEDEN DERLEME ANINDA CANLI API DEGIL:
 *  - Derleme, calisan bir API + veritabani gerektirmez. Frontend is akisi
 *    kendi basina yesil kalir; API'nin o an ayakta olmamasi bir dagitimi
 *    engellemez ve CI'a sir/servis eklemez.
 *  - Uretilen dosya depoda durur: bir Impressum degisikligi **gozden gecirilebilir
 *    bir diff** olarak gorunur. Derleme aninda cekilen icerikte boyle bir iz olmaz.
 *  - Deterministiktir: ayni commit her yerde ayni HTML'i uretir.
 *
 * BEDELI: metin degisirse anlik goruntu tazelenene kadar prerender edilmis
 * sayfa eski kalir. Bu yuzden (a) tarayici hidrasyondan sonra CANLI veriyi
 * ceker ve icerik guncellenir, (b) anlik goruntu belgelerin `version` alanini
 * tasir, (c) bu betik hukuki metin degistiginde yeniden calistirilir:
 *
 *     npm run legal:snapshot                      # varsayilan: http://localhost:5080
 *     GUEST_API_TARGET=http://localhost:5081 npm run legal:snapshot
 *     GUEST_HOTEL_SLUG=berlin-mitte npm run legal:snapshot
 *
 * `--check` modu ag erisimi olmadan calisir: dosyanin var oldugunu, uc dili de
 * tasidigini ve kunyenin dolu oldugunu dogrular (CI bu modu kullanir).
 */
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const HERE = dirname(fileURLToPath(import.meta.url));
const OUTPUT = resolve(HERE, '../projects/guest-web/src/generated/legal-snapshot.json');
const CULTURES = ['de', 'en', 'tr'];

const target = (process.env.GUEST_API_TARGET ?? 'http://localhost:5080').replace(/\/+$/, '');
const slug = process.env.GUEST_HOTEL_SLUG ?? 'berlin-mitte';

if (process.argv.includes('--check')) {
  check();
} else {
  await generate();
}

function check() {
  let snapshot;
  try {
    snapshot = JSON.parse(readFileSync(OUTPUT, 'utf8'));
  } catch (error) {
    fail(`Anlik goruntu okunamadi (${OUTPUT}): ${error.message}\n` + '`npm run legal:snapshot` ile uretin.');
  }

  for (const culture of CULTURES) {
    const entry = snapshot.documents?.[culture];
    if (!entry?.imprint?.legalEntityName) {
      fail(`Anlik goruntude "${culture}" dili icin kunye (imprint) yok.`);
    }
    if (!Array.isArray(entry.documents) || entry.documents.length === 0) {
      fail(`Anlik goruntude "${culture}" dili icin hukuki belge yok.`);
    }
  }

  console.log(
    `legal-snapshot.json tamam: ${CULTURES.join(', ')} · otel "${snapshot.hotelSlug}" · ` +
      `uretim ${snapshot.generatedAt}`,
  );
}

async function generate() {
  const documents = {};

  for (const culture of CULTURES) {
    const url = `${target}/api/v1/public/hotels/${encodeURIComponent(slug)}/legal`;
    let response;
    try {
      response = await fetch(url, { headers: { 'Accept-Language': culture } });
    } catch (error) {
      fail(`API'ye ulasilamadi (${url}): ${error.message}\n` + 'GUEST_API_TARGET dogru mu, API ayakta mi?');
    }

    if (!response.ok) {
      fail(
        `API ${response.status} dondu (${url}).` +
          (response.status === 404
            ? ' Public kanal bu otel icin kapali olabilir (PublicBookingSettings.IsEnabled).'
            : ''),
      );
    }

    documents[culture] = await response.json();
  }

  const snapshot = {
    '//': 'URETILMIS DOSYA — elle duzenlemeyin. Kaynak: GET /public/hotels/{slug}/legal. Yenilemek icin: npm run legal:snapshot',
    hotelSlug: slug,
    generatedAt: new Date().toISOString(),
    documents,
  };

  mkdirSync(dirname(OUTPUT), { recursive: true });
  writeFileSync(OUTPUT, JSON.stringify(snapshot, null, 2) + '\n', 'utf8');

  console.log(`legal-snapshot.json yazildi: ${OUTPUT}`);
}

function fail(message) {
  console.error(message);
  process.exit(1);
}
