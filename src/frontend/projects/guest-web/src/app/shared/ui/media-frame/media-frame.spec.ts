import { TestBed } from '@angular/core/testing';
import { beforeEach, describe, expect, it } from 'vitest';

import { MediaFrame } from './media-frame';

function render(inputs: Record<string, unknown>) {
  const fixture = TestBed.createComponent(MediaFrame);
  for (const [name, value] of Object.entries(inputs)) {
    fixture.componentRef.setInput(name, value);
  }
  fixture.detectChanges();
  return fixture.nativeElement as HTMLElement;
}

beforeEach(() => TestBed.configureTestingModule({}));

describe('MediaFrame — duzen kaymasi (CLS) disiplini', () => {
  it('gorsel yokken bile kutuyu olcuye gore ayirir', () => {
    const element = render({ width: 1600, height: 900, alt: 'Ansicht' });
    const figure = element.querySelector<HTMLElement>('figure');

    // Oran onceden ayrildigi icin fotograf gelince sayfa ziplamaz.
    expect(figure?.style.aspectRatio).toBe('1600 / 900');
  });

  it('gorsel geldiginde AYNI kutuyu kullanir ve width/height nitelikleri tasir', () => {
    const element = render({
      width: 1200,
      height: 800,
      alt: 'Zimmer',
      src: '/media/room.jpg',
    });

    const image = element.querySelector<HTMLImageElement>('[data-testid="media-image"]');
    expect(element.querySelector('figure')?.style.aspectRatio).toBe('1200 / 800');
    expect(image?.getAttribute('width')).toBe('1200');
    expect(image?.getAttribute('height')).toBe('800');
  });
});

describe('MediaFrame — yukleme onceligi', () => {
  it('varsayilan olarak tembel yuklenir', () => {
    const element = render({ width: 800, height: 800, alt: 'x', src: '/a.jpg' });
    const image = element.querySelector<HTMLImageElement>('img');

    expect(image?.getAttribute('loading')).toBe('lazy');
    expect(image?.getAttribute('fetchpriority')).toBe('auto');
  });

  it('LCP adayi isaretlendiginde erken ve yuksek oncelikli yuklenir', () => {
    const element = render({
      width: 1600,
      height: 900,
      alt: 'x',
      src: '/a.jpg',
      priority: true,
    });
    const image = element.querySelector<HTMLImageElement>('img');

    expect(image?.getAttribute('loading')).toBe('eager');
    expect(image?.getAttribute('fetchpriority')).toBe('high');
  });
});

describe('MediaFrame — erisilebilirlik', () => {
  it('yer tutucu da erisilebilir ad tasir', () => {
    const element = render({ width: 4, height: 3, alt: 'Ansicht des Hauses' });
    const placeholder = element.querySelector('[data-testid="media-placeholder"]');

    expect(placeholder?.getAttribute('role')).toBe('img');
    expect(placeholder?.getAttribute('aria-label')).toBe('Ansicht des Hauses');
  });

  it('cizim ogesi ekran okuyucudan gizlenir (dekoratif)', () => {
    const element = render({ width: 4, height: 3, alt: 'Ansicht' });
    expect(element.querySelector('svg')?.getAttribute('aria-hidden')).toBe('true');
  });
});
