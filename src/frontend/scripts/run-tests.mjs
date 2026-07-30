#!/usr/bin/env node
/**
 * `npm run test` sarmalayicisi.
 *
 * Iki gorevi var:
 *
 * 1) Angular 22 birim test builder'i Vitest kullanir, ancak CLI bayraklari
 *    Vitest'inkilerle birebir ayni degildir. Bu betik Vitest aliskanligindan
 *    gelen `--run` / `--watch` bayraklarini Angular'in `--watch=false|true`
 *    secenegine cevirir ve varsayilan olarak tek seferlik (CI dostu) calisir.
 *
 * 2) Workspace'te IKI uygulama vardir (yonetim paneli + misafir sitesi) ve
 *    Angular 22'de `defaultProject` kaldirildigi icin `ng test` proje adi
 *    olmadan calismaz. Betik projeleri sirayla calistirir ve ilk hatada durur.
 *    Boylece CI adimi (`npm run test -- --run`) degismeden her iki uygulamayi
 *    da kapsar.
 *
 * Ornekler:
 *   npm run test                       -> her iki proje, watch kapali
 *   npm run test -- --run              -> ayni
 *   npm run test -- --watch            -> watch acik
 *   npm run test -- --project=guest-web
 */
import { spawn } from 'node:child_process';
import { createRequire } from 'node:module';

const ALL_PROJECTS = ['hotelcore-web', 'guest-web'];

const raw = process.argv.slice(2);
const args = [];
let watch = false;
let projects = ALL_PROJECTS;

for (const arg of raw) {
  if (arg === '--run' || arg === '--watch=false' || arg === '--no-watch') {
    watch = false;
  } else if (arg === '--watch' || arg === '-w' || arg === '--watch=true') {
    watch = true;
  } else if (arg.startsWith('--project=')) {
    projects = [arg.slice('--project='.length)];
  } else {
    args.push(arg);
  }
}

// Windows'ta `.cmd` sarmalayicilarini spawn etmek EINVAL uretir; bu yuzden
// Angular CLI'nin JS giris noktasi dogrudan Node ile calistirilir.
const require = createRequire(import.meta.url);
const ngBin = require.resolve('@angular/cli/bin/ng.js');

for (const project of projects) {
  console.log(`\n=== ng test ${project} (watch=${watch}) ===\n`);
  const code = await run([ngBin, 'test', project, `--watch=${watch}`, ...args]);
  if (code !== 0) {
    process.exit(code);
  }
}

function run(commandArgs) {
  return new Promise((resolve) => {
    const child = spawn(process.execPath, commandArgs, { stdio: 'inherit' });
    child.on('exit', (code) => resolve(code ?? 1));
  });
}
