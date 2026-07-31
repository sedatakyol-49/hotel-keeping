import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { API, hold, problem } from '../../../testing/public-fixtures';
import { HoldStore } from './hold.store';

const TOKEN = 'Vb3nQ8sT1kR6yPz0LmXhAw';

function http(): HttpTestingController {
  return TestBed.inject(HttpTestingController);
}

beforeEach(() => {
  // Hold 09:15'te doluyor; "simdi" 09:05 -> tam 600 saniye kaldi.
  vi.useFakeTimers();
  vi.setSystemTime(new Date('2026-07-31T09:05:00+02:00'));
  TestBed.configureTestingModule({
    providers: [provideHttpClient(), provideHttpClientTesting()],
  });
  globalThis.sessionStorage?.clear();
});

afterEach(() => {
  vi.useRealTimers();
});

describe('Hold — geri sayim', () => {
  it('kalan sureyi `expiresAt`ten hesaplar (sayaci azaltmaz)', () => {
    const store = TestBed.inject(HoldStore);
    store.open(TOKEN);
    http().expectOne(API.hold(TOKEN)).flush(hold());

    expect(store.remainingSeconds()).toBe(600);

    // Sekme 5 dakika uyusa bile sure `expiresAt`ten yeniden turer.
    vi.advanceTimersByTime(300_000);
    expect(store.remainingSeconds()).toBe(300);
  });

  it('sure dolunca expired olur ve sayac sifirda kalir', () => {
    const store = TestBed.inject(HoldStore);
    store.open(TOKEN);
    http().expectOne(API.hold(TOKEN)).flush(hold());

    vi.advanceTimersByTime(601_000);

    expect(store.remainingSeconds()).toBe(0);
    expect(store.expired()).toBe(true);
  });

  it('sunucu 409 `HOLD_EXPIRED` dondururse de sure dolmus sayilir', () => {
    const store = TestBed.inject(HoldStore);
    store.open(TOKEN);
    http()
      .expectOne(API.hold(TOKEN))
      .flush(problem('HOLD_EXPIRED'), { status: 409, statusText: 'Conflict' });

    expect(store.expired()).toBe(true);
    expect(store.error()?.recovery).toBe('renewHold');
  });
});

describe('Hold — kurtarma (yeni teklif)', () => {
  it('ayni parametrelerle yeni bir hold ister', () => {
    const store = TestBed.inject(HoldStore);
    store.open(TOKEN);
    http().expectOne(API.hold(TOKEN)).flush(hold());

    vi.advanceTimersByTime(601_000);
    store.renew();

    const request = http().expectOne(API.holds);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      roomTypeCode: 'DBL',
      checkIn: '2026-08-10',
      checkOut: '2026-08-13',
      adults: 2,
      children: 0,
    });
  });

  it('yeni teklifin fiyati farkliysa ONCEKI tutar saklanir (ekran farki gosterebilsin)', () => {
    const store = TestBed.inject(HoldStore);
    store.open(TOKEN);
    http().expectOne(API.hold(TOKEN)).flush(hold());

    store.renew();
    const renewed = hold({
      expiresAt: '2026-07-31T09:30:00+02:00',
      price: { ...hold().price, totalGross: 492 },
    });
    http().expectOne(API.holds).flush(renewed, { status: 201, statusText: 'Created' });

    expect(store.previousTotal()).toBe(468);
    expect(store.hold()?.price.totalGross).toBe(492);
  });
});

describe('Hold — zorunlu depolama (§25 Abs. 2 Nr. 2)', () => {
  it('token oturum deposuna yazilir; sayfa yenilenince kurtarilabilir', () => {
    const store = TestBed.inject(HoldStore);
    store.open(TOKEN);
    http().expectOne(API.hold(TOKEN)).flush(hold());

    expect(store.storedToken()).toBe(TOKEN);
  });

  it('rezervasyon tamamlaninca iz birakilmaz', () => {
    const store = TestBed.inject(HoldStore);
    store.open(TOKEN);
    http().expectOne(API.hold(TOKEN)).flush(hold());

    store.clear();

    expect(store.storedToken()).toBeNull();
    expect(store.hold()).toBeNull();
  });

  it('akistan cikista envanter HEMEN serbest birakilir', () => {
    const store = TestBed.inject(HoldStore);
    store.open(TOKEN);
    http().expectOne(API.hold(TOKEN)).flush(hold());

    store.release();

    const request = http().expectOne(API.hold(TOKEN));
    expect(request.request.method).toBe('DELETE');
    request.flush(null, { status: 204, statusText: 'No Content' });
    expect(store.storedToken()).toBeNull();
  });
});

describe('Hold — cift tiklama korumasi', () => {
  it('istek surerken ikinci bir hold acmaz', () => {
    const store = TestBed.inject(HoldStore);
    const request = {
      roomTypeCode: 'DBL',
      checkIn: '2026-08-10',
      checkOut: '2026-08-13',
      adults: 2,
      children: 0,
    };

    store.create(request);
    store.create(request);

    http().expectOne(API.holds);
  });
});
