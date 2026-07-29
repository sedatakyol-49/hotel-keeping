#!/usr/bin/env node
/**
 * `npm run test` sarmalayicisi.
 *
 * Angular 22 birim test builder'i Vitest kullanir, ancak CLI bayraklari
 * Vitest'inkilerle birebir ayni degildir. Bu betik Vitest aliskanligindan
 * gelen `--run` / `--watch` bayraklarini Angular'in `--watch=false|true`
 * secenegine cevirir ve varsayilan olarak tek seferlik (CI dostu) calisir.
 *
 * Ornekler:
 *   npm run test            -> ng test --watch=false
 *   npm run test -- --run   -> ng test --watch=false
 *   npm run test -- --watch -> ng test --watch=true
 */
import { spawn } from 'node:child_process';
import { createRequire } from 'node:module';

const raw = process.argv.slice(2);
const args = [];
let watch = false;

for (const arg of raw) {
  if (arg === '--run' || arg === '--watch=false' || arg === '--no-watch') {
    watch = false;
  } else if (arg === '--watch' || arg === '-w' || arg === '--watch=true') {
    watch = true;
  } else {
    args.push(arg);
  }
}

// Windows'ta `.cmd` sarmalayicilarini spawn etmek EINVAL uretir; bu yuzden
// Angular CLI'nin JS giris noktasi dogrudan Node ile calistirilir.
const require = createRequire(import.meta.url);
const ngBin = require.resolve('@angular/cli/bin/ng.js');

const child = spawn(process.execPath, [ngBin, 'test', `--watch=${watch}`, ...args], {
  stdio: 'inherit',
});

child.on('exit', (code) => process.exit(code ?? 1));
