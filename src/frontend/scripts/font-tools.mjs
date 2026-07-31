/**
 * ===========================================================================
 * WOFF2 OKUYUCU — "yazi tipi gercekten o harfi tasiyor mu?" sorusunun cevabi
 * ===========================================================================
 *
 * Neden kendi okuyucumuz var:
 *
 * Bir @font-face bildirimindeki `unicode-range`, tarayiciya "bu dosyayi su
 * araliklar icin indir" der. **Dosyanin o araliktaki her harfi tasidigini
 * garanti ETMEZ.** Google'in alt kume (subset) dosyalarinda aralik ile gercek
 * kapsam ayrisir: `latin-ext` araligi U+0100-02BA'yi kapsar, ama dosyada
 * yalnizca ailenin sahiden cizdigi glifler bulunur. Aradaki fark sessizdir —
 * tarayici eksik harfi yedek yazi tipiyle cizer, arayuz tek kelimenin ortasinda
 * iki farkli yazi tipine boluner. Turkce'de bu tam olarak "ğ ş İ" harflerinde
 * olur ve gozden kacar.
 *
 * Bu yuzden kapsam **dosyanin cmap tablosundan** okunur, aralik bildiriminden
 * degil. Ayrica metrikler (head/hhea/OS2) buradan alinir; yedek yuzey
 * ayarlamasi (size-adjust, ascent-override) icin gerekir.
 *
 * WOFF2 bicimi (W3C): baslik + tablo dizini + TEK bir brotli akisi. Tablolarin
 * govdeleri akis icinde dizin sirasiyla ard arda durur. `glyf`/`loca`/`hmtx`
 * donusturulmus olabilir; `cmap`, `head`, `hhea`, `OS/2` ASLA donusturulmez —
 * bu yuzden brotli acildiktan sonra dogrudan okunabilirler.
 */
import { brotliDecompressSync } from 'node:zlib';
import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';

/** WOFF2 spesifikasyonundaki sabit tablo etiketi tablosu (indeks 0..62). */
const KNOWN_TAGS = [
  'cmap',
  'head',
  'hhea',
  'hmtx',
  'maxp',
  'name',
  'OS/2',
  'post',
  'cvt ',
  'fpgm',
  'glyf',
  'loca',
  'prep',
  'CFF ',
  'VORG',
  'EBDT',
  'EBLC',
  'gasp',
  'hdmx',
  'kern',
  'LTSH',
  'PCLT',
  'VDMX',
  'vhea',
  'vmtx',
  'BASE',
  'GDEF',
  'GPOS',
  'GSUB',
  'EBSC',
  'JSTF',
  'MATH',
  'CBDT',
  'CBLC',
  'COLR',
  'CPAL',
  'SVG ',
  'sbix',
  'acnt',
  'avar',
  'bdat',
  'bloc',
  'bsln',
  'cvar',
  'fdsc',
  'feat',
  'fmtx',
  'fvar',
  'gvar',
  'hsty',
  'just',
  'lcar',
  'mort',
  'morx',
  'opbd',
  'prop',
  'trak',
  'Zapf',
  'Silf',
  'Glat',
  'Gloc',
  'Feat',
  'Sill',
];

/** WOFF2'nin degisken uzunluklu tamsayisi (UIntBase128). */
function readBase128(buffer, offset) {
  let value = 0;
  for (let index = 0; index < 5; index++) {
    const byte = buffer[offset + index];
    value = value * 128 + (byte & 0x7f);
    if ((byte & 0x80) === 0) {
      return { value, offset: offset + index + 1 };
    }
  }
  throw new Error('Bozuk UIntBase128');
}

/**
 * WOFF2 dosyasini acar ve `{ tag -> Buffer }` dondurur.
 * Donusturulmus tablolar (glyf/loca) oldugu gibi birakilir; onlara bakmiyoruz.
 */
export function readWoff2Tables(filePath) {
  const file = readFileSync(filePath);

  if (file.toString('latin1', 0, 4) !== 'wOF2') {
    throw new Error(`${filePath}: WOFF2 imzasi yok`);
  }

  const numTables = file.readUInt16BE(12);
  let cursor = 48;
  const directory = [];

  for (let index = 0; index < numTables; index++) {
    const flags = file[cursor];
    cursor += 1;

    const tagIndex = flags & 0x3f;
    let tag;
    if (tagIndex === 0x3f) {
      tag = file.toString('latin1', cursor, cursor + 4);
      cursor += 4;
    } else {
      tag = KNOWN_TAGS[tagIndex];
    }

    const original = readBase128(file, cursor);
    cursor = original.offset;

    // Donusum sayisi 0..3; `glyf`/`loca` icin 0 = donusturulmus,
    // diger tablolar icin 0 = donusturulmemis (spesifikasyon §5.3).
    const transformVersion = (flags >> 6) & 0x03;
    const transformed =
      tag === 'glyf' || tag === 'loca' ? transformVersion === 0 : transformVersion !== 0;

    let length = original.value;
    if (transformed) {
      const transform = readBase128(file, cursor);
      cursor = transform.offset;
      length = transform.value;
    }

    directory.push({ tag, length });
  }

  const decompressed = brotliDecompressSync(file.subarray(cursor));

  const tables = {};
  let position = 0;
  for (const entry of directory) {
    tables[entry.tag] = decompressed.subarray(position, position + entry.length);
    position += entry.length;
  }
  return tables;
}

/** cmap tablosundan kapsanan kod noktalarinin kumesi. */
function codePointsFromCmap(cmap) {
  const covered = new Set();
  const numSubtables = cmap.readUInt16BE(2);
  const subtableOffsets = new Set();

  for (let index = 0; index < numSubtables; index++) {
    const record = 4 + index * 8;
    subtableOffsets.add(cmap.readUInt32BE(record + 4));
  }

  for (const offset of subtableOffsets) {
    const format = cmap.readUInt16BE(offset);

    if (format === 4) {
      const segCountX2 = cmap.readUInt16BE(offset + 6);
      const segCount = segCountX2 / 2;
      const endBase = offset + 14;
      const startBase = endBase + segCountX2 + 2;
      const deltaBase = startBase + segCountX2;
      const rangeBase = deltaBase + segCountX2;

      for (let segment = 0; segment < segCount; segment++) {
        const end = cmap.readUInt16BE(endBase + segment * 2);
        const start = cmap.readUInt16BE(startBase + segment * 2);
        const delta = cmap.readInt16BE(deltaBase + segment * 2);
        const rangeOffset = cmap.readUInt16BE(rangeBase + segment * 2);
        if (start === 0xffff) {
          continue;
        }

        for (let code = start; code <= end && code !== 0x10000; code++) {
          let glyph;
          if (rangeOffset === 0) {
            glyph = (code + delta) & 0xffff;
          } else {
            const glyphIndexAddress = rangeBase + segment * 2 + rangeOffset + (code - start) * 2;
            if (glyphIndexAddress + 1 >= cmap.length) {
              continue;
            }
            glyph = cmap.readUInt16BE(glyphIndexAddress);
            if (glyph !== 0) {
              glyph = (glyph + delta) & 0xffff;
            }
          }
          if (glyph !== 0) {
            covered.add(code);
          }
        }
      }
      continue;
    }

    if (format === 12) {
      const numGroups = cmap.readUInt32BE(offset + 12);
      for (let group = 0; group < numGroups; group++) {
        const record = offset + 16 + group * 12;
        const start = cmap.readUInt32BE(record);
        const end = cmap.readUInt32BE(record + 4);
        for (let code = start; code <= end; code++) {
          covered.add(code);
        }
      }
      continue;
    }

    if (format === 6) {
      const first = cmap.readUInt16BE(offset + 6);
      const count = cmap.readUInt16BE(offset + 8);
      for (let index = 0; index < count; index++) {
        if (cmap.readUInt16BE(offset + 10 + index * 2) !== 0) {
          covered.add(first + index);
        }
      }
    }
  }

  return covered;
}

/**
 * Bir WOFF2 dosyasinin kapsadigi kod noktalari + tipografik metrikleri.
 * Metrikler em birimine normalize edilir (yedek yuzey ayarlamasi icin).
 */
export function inspectWoff2(filePath) {
  const tables = readWoff2Tables(filePath);
  const head = tables['head'];
  const hhea = tables['hhea'];
  const os2 = tables['OS/2'];

  const unitsPerEm = head.readUInt16BE(18);

  return {
    file: filePath,
    unitsPerEm,
    codePoints: codePointsFromCmap(tables['cmap']),
    metrics: {
      hheaAscender: hhea.readInt16BE(4) / unitsPerEm,
      hheaDescender: hhea.readInt16BE(6) / unitsPerEm,
      hheaLineGap: hhea.readInt16BE(8) / unitsPerEm,
      typoAscender: os2.readInt16BE(68) / unitsPerEm,
      typoDescender: os2.readInt16BE(70) / unitsPerEm,
      typoLineGap: os2.readInt16BE(72) / unitsPerEm,
      winAscent: os2.readUInt16BE(74) / unitsPerEm,
      winDescent: os2.readUInt16BE(76) / unitsPerEm,
      xAvgCharWidth: os2.readInt16BE(2) / unitsPerEm,
    },
  };
}

/** Dosya icerigi ozeti — vendor edilen dosyanin degismedigini kanitlar. */
export function sha256(filePath) {
  return createHash('sha256').update(readFileSync(filePath)).digest('hex');
}

/** `U+0100-02BA,U+0131` bicimindeki bildirimden kod noktasi araligi kumesi. */
export function parseUnicodeRange(declaration) {
  const ranges = [];
  for (const part of declaration.split(',')) {
    const token = part.trim().replace(/^U\+/i, '');
    if (token.includes('-')) {
      const [start, end] = token.split('-');
      ranges.push([parseInt(start, 16), parseInt(end, 16)]);
    } else if (token.includes('?')) {
      ranges.push([
        parseInt(token.replace(/\?/g, '0'), 16),
        parseInt(token.replace(/\?/g, 'F'), 16),
      ]);
    } else {
      const value = parseInt(token, 16);
      ranges.push([value, value]);
    }
  }
  return ranges;
}

export function rangesContain(ranges, codePoint) {
  return ranges.some(([start, end]) => codePoint >= start && codePoint <= end);
}
