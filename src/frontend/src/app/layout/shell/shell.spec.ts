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
    expect((harness.fixture.nativeElement as HTMLElement).querySelector('hc-sidebar')).not.toBeNull();

    await harness.navigateByUrl('/dashboard');
    harness.detectChanges();

    expect((harness.fixture.nativeElement as HTMLElement).querySelector('hc-sidebar')).toBeNull();
  });
});
