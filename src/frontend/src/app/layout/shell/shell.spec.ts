import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, type ActivatedRouteSnapshot, type Data } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { provideTranslateService } from '@ngx-translate/core';
import { beforeEach, describe, expect, it } from 'vitest';

import { DASHBOARD_ROUTES } from '../../features/dashboard/dashboard.routes';
import { HIDE_SIDEBAR, shouldHideSidebar } from '../chrome';
import { Shell } from './shell';

/** Rota agacinda `data` bayragi arayan yardimciyi test etmek icin sahte anlik goruntu. */
function snapshot(data: Data, child: ActivatedRouteSnapshot | null = null): ActivatedRouteSnapshot {
  return { data, firstChild: child } as unknown as ActivatedRouteSnapshot;
}

@Component({
  selector: 'hc-test-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<p>page</p>`,
})
class TestPage {}

/**
 * Gercek ekranlardaki (`reports`, `shifts`, `occupancy-plan`, ...) `sr-only`
 * kullanimini temsil eden sayfa: tablo `<caption>`'i ve hucre ici aciklama.
 */
@Component({
  selector: 'hc-test-a11y-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <table>
      <caption class="sr-only">
        table caption
      </caption>
      <tbody>
        <tr>
          <td><span class="sr-only">cell description</span>42</td>
        </tr>
      </tbody>
    </table>
  `,
})
class TestA11yPage {}

/** Konumlandirma baglami (containing block) kuran Tailwind `position` siniflari. */
const POSITIONING_CLASSES = ['relative', 'absolute', 'fixed', 'sticky'] as const;

function establishesPositioningContext(element: Element): boolean {
  return POSITIONING_CLASSES.some((className) => element.classList.contains(className));
}

describe('shouldHideSidebar', () => {
  it('bayrak en derin cocuk rotada olsa bile bulur', () => {
    const tree = snapshot({}, snapshot({}, snapshot({ [HIDE_SIDEBAR]: true })));
    expect(shouldHideSidebar(tree)).toBe(true);
  });

  it('bayrak yoksa kenar cubugu gorunur kalir', () => {
    expect(shouldHideSidebar(snapshot({ titleKey: 'rooms.title' }, snapshot({})))).toBe(false);
    expect(shouldHideSidebar(null)).toBe(false);
  });
});

describe('hub rotasi', () => {
  it('kenar cubugunu gizleme bayragini tasir', () => {
    expect(DASHBOARD_ROUTES[0]?.data?.[HIDE_SIDEBAR]).toBe(true);
  });
});

describe('Shell — kabuk duzeni', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideTranslateService({ lang: 'de', fallbackLang: 'de' }),
        provideRouter([
          {
            path: '',
            component: Shell,
            children: [
              // Hub: gercek `DASHBOARD_ROUTES` ile ayni bayrak.
              { path: 'dashboard', component: TestPage, data: { [HIDE_SIDEBAR]: true } },
              { path: 'rooms', component: TestPage },
            ],
          },
        ]),
      ],
    });
  });

  it('hub ekraninda kenar cubugunu ve mobil menu dugmesini render etmez', async () => {
    const harness = await RouterTestingHarness.create('/dashboard');
    const element = harness.fixture.nativeElement as HTMLElement;

    expect(element.querySelector('hc-sidebar')).toBeNull();
    expect(element.querySelector('[aria-controls="hc-mobile-drawer"]')).toBeNull();
    // Topbar hub'da da kalir: otel ve dil secimi erisilebilir olmalidir.
    expect(element.querySelector('hc-topbar')).not.toBeNull();
    expect(element.querySelector('hc-hotel-switcher')).not.toBeNull();
  });

  it('bir module girildiginde kenar cubugu ve menu dugmesi geri gelir', async () => {
    const harness = await RouterTestingHarness.create('/dashboard');
    await harness.navigateByUrl('/rooms');
    harness.detectChanges();
    const element = harness.fixture.nativeElement as HTMLElement;

    expect(element.querySelector('hc-sidebar')).not.toBeNull();
    expect(element.querySelector('[aria-controls="hc-mobile-drawer"]')).not.toBeNull();
  });

  it('modulden hub a donuldugunde kenar cubugu yeniden gizlenir', async () => {
    const harness = await RouterTestingHarness.create('/rooms');
    expect(
      (harness.fixture.nativeElement as HTMLElement).querySelector('hc-sidebar'),
    ).not.toBeNull();

    await harness.navigateByUrl('/dashboard');
    harness.detectChanges();

    expect((harness.fixture.nativeElement as HTMLElement).querySelector('hc-sidebar')).toBeNull();
  });
});

/**
 * Regresyon: belge (window) kaydirmasi olusmamalidir.
 *
 * Hata tablosu: kabuk cercevesi `h-dvh overflow-hidden`, ana icerik ise ayri bir
 * `overflow-y-auto` kabinda kayar. Tailwind'in `sr-only` yardimcisi
 * `position: absolute` kullandigi icin, kaydirma kabinda konumlandirma baglami
 * yoksa bu elemanlarin kapsayici blogu **initial containing block** olur:
 * kirpilmazlar, **belge** yuksekligini buyuturler ve pencere kaydirilabilir hale
 * gelir — bu da `position` ile sabitlenen ust cubugu ekrandan cikarir
 * (olculdu: /reports 1440x900 -> documentElement.scrollHeight 2756 / clientHeight 900,
 * window.scrollTo(0, 5000) sonrasi scrollY 1856, hc-topbar top -1856).
 *
 * SINIR: birim testler jsdom uzerinde calisir; jsdom yerlesim (layout) hesaplamaz
 * ve Tailwind CSS'i yuklemez, bu yuzden `scrollHeight` burada olculemez. Bu
 * nedenle test, hatayi mumkun kilan **yapisal** kosulu dogrular: kirpma cercevesi
 * ile `sr-only` icerik arasinda mutlaka bir konumlandirma baglami bulunmalidir.
 * Duzeltmeden sonra ayni olcum: scrollHeight == clientHeight, scrollY 0, topbar top 0.
 */
describe('Shell — kaydirma kabi konumlandirma baglami', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideTranslateService({ lang: 'de', fallbackLang: 'de' }),
        provideRouter([
          {
            path: '',
            component: Shell,
            children: [{ path: 'reports', component: TestA11yPage }],
          },
        ]),
      ],
    });
  });

  it('kabuk cercevesi tasmayi kirpar ve ic kap dikeyde kayar', async () => {
    const harness = await RouterTestingHarness.create('/reports');
    const element = harness.fixture.nativeElement as HTMLElement;

    const frame = element.querySelector('.overflow-hidden');
    expect(frame).not.toBeNull();
    expect(frame?.classList.contains('h-dvh')).toBe(true);

    const scroller = element.querySelector('#hc-main')?.parentElement;
    expect(scroller).toBeTruthy();
    expect(scroller?.classList.contains('overflow-y-auto')).toBe(true);
  });

  it('kaydirma kabi konumlandirma baglami kurar (`relative`)', async () => {
    const harness = await RouterTestingHarness.create('/reports');
    const element = harness.fixture.nativeElement as HTMLElement;
    const scroller = element.querySelector('#hc-main')?.parentElement;

    expect(scroller && establishesPositioningContext(scroller)).toBe(true);
  });

  it('her `sr-only` eleman ile kirpma cercevesi arasinda konumlandirma baglami vardir', async () => {
    const harness = await RouterTestingHarness.create('/reports');
    const element = harness.fixture.nativeElement as HTMLElement;
    const frame = element.querySelector('.overflow-hidden');
    const srOnly = Array.from(element.querySelectorAll('.sr-only'));

    expect(frame).not.toBeNull();
    // Sayfa gercekten `sr-only` iceriyor olmali; aksi halde test bos gecerdi.
    expect(srOnly.length).toBeGreaterThan(0);

    for (const node of srOnly) {
      let ancestor: HTMLElement | null = node.parentElement;
      let anchored = false;
      while (ancestor !== null && ancestor !== frame) {
        if (establishesPositioningContext(ancestor)) {
          anchored = true;
          break;
        }
        ancestor = ancestor.parentElement;
      }
      expect(anchored, `sr-only eleman kirpma cercevesine bagli degil: ${node.outerHTML}`).toBe(
        true,
      );
    }
  });
});
