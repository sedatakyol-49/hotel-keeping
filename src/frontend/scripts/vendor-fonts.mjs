#!/usr/bin/env node
/**
 * ===========================================================================
 * YAZI TIPI VENDOR'LAMA — depodaki woff2 dosyalari nereden geldi
 * ===========================================================================
 *
 * Yazi tipleri artik Google'dan inmiyor, depoda duruyor (gerekce:
 * projects/shared/styles/fonts.css). "Depoda duruyor" demek "nereden geldigi
 * belirsiz" demek olmamali: bu betik, dosyalari **yeniden uretmenin** kayitli
 * yoludur.
 *
 * Kaynak olarak `@fontsource/*` npm paketleri kullanilir. Bu paketler Google
 * Fonts'un alt kume (subset) ciktilarini birebir yeniden yayimlar ve OFL lisans
 * metnini de tasir; boylece "hangi surum, hangi alt kume, hangi lisans"
 * sorularinin hepsi bir paket surumune baglanir. Paketler bagimlilik olarak
 * EKLENMEZ (`npm pack` ile tek seferlik indirilir) — uretim bagimliligi
 * listesini bir yazi tipi icin buyutmenin anlami yok.
 *
 * Calistirma:
 *   node scripts/vendor-fonts.mjs           # indir, kopyala, manifest ozetlerini tazele
 *   node scripts/vendor-fonts.mjs --check   # dosyalar upstream ile ayni mi (degistirme)
 *
 * Kapi ayridir: `npm run verify:fonts`.
 */
import { execFileSync } from 'node:child_process';
import {
  copyFileSync,
  mkdirSync,
  mkdtempSync,
  readFileSync,
  rmSync,
  statSync,
  writeFileSync,
} from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

import { sha256 } from './font-tools.mjs';

const HERE = dirname(fileURLToPath(import.meta.url));
const ROOT = resolve(HERE, '..');
const FONT_DIR = join(ROOT, 'projects/shared/assets/fonts');
const MANIFEST = join(ROOT, 'scripts/fonts/manifest.json');

const check = process.argv.includes('--check');
const manifest = JSON.parse(readFileSync(MANIFEST, 'utf8'));

const workspace = mkdtempSync(join(tmpdir(), 'hotelcore-fonts-'));
let problems = 0;

try {
  for (const family of manifest.families) {
    const [name, version] = [
      family.package.slice(0, family.package.lastIndexOf('@')),
      family.package.slice(family.package.lastIndexOf('@') + 1),
    ];

    console.log(`\n${family.family}  <-  ${name}@${version}  (${family.license.id})`);

    execFileSync('npm', ['pack', `${name}@${version}`, '--silent'], {
      cwd: workspace,
      stdio: ['ignore', 'ignore', 'inherit'],
      shell: process.platform === 'win32',
    });

    const tarball = `${name.replace('@', '').replace('/', '-')}-${version}.tgz`;
    const directory = family.family.replace(/\s+/g, '-');
    const extracted = join(workspace, directory);
    mkdirSync(extracted, { recursive: true });

    /*
     * Yollar GORELI verilir ve `cwd` calisma dizinine ayarlanir. Sebep Windows:
     * PATH'te GNU tar varsa (Git for Windows) `C:\...` bicimindeki mutlak yolu
     * "uzak sunucu" saniyor ve "Cannot connect to C: resolve failed" diyor.
     * Goreli yol her iki tar uygulamasinda da (bsdtar / GNU tar) calisir.
     */
    execFileSync('tar', ['-xzf', tarball, '-C', directory], {
      cwd: workspace,
      stdio: 'inherit',
    });

    const upstreamFiles = join(extracted, 'package/files');
    const upstreamLicense = join(extracted, 'package/LICENSE');

    // Lisans metni: her zaman yazi tipiyle BIRLIKTE tasinir (OFL kosulu).
    compareOrCopy(upstreamLicense, join(FONT_DIR, family.license.file));

    for (const face of family.faces) {
      const source = join(upstreamFiles, face.file);
      const target = join(FONT_DIR, face.file);
      compareOrCopy(source, target);

      if (!check) {
        face.bytes = statSync(target).size;
        face.sha256 = sha256(target);
      } else if (sha256(target) !== face.sha256) {
        console.error(`  FARKLI  ${face.file}: manifest ozeti tutmuyor`);
        problems++;
      }
    }
  }

  if (!check) {
    manifest.vendoredAt = new Date().toISOString().slice(0, 10);
    writeFileSync(MANIFEST, `${JSON.stringify(manifest, null, 2)}\n`);
    console.log(`\nmanifest tazelendi: ${MANIFEST}`);
  }
} finally {
  rmSync(workspace, { recursive: true, force: true });
}

function compareOrCopy(source, target) {
  const sourceDigest = sha256(source);
  let targetDigest = null;
  try {
    targetDigest = sha256(target);
  } catch {
    targetDigest = null;
  }

  if (sourceDigest === targetDigest) {
    console.log(`  ayni    ${target.replace(ROOT, '.')}`);
    return;
  }
  if (check) {
    console.error(`  FARKLI  ${target.replace(ROOT, '.')} — upstream ile ayni degil`);
    problems++;
    return;
  }
  mkdirSync(dirname(target), { recursive: true });
  copyFileSync(source, target);
  console.log(`  yazildi ${target.replace(ROOT, '.')}`);
}

if (problems > 0) {
  console.error(`\n${problems} dosya upstream ile ayni degil.`);
  process.exit(1);
}
