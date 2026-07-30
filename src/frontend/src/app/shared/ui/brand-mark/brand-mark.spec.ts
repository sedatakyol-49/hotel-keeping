import { TestBed } from '@angular/core/testing';
import { describe, expect, it } from 'vitest';

import { BrandMark } from './brand-mark';

/** Isareti verilen girdilerle canlandirip kok `<svg>` ogesini dondurur. */
function render(inputs: Partial<{ size: number; label: string }> = {}) {
  const fixture = TestBed.createComponent(BrandMark);
  for (const [name, value] of Object.entries(inputs)) {
    fixture.componentRef.setInput(name, value);
  }
  fixture.detectChanges();

  const element = fixture.nativeElement as HTMLElement;
  const svg = element.querySelector('svg');
  expect(svg).not.toBeNull();

  return { fixture, element, svg: svg as SVGSVGElement };
}

describe('BrandMark — erisilebilirlik', () => {
  it('etiket verildiginde erisilebilir ad tasir', () => {
    const { svg } = render({ label: 'HotelCore' });

    expect(svg.getAttribute('role')).toBe('img');
    expect(svg.getAttribute('aria-label')).toBe('HotelCore');
    // Adi tasiyan isaret ekran okuyucudan gizlenmemeli.
    expect(svg.getAttribute('aria-hidden')).toBeNull();
  });

  it('etiket verilmediginde susleme olarak gizlenir', () => {
    // Yanindaki gorunur marka adi (common.appName) zaten okundugu icin
    // varsayilan davranis cift duyuruyu onler.
    const { svg } = render();

    expect(svg.getAttribute('aria-hidden')).toBe('true');
    expect(svg.getAttribute('role')).toBeNull();
    expect(svg.getAttribute('aria-label')).toBeNull();
  });

  it('odak sirasina girmez', () => {
    const { svg } = render({ label: 'HotelCore' });

    expect(svg.getAttribute('focusable')).toBe('false');
  });
});

describe('BrandMark — cizim', () => {
  it('istenen olcuyu uygular ve olcekten bagimsiz viewBox korur', () => {
    const small = render({ size: 16 });
    expect(small.svg.getAttribute('width')).toBe('16');
    expect(small.svg.getAttribute('height')).toBe('16');
    expect(small.svg.getAttribute('viewBox')).toBe('0 0 32 32');

    const large = render({ size: 96 });
    expect(large.svg.getAttribute('width')).toBe('96');
    expect(large.svg.getAttribute('viewBox')).toBe('0 0 32 32');
  });

  it('cerceveyi her olcekte 1px kalan hairline olarak cizer', () => {
    const { svg } = render();
    const frame = svg.querySelector('rect[stroke]');

    expect(frame?.getAttribute('stroke-width')).toBe('1');
    expect(frame?.getAttribute('vector-effect')).toBe('non-scaling-stroke');
  });

  it('H ve S ayni govde kalinliginda, tek aksan bakir taban cizgisidir', () => {
    const { svg } = render();

    // S tek cizgi (monoline) olarak cizilir; H dolgu olarak — ikisi de 3 birim.
    expect(svg.querySelector('path[stroke-width="3"]')).not.toBeNull();

    const accents = [...svg.querySelectorAll('[fill]')].filter((node) =>
      node.getAttribute('fill')?.includes('copper'),
    );
    expect(accents).toHaveLength(1);
  });

  it('yasak gorsel ozellikleri (gradyan, golge, yuvarlak kose) icermez', () => {
    const { svg } = render();
    const markup = svg.outerHTML;

    expect(markup).not.toMatch(/Gradient|filter=|rx=|ry=|stroke-linejoin="round"/);
  });
});
