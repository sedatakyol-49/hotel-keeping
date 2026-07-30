import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { provideTranslateService } from '@ngx-translate/core';
import { beforeEach, describe, expect, it } from 'vitest';

import type { AuthenticatedUser } from '../../core/models/auth.model';
import { PERMISSIONS, type PermissionKey } from '../../core/models/permission.model';
import { AuthStore } from '../../core/state/auth.store';
import { SidebarState } from '../sidebar-state';
import { Sidebar } from './sidebar';

@Component({
  selector: 'hc-test-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<p>page</p>`,
})
class TestPage {}

function user(permissions: readonly PermissionKey[]): AuthenticatedUser {
  return {
    id: 'u-1',
    email: 'anna.becker@hotel.de',
    roles: ['Receptionist'],
    permissions,
    hotels: [{ id: 'h-1', name: 'Hotel Adler', currency: 'EUR' }],
    canAccessAllHotels: false,
    defaultHotelId: 'h-1',
  };
}

/** Kenar cubugunu verilen rotada, verilen izinlerle canlandirir. */
async function render(url: string, permissions: readonly PermissionKey[]) {
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(),
      provideHttpClientTesting(),
      provideTranslateService({ lang: 'de', fallbackLang: 'de' }),
      provideRouter([
        { path: 'dashboard', component: TestPage },
        { path: 'rooms', component: TestPage },
        { path: 'rooms/types', component: TestPage },
        { path: 'rooms/new', component: TestPage },
        { path: 'housekeeping', component: TestPage },
        { path: 'invoices', component: TestPage },
        { path: 'reports', component: TestPage },
      ]),
    ],
  });

  TestBed.inject(AuthStore).setSession(user(permissions));

  const harness = await RouterTestingHarness.create(url);
  const fixture = TestBed.createComponent(Sidebar);
  fixture.detectChanges();

  return {
    harness,
    fixture,
    element: fixture.nativeElement as HTMLElement,
    state: TestBed.inject(SidebarState),
  };
}

function groupToggle(element: HTMLElement, group: string): HTMLButtonElement | null {
  return element.querySelector<HTMLButtonElement>(
    `[data-testid="nav-group-toggle"][data-group="${group}"]`,
  );
}

function submenu(element: HTMLElement, group: string): HTMLElement | null {
  return element.querySelector<HTMLElement>(`[data-testid="nav-submenu"][data-group="${group}"]`);
}

function activePaths(element: HTMLElement): readonly string[] {
  return [
    ...element.querySelectorAll<HTMLElement>('[data-testid="nav-link"][aria-current="page"]'),
  ].map((link) => link.dataset['path'] ?? '');
}

/** Finans bolumunun **iki** ogesi de gorunur olmali; aksi halde tek ogeli bolum
 *  kurali devreye girer ve accordion yerine dogrudan baglanti cizilir. */
const ALL: readonly PermissionKey[] = [
  PERMISSIONS.RoomsView,
  PERMISSIONS.RoomsManage,
  PERMISSIONS.HousekeepingView,
  PERMISSIONS.ReservationsView,
  PERMISSIONS.InvoicesView,
  PERMISSIONS.ReportsView,
];

beforeEach(() => {
  // Kalici tercihler testler arasinda sizmasin.
  globalThis.localStorage?.clear();
});

describe('Sidebar — ana menu ve alt menuler', () => {
  it('coklu bolumu acilip kapanan ana kalem olarak cizer', async () => {
    const { element } = await render('/dashboard', ALL);

    const toggle = groupToggle(element, 'nav.section.operations');
    expect(toggle).not.toBeNull();
    expect(toggle?.getAttribute('aria-controls')).toBe(
      submenu(element, 'nav.section.operations')?.id,
    );
  });

  it('tek ogeli bolumu accordion yerine dogrudan baglanti olarak cizer', async () => {
    const { element } = await render('/dashboard', ALL);

    expect(groupToggle(element, 'nav.section.overview')).toBeNull();
    expect(
      element.querySelector('[data-testid="nav-link"][data-path="/dashboard"]'),
    ).not.toBeNull();
  });

  it('alt menuyu tiklamayla acar ve kapatir', async () => {
    const { element, fixture } = await render('/dashboard', ALL);
    const group = 'nav.section.finance';

    expect(submenu(element, group)?.hidden).toBe(true);

    groupToggle(element, group)?.click();
    fixture.detectChanges();
    expect(submenu(element, group)?.hidden).toBe(false);
    expect(groupToggle(element, group)?.getAttribute('aria-expanded')).toBe('true');

    groupToggle(element, group)?.click();
    fixture.detectChanges();
    expect(submenu(element, group)?.hidden).toBe(true);
  });

  it('aktif rotayi iceren ana kalemi kendiliginden acar', async () => {
    const { element } = await render('/housekeeping', ALL);

    expect(submenu(element, 'nav.section.operations')?.hidden).toBe(false);
    // Kullanicinin dokunmadigi diger bolumler kapali kalir.
    expect(submenu(element, 'nav.section.finance')?.hidden).toBe(true);
  });

  it('aktif kalemi en uzun yol eslesmesine gore isaretler', async () => {
    // /rooms ve /rooms/types kardes kalemler: prefix eslesmesi ikisini birden
    // aktif gosterirdi.
    const types = await render('/rooms/types', ALL);
    expect(activePaths(types.element)).toEqual(['/rooms/types']);

    TestBed.resetTestingModule();

    // /rooms/new bir alt rota: tam eslesme hicbirini aktif yapmazdi.
    const nested = await render('/rooms/new', ALL);
    expect(activePaths(nested.element)).toEqual(['/rooms']);
  });

  it('tum alt kalemleri izinle suzulen ana kalemi hic cizmez', async () => {
    const { element } = await render('/dashboard', [PERMISSIONS.RoomsView]);

    // Finans bolumunun tek gorunur ogesi yok -> ana kalem de yok.
    expect(groupToggle(element, 'nav.section.finance')).toBeNull();
    expect(element.querySelector('[data-testid="nav-link"][data-path="/invoices"]')).toBeNull();

    // Operasyon bolumu yalnizca izinli ogeyi tasir.
    expect(element.querySelector('[data-testid="nav-link"][data-path="/rooms"]')).not.toBeNull();
    expect(element.querySelector('[data-testid="nav-link"][data-path="/rooms/types"]')).toBeNull();
  });
});

describe('SidebarState — kalici tercihler', () => {
  it('daraltma durumunu localStorage a yazar ve geri okur', async () => {
    const { state } = await render('/rooms', ALL);

    expect(state.collapsed()).toBe(false);
    state.toggleCollapsed();
    expect(state.collapsed()).toBe(true);
    expect(globalThis.localStorage?.getItem('hotelcore.sidebar.collapsed')).toBe('1');

    // Yeni bir ornek (sayfa yenilemesi) tercihi korur.
    TestBed.resetTestingModule();
    const reloaded = await render('/rooms', ALL);
    expect(reloaded.state.collapsed()).toBe(true);
  });

  it('acik alt menuleri localStorage a yazar', async () => {
    const { state } = await render('/dashboard', ALL);

    state.toggleGroup('nav.section.finance');

    const raw = globalThis.localStorage?.getItem('hotelcore.sidebar.expandedGroups') ?? '[]';
    expect(JSON.parse(raw)).toContain('nav.section.finance');
  });

  it('bozuk kalici degeri yok sayar ve varsayilanla calisir', async () => {
    globalThis.localStorage?.setItem('hotelcore.sidebar.expandedGroups', 'not-json');

    const { state } = await render('/dashboard', ALL);

    expect(state.isExpanded('nav.section.finance')).toBe(false);
  });
});

describe('Sidebar — daraltilmis (rail) mod', () => {
  it('rail modunda kisa gosterimi kullanir, tam etiketi ekran okuyucuya birakir', async () => {
    const { element, fixture, state } = await render('/dashboard', ALL);

    state.toggleCollapsed();
    fixture.detectChanges();

    // Test ortaminda ceviri dosyasi yuklenmedigi icin `translate` anahtari
    // dondurur; burada dogrulanan sey **hangi anahtarin nereye baglandigi**:
    // gorsel kisa gosterim `nav.short.*`, ekran okuyucu ve title tam etiket.
    const toggle = groupToggle(element, 'nav.section.operations');
    expect(toggle?.querySelector('[aria-hidden="true"]')?.textContent?.trim()).toBe(
      'nav.short.operations',
    );
    expect(toggle?.querySelector('.sr-only')?.textContent?.trim()).toBe('nav.section.operations');
    expect(toggle?.getAttribute('title')).toBe('nav.section.operations');
  });

  it('rail modunda marka isaretini cizer, tam adi `sr-only` birakir', async () => {
    const { element, fixture, state } = await render('/dashboard', ALL);
    const brandBlock = element.querySelector('nav > div');

    // Genis moddayken de isaret vardir; ad ise gorunur metindir.
    expect(brandBlock?.querySelector('[data-testid="brand-mark"]')).not.toBeNull();
    expect(brandBlock?.querySelector('.sr-only')).toBeNull();

    state.toggleCollapsed();
    fixture.detectChanges();

    const rail = element.querySelector('nav > div');
    const mark = rail?.querySelector('[data-testid="brand-mark"]');

    expect(mark).not.toBeNull();
    // Isaret susleme; ad ekran okuyucuya `sr-only` metinle verilir.
    expect(mark?.getAttribute('aria-hidden')).toBe('true');
    expect(rail?.querySelector('.sr-only')?.textContent?.trim()).toBe('common.appName');
    // Eski tek harfli "H" gosterimi kalmamali.
    expect(rail?.textContent?.replace(/\s/g, '')).toBe('common.appName');
  });

  it('mobil cekmecede daraltma devre disi kalir', async () => {
    const { fixture, state } = await render('/dashboard', ALL);

    state.toggleCollapsed();
    fixture.componentRef.setInput('allowCollapse', false);
    fixture.detectChanges();

    const nav = (fixture.nativeElement as HTMLElement).querySelector('nav');
    expect(nav?.classList.contains('hc-rail')).toBe(false);
  });
});
