#!/usr/bin/env node
/**
 * `npm run build` sarmalayicisi — workspace'te IKI uygulama vardir.
 *
 * Angular CLI 22'de `defaultProject` kaldirildigi icin `ng build` (proje adi
 * olmadan) birden fazla uygulamali bir workspace'te hata verir. CI adimlarinin
 * (`npm run build`) degismeden calismasi icin bu betik her iki uygulamayi
 * sirayla derler ve ilk hatada durur.
 *
 * AYRICA: **prerender hatalari sessiz kalmaz.** Angular, prerender sirasinda bir
 * HTTP istegi duserse `Unable to handle request: '…'` yazip devam eder ve cikis
 * kodu 0 kalir; sonuc, icerigi eksik ama "basariyla" uretilmis bir sayfadir.
 * Gercekten oldu (ana sayfa katalogsuz prerender edildi). Bu yuzden cikti
 * yakalanir, oldugu gibi tekrar basilir ve bu desen gorulurse derleme KIRILIR.
 * Derinlemesine icerik denetimi ayri bir adimdadir: `npm run verify:build`.
 *
 * Ekstra bayraklar oldugu gibi iletilir:
 *   npm run build -- --configuration development
 */
import { spawn } from 'node:child_process';
import { createRequire } from 'node:module';

const PROJECTS = ['hotelcore-web', 'guest-web'];
const extraArgs = process.argv.slice(2);

/** Prerender/SSR sirasinda dusen istekler bu satirlari uretir. */
const FAILURE_PATTERNS = [/Unable to handle request/i, /Prerendering .* failed/i];

// Windows'ta `.cmd` sarmalayicilarini spawn etmek EINVAL uretir; bu yuzden
// Angular CLI'nin JS giris noktasi dogrudan Node ile calistirilir.
const require = createRequire(import.meta.url);
const ngBin = require.resolve('@angular/cli/bin/ng.js');

for (const project of PROJECTS) {
  const { code, output } = await run([ngBin, 'build', project, ...extraArgs]);

  if (code !== 0) {
    process.exit(code);
  }

  const offending = output
    .split(/\r?\n/)
    .filter((line) => FAILURE_PATTERNS.some((pattern) => pattern.test(line)));

  if (offending.length > 0) {
    console.error(
      `\n"${project}" derlemesi 0 ile bitti ama prerender istekleri DUSTU:\n` +
        offending.map((line) => `  ${line.trim()}`).join('\n') +
        '\n\nUretilen sayfalar bu verinin icerigini TASIMIYOR. Ya rota SSR olmali\n' +
        '(fiyat/musaitlik tasiyan sayfalar icin dogru cevap budur), ya da veri\n' +
        'derleme aninda saglanmali (hukuki metinlerde oldugu gibi: legal-snapshot).\n' +
        'Gerekce: projects/guest-web/src/app/app.routes.server.ts\n',
    );
    process.exit(1);
  }
}

function run(args) {
  return new Promise((resolve) => {
    const child = spawn(process.execPath, args, { stdio: ['inherit', 'pipe', 'pipe'] });
    let output = '';

    // Cikti hem toplanir hem AYNEN gecirilir: derleme log'u degismemeli.
    child.stdout.on('data', (chunk) => {
      output += chunk;
      process.stdout.write(chunk);
    });
    child.stderr.on('data', (chunk) => {
      output += chunk;
      process.stderr.write(chunk);
    });

    child.on('exit', (code) => resolve({ code: code ?? 1, output }));
  });
}
