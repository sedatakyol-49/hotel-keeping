#!/usr/bin/env node
/**
 * ===========================================================================
 * YAZI TIPI KAPISI — "Google'a giden istek" sessizce geri gelmesin
 * ===========================================================================
 *
 * Bu ihlal turu geri gelir: birisi bir ornekten kopyaladigi `<link
 * href="https://fonts.googleapis.com/...">` satirini index.html'e yapistirir,
 * her sey calisir, kimse fark etmez — ve sayfayi acan herkesin IP adresi onay
 * alinmadan bir ucuncu tarafa gitmeye baslar (LG Munchen I, 20.01.2022 —
 * 3 O 17493/20). Kod incelemesi bunu yakalamaz; derleme yakalamali.
 *
 * BES KAPI:
 *
 *  1) KAYNAK: hicbir kaynak dosyada ucuncu taraf yazi tipi barindiricisina
 *     referans yok (yorum satirlari haric — yorum istek uretmez).
 *
 *  2) CIKTI: `dist/**` icindeki hicbir metin dosyasinda ayni adresler yok.
 *     Bu ayri bir kapidir cunku Angular uretim derlemesi Google Fonts CSS'ini
 *     "inline" eder: `googleapis.com` istegi kaybolur ama `gstatic.com`
 *     adresleri HTML'in icine gomulur ve dosyalar yine oradan iner. Yani
 *     "ag kaydinda css2 istegi yok" tek basina hicbir sey kanitlamaz.
 *     SSR/prerender ciktisi da bu taramaya dahildir.
 *
 *  3) YUZ ENVANTERI: fonts.css'in bildirdigi her woff2 depoda var, manifestteki
 *     SHA-256 ile ayni, ve derlenmis her uygulamada servis ediliyor. Ayrica
 *     index.html'deki her `preload` hedefi gercekten var ve bildirilmis.
 *
 *  4) DIL KAPSAMI (asil tuzak): arayuz de/en/tr. `latin` alt kumesi Turkce
 *     `ğ Ğ ş Ş İ` harflerini ICERMEZ (`ı` istisnadir, o `latin`dedir). Kapsam
 *     dosyanin **cmap tablosundan** okunur — `unicode-range` bildirimi bir sey
 *     kanitlamaz, yalnizca hangi dosyanin indirilecegini soyler. Ceviri
 *     dosyalarindaki ve hukuki metinlerdeki her karakter icin hem cmap hem
 *     unicode-range kontrol edilir; kapsanmayanlar manifestteki gerekceli
 *     listeyle birebir esitse gecer, yeni bir bosluk cikarsa KIRILIR.
 *
 *  5) TEK KAYNAK: `@font-face` tanimlari yalnizca paylasilan katmanda
 *     (projects/shared/styles/fonts.css) bulunur — uygulama stillerine
 *     kopyalanmaz.
 *
 * Calistirma:
 *   npm run verify:fonts          (kaynak kapilari; dist varsa cikti kapilari)
 *   npm run verify:build          (derlemeden sonra; bu kapi oraya baglidir)
 */
import { existsSync, readFileSync, readdirSync, statSync } from 'node:fs';
import { dirname, extname, join, relative, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

import { inspectWoff2, parseUnicodeRange, rangesContain, sha256 } from './font-tools.mjs';

const HERE = dirname(fileURLToPath(import.meta.url));
const ROOT = resolve(HERE, '..');

const FONTS_CSS = join(ROOT, 'projects/shared/styles/fonts.css');
const FONT_DIR = join(ROOT, 'projects/shared/assets/fonts');
const MANIFEST = join(ROOT, 'scripts/fonts/manifest.json');

const INDEX_FILES = ['src/index.html', 'projects/guest-web/src/index.html'];

/** Ceviri ve hukuki metinler: sayfada gercekten cizilecek karakterler. */
const TEXT_SOURCES = [
  'public/i18n/de.json',
  'public/i18n/en.json',
  'public/i18n/tr.json',
  'projects/guest-web/src/i18n/de.json',
  'projects/guest-web/src/i18n/en.json',
  'projects/guest-web/src/i18n/tr.json',
  'projects/guest-web/src/generated/legal-snapshot.json',
];

/**
 * Ceviri dosyalari silinse bile dusmemesi gereken cekirdek.
 * TR: ı İ ğ Ğ ş Ş ç Ç ö Ö ü Ü — DE: ä ö ü ß ẞ ve tipografik noktalama.
 */
const REQUIRED_CHARACTERS = 'ıİğĞşŞçÇöÖüÜäÄöÖüÜßẞ€„“”–—…§«»';

const THIRD_PARTY_HOSTS = [
  'fonts.googleapis.com',
  'fonts.gstatic.com',
  'fonts.google.com',
  'use.typekit.net',
  'fonts.bunny.net',
  'cdn.jsdelivr.net',
  'unpkg.com',
  'cdnjs.cloudflare.com',
];

const SCANNED_EXTENSIONS = new Set([
  '.html',
  '.css',
  '.scss',
  '.ts',
  '.js',
  '.mjs',
  '.json',
  '.webmanifest',
]);

const SKIP_DIRECTORIES = new Set([
  'node_modules',
  '.angular',
  '.git',
  'dist',
  'out-tsc',
  'coverage',
]);

const failures = [];
const fail = (message) => {
  failures.push(message);
  console.error(`  FAIL  ${message}`);
};
const pass = (message) => console.log(`  ok    ${message}`);

/**
 * Yorumlar cikarilir: bir yorum satiri ag istegi uretemez.
 *
 * HTML yorumu HER dosya turunde temizlenir. Sebep somut: Angular, index.html'i
 * SSR paketine `dist/guest-web/server/assets-chunks/*.mjs` icinde bir sablon
 * dizesi olarak gomer ve HTML yorumlarini KORUR. Uzantiya bakarak temizlemek,
 * o dosyalari "kirli" gostererek kapiyi yanlis yere calistirirdi.
 */
function withoutComments(source, extension) {
  let text = source.replace(/<!--[\s\S]*?-->/g, ' ');
  if (
    extension === '.css' ||
    extension === '.scss' ||
    extension === '.ts' ||
    extension === '.js' ||
    extension === '.mjs'
  ) {
    text = text.replace(/\/\*[\s\S]*?\*\//g, ' ');
    text = text
      .split(/\r?\n/)
      .map((line) => (/^\s*(\/\/|\*)/.test(line) ? '' : line))
      .join('\n');
  }
  return text;
}

function walk(directory, files = []) {
  for (const entry of readdirSync(directory, { withFileTypes: true })) {
    if (entry.isDirectory()) {
      if (!SKIP_DIRECTORIES.has(entry.name)) {
        walk(join(directory, entry.name), files);
      }
      continue;
    }
    files.push(join(directory, entry.name));
  }
  return files;
}

function scanForThirdParty(files, { stripComments }) {
  const hits = [];
  for (const file of files) {
    const extension = extname(file);
    if (!SCANNED_EXTENSIONS.has(extension)) {
      continue;
    }
    let text = readFileSync(file, 'utf8');
    if (stripComments) {
      text = withoutComments(text, extension);
    }
    for (const host of THIRD_PARTY_HOSTS) {
      if (text.includes(host)) {
        const line = text.split(/\r?\n/).findIndex((entry) => entry.includes(host)) + 1;
        hits.push(`${relative(ROOT, file)}:${line} -> ${host}`);
      }
    }
  }
  return hits;
}

// ---------------------------------------------------------------------------
console.log('\n[1/5] Kaynakta ucuncu taraf yazi tipi barindiricisi');
// ---------------------------------------------------------------------------

const sourceFiles = [
  ...walk(join(ROOT, 'src')),
  ...walk(join(ROOT, 'projects')),
  ...walk(join(ROOT, 'public')),
].filter((file) => file !== resolve(HERE, 'verify-fonts.mjs'));

const sourceHits = scanForThirdParty(sourceFiles, { stripComments: true });
if (sourceHits.length > 0) {
  fail(
    `Kaynakta ucuncu taraf yazi tipi referansi var:\n        ${sourceHits.join('\n        ')}\n` +
      "        Yazi tipleri depoda ve kendi origin'imizden servis edilir\n" +
      '        (projects/shared/styles/fonts.css). Google Fonts <link>/@import YASAKTIR.',
  );
} else {
  pass(`${sourceFiles.length} kaynak dosya tarandi — ucuncu taraf yazi tipi referansi yok`);
}

for (const index of INDEX_FILES) {
  const html = withoutComments(readFileSync(join(ROOT, index), 'utf8'), '.html');
  if (/<link[^>]+rel=["']?preconnect/i.test(html)) {
    fail(`${index}: preconnect kalmis — hedefi ne olursa olsun ucuncu taraf isareti`);
  } else {
    pass(`${index}: preconnect yok`);
  }
}

// ---------------------------------------------------------------------------
console.log('\n[2/5] fonts.css yuz envanteri ve manifest ozetleri');
// ---------------------------------------------------------------------------

const fontsCss = readFileSync(FONTS_CSS, 'utf8');
const manifest = JSON.parse(readFileSync(MANIFEST, 'utf8'));

/** fonts.css icindeki gercek @font-face bloklari (yorumlar cikarildi). */
const faceBlocks = [...withoutComments(fontsCss, '.css').matchAll(/@font-face\s*\{([^}]*)\}/g)].map(
  (match) => match[1],
);

const declaredFaces = [];
for (const block of faceBlocks) {
  const url = /url\(['"]?([^'")]+)['"]?\)/.exec(block);
  if (url === null) {
    continue; // metrik esli yedek yuz (yalnizca local())
  }
  declaredFaces.push({
    file: url[1].replace(/^\/fonts\//, ''),
    family: /font-family:\s*['"]([^'"]+)['"]/.exec(block)?.[1] ?? '?',
    weight: Number(/font-weight:\s*(\d+)/.exec(block)?.[1] ?? 0),
    style: /font-style:\s*(\w+)/.exec(block)?.[1] ?? 'normal',
    display: /font-display:\s*(\w+)/.exec(block)?.[1] ?? null,
    unicodeRange: /unicode-range:\s*([^;]+);/.exec(block)?.[1]?.replace(/\s+/g, ' ').trim() ?? null,
  });
}

const manifestFiles = manifest.families.flatMap((family) =>
  family.faces.map((face) => ({ ...face, family: family.family })),
);

if (declaredFaces.length !== manifestFiles.length) {
  fail(
    `fonts.css ${declaredFaces.length} yuz bildiriyor, manifest ${manifestFiles.length} yuz sayiyor — biri guncellenmemis`,
  );
} else {
  pass(`${declaredFaces.length} indirilebilir yuz bildirildi (manifest ile ayni sayida)`);
}

for (const face of declaredFaces) {
  const path = join(FONT_DIR, face.file);
  if (!existsSync(path)) {
    fail(`${face.file}: fonts.css bildiriyor ama dosya depoda YOK`);
    continue;
  }
  if (face.display !== 'swap') {
    fail(`${face.file}: font-display "${face.display}" — beklenen "swap" (FOIT yok)`);
  }
  if (face.unicodeRange === null) {
    fail(`${face.file}: unicode-range yok — alt kume bolme etkisiz kalir`);
  }
  const entry = manifestFiles.find((candidate) => candidate.file === face.file);
  if (entry === undefined) {
    fail(`${face.file}: manifestte kayitli degil (kaynak/lisans belirsiz)`);
    continue;
  }
  const digest = sha256(path);
  if (digest !== entry.sha256) {
    fail(`${face.file}: SHA-256 manifestten farkli — dosya degistirilmis\n        ${digest}`);
  }
  const bytes = statSync(path).size;
  if (bytes !== entry.bytes) {
    fail(`${face.file}: boyut ${bytes} B, manifest ${entry.bytes} B`);
  }
}

for (const family of manifest.families) {
  const licenseFile = join(FONT_DIR, family.license.file);
  if (!existsSync(licenseFile)) {
    fail(`${family.family}: lisans dosyasi eksik (${family.license.file})`);
  } else if (!readFileSync(licenseFile, 'utf8').includes('SIL OPEN FONT LICENSE')) {
    fail(`${family.family}: lisans dosyasi SIL OFL metnini icermiyor`);
  } else {
    pass(`${family.family} — ${family.license.id}, ${family.license.file}`);
  }
}

// Preload hedefleri gercek mi?
for (const index of INDEX_FILES) {
  const html = withoutComments(readFileSync(join(ROOT, index), 'utf8'), '.html');
  const preloads = [...html.matchAll(/<link[^>]*rel="preload"[^>]*>/g)]
    .map((match) => /href="([^"]+)"/.exec(match[0])?.[1])
    .filter((href) => href !== undefined && href.includes('font'));

  if (preloads.length === 0) {
    fail(`${index}: kritik yuzler icin preload yok`);
  }
  for (const href of preloads) {
    const file = href.replace(/^\/?fonts\//, '');
    if (!declaredFaces.some((face) => face.file === file)) {
      fail(`${index}: preload "${href}" fonts.css'te bildirilen bir yuze karsilik gelmiyor`);
    } else if (!existsSync(join(FONT_DIR, file))) {
      fail(`${index}: preload "${href}" -> dosya yok`);
    }
  }
  pass(`${index}: ${preloads.length} preload hedefi dogrulandi`);
}

// @font-face yalnizca paylasilan katmanda mi?
const strayFaces = sourceFiles.filter(
  (file) =>
    (extname(file) === '.css' || extname(file) === '.scss') &&
    file !== FONTS_CSS &&
    withoutComments(readFileSync(file, 'utf8'), extname(file)).includes('@font-face'),
);
if (strayFaces.length > 0) {
  fail(
    `@font-face paylasilan katman disinda tanimlanmis: ${strayFaces
      .map((file) => relative(ROOT, file))
      .join(', ')}`,
  );
} else {
  pass('@font-face yalnizca projects/shared/styles/fonts.css icinde');
}

// ---------------------------------------------------------------------------
console.log('\n[3/5] Dil kapsami — de / en / tr (cmap + unicode-range)');
// ---------------------------------------------------------------------------

const repertoire = new Map();
const remember = (text, source) => {
  for (const character of text) {
    const codePoint = character.codePointAt(0);
    if (codePoint < 0x20) {
      continue; // kontrol karakterleri cizilmez
    }
    if (!repertoire.has(codePoint)) {
      repertoire.set(codePoint, source);
    }
  }
};

for (const source of TEXT_SOURCES) {
  const path = join(ROOT, source);
  if (!existsSync(path)) {
    fail(`Metin kaynagi yok: ${source}`);
    continue;
  }
  const walkJson = (node) => {
    if (typeof node === 'string') {
      remember(node.replace(/<[^>]*>/g, ' '), source);
    } else if (node !== null && typeof node === 'object') {
      for (const value of Object.values(node)) {
        walkJson(value);
      }
    }
  };
  walkJson(JSON.parse(readFileSync(path, 'utf8')));
}
remember(REQUIRED_CHARACTERS, 'zorunlu cekirdek (de/tr)');

pass(`${repertoire.size} ayri kod noktasi denetlenecek (ceviriler + hukuki metinler)`);

const asUnicode = (codePoint) => `U+${codePoint.toString(16).toUpperCase().padStart(4, '0')}`;

for (const family of manifest.families) {
  const covered = new Set();
  const ranges = [];

  for (const face of family.faces) {
    const declaration = declaredFaces.find((candidate) => candidate.file === face.file);
    if (declaration === undefined) {
      continue;
    }
    const info = inspectWoff2(join(FONT_DIR, face.file));
    const faceRanges = parseUnicodeRange(declaration.unicodeRange ?? '');
    ranges.push(...faceRanges);
    for (const codePoint of info.codePoints) {
      // Bir glif, ancak hem dosyada VAR hem de bildirilen aralikta ise cizilir.
      if (rangesContain(faceRanges, codePoint)) {
        covered.add(codePoint);
      }
    }
  }

  const uncovered = [...repertoire.keys()]
    .filter((codePoint) => !covered.has(codePoint))
    .sort((a, b) => a - b)
    .map(asUnicode);

  const known = (manifest.knownUncovered[family.family] ?? []).map((entry) => entry.codePoint);
  const unexpected = uncovered.filter((entry) => !known.includes(entry));
  const stale = known.filter((entry) => !uncovered.includes(entry));

  if (unexpected.length > 0) {
    fail(
      `${family.family}: metinlerde gecen ama yazi tipinde OLMAYAN karakter(ler): ` +
        unexpected
          .map((entry) => {
            const codePoint = parseInt(entry.slice(2), 16);
            return `"${String.fromCodePoint(codePoint)}" ${entry} (kaynak: ${repertoire.get(codePoint)})`;
          })
          .join(', ') +
        '\n        Tarayici bu karakteri YEDEK yazi tipiyle cizer; kelime ortasinda\n' +
        '        iki yazi tipi gorunur. Ya metin degistirilmeli, ya alt kume\n' +
        '        genisletilmeli, ya da manifest.knownUncovered gerekceyle guncellenmeli.',
    );
  } else if (stale.length > 0) {
    fail(
      `${family.family}: manifest.knownUncovered'da artik gecersiz kayit(lar): ${stale.join(', ')}`,
    );
  } else {
    pass(
      `${family.family}: ${repertoire.size - uncovered.length}/${repertoire.size} karakter kapsandi` +
        (known.length > 0 ? ` (bilinen istisna: ${known.join(', ')})` : ''),
    );
  }

  // Turkce cekirdegi ayrica ve acikca sinanir — bu dosyanin varlik sebebi.
  const turkish = [...'ıİğĞşŞçÇöÖüÜ'].filter((character) => !covered.has(character.codePointAt(0)));
  if (turkish.length > 0) {
    fail(`${family.family}: Turkce harfler eksik -> ${turkish.join(' ')} (latin-ext tasinmali)`);
  } else {
    pass(`${family.family}: Turkce cekirdek tam (ı İ ğ Ğ ş Ş ç Ç ö Ö ü Ü)`);
  }

  if (!ranges.some((range) => range[0] <= 0x011f && range[1] >= 0x011f)) {
    fail(`${family.family}: unicode-range latin-ext'i bildirmiyor — dosya hic indirilmez`);
  }
}

// ---------------------------------------------------------------------------
console.log('\n[4/5] Derleme ciktisi (dist)');
// ---------------------------------------------------------------------------

const distRoot = join(ROOT, 'dist');
if (!existsSync(distRoot)) {
  console.log('  --    dist yok, cikti kapilari atlandi (once npm run build)');
} else {
  const distFiles = walk(distRoot);
  const distHits = scanForThirdParty(distFiles, { stripComments: true });

  if (distHits.length > 0) {
    fail(
      `Derleme ciktisinda ucuncu taraf yazi tipi adresi var:\n        ${distHits.join('\n        ')}\n` +
        "        Not: Angular, Google Fonts CSS'ini inline eder; css2 istegi\n" +
        "        kaybolur ama gstatic adresleri HTML'e gomulur. Kapi budur.",
    );
  } else {
    pass(`${distFiles.length} cikti dosyasi tarandi — googleapis/gstatic yok`);
  }

  for (const browserDirectory of ['dist/hotelcore-web/browser', 'dist/guest-web/browser']) {
    const directory = join(ROOT, browserDirectory);
    if (!existsSync(directory)) {
      fail(`${browserDirectory} yok — iki uygulama da derlenmeli`);
      continue;
    }
    const missing = declaredFaces.filter(
      (face) => !existsSync(join(directory, 'fonts', face.file)),
    );
    if (missing.length > 0) {
      fail(
        `${browserDirectory}: ${missing.length} yuz servis edilmiyor (${missing
          .map((face) => face.file)
          .join(', ')})`,
      );
    } else {
      pass(`${browserDirectory}: ${declaredFaces.length} yuz servis ediliyor`);
    }
  }

  // SSR/prerender ciktisi ayrica: sunucudan cikan HTML de temiz olmali.
  const prerendered = walk(join(ROOT, 'dist/guest-web')).filter((file) =>
    file.endsWith('index.html'),
  );
  const dirty = prerendered.filter((file) => {
    const html = withoutComments(readFileSync(file, 'utf8'), '.html');
    return THIRD_PARTY_HOSTS.some((host) => html.includes(host));
  });
  if (dirty.length > 0) {
    fail(`SSR/prerender HTML'inde ucuncu taraf adresi: ${dirty.length} sayfa`);
  } else if (prerendered.length > 0) {
    pass(`${prerendered.length} prerender/SSR HTML sayfasi temiz`);
  }
}

// ---------------------------------------------------------------------------
console.log('\n[5/5] Ozet');
// ---------------------------------------------------------------------------

const totalBytes = declaredFaces.reduce(
  (sum, face) => sum + statSync(join(FONT_DIR, face.file)).size,
  0,
);
console.log(
  `        ${declaredFaces.length} yuz / ${(totalBytes / 1024).toFixed(1)} kB depoda; ` +
    `kritik yol (latin 400 x3): ` +
    `${(
      [
        'ibm-plex-sans-latin-400-normal.woff2',
        'ibm-plex-mono-latin-400-normal.woff2',
        'instrument-serif-latin-400-normal.woff2',
      ]
        .map((file) => statSync(join(FONT_DIR, file)).size)
        .reduce((a, b) => a + b, 0) / 1024
    ).toFixed(1)} kB`,
);

if (failures.length > 0) {
  console.error(`\nYazi tipi kapisi BASARISIZ (${failures.length} bulgu).`);
  process.exit(1);
}

console.log('\nYazi tipi kapisi tamam — ucuncu tarafa giden yazi tipi istegi yok.');
