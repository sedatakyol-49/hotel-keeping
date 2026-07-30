#!/usr/bin/env node
/**
 * `npm run build` sarmalayicisi — workspace'te IKI uygulama vardir.
 *
 * Angular CLI 22'de `defaultProject` kaldirildigi icin `ng build` (proje adi
 * olmadan) birden fazla uygulamali bir workspace'te hata verir. CI adimlarinin
 * (`npm run build`) degismeden calismasi icin bu betik her iki uygulamayi
 * sirayla derler ve ilk hatada durur.
 *
 * Ekstra bayraklar oldugu gibi iletilir:
 *   npm run build -- --configuration development
 */
import { spawn } from 'node:child_process';
import { createRequire } from 'node:module';

const PROJECTS = ['hotelcore-web', 'guest-web'];
const extraArgs = process.argv.slice(2);

// Windows'ta `.cmd` sarmalayicilarini spawn etmek EINVAL uretir; bu yuzden
// Angular CLI'nin JS giris noktasi dogrudan Node ile calistirilir.
const require = createRequire(import.meta.url);
const ngBin = require.resolve('@angular/cli/bin/ng.js');

for (const project of PROJECTS) {
  const code = await run([ngBin, 'build', project, ...extraArgs]);
  if (code !== 0) {
    process.exit(code);
  }
}

function run(args) {
  return new Promise((resolve) => {
    const child = spawn(process.execPath, args, { stdio: 'inherit' });
    child.on('exit', (code) => resolve(code ?? 1));
  });
}
