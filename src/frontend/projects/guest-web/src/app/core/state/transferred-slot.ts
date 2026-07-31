import { PLATFORM_ID, TransferState, inject, makeStateKey } from '@angular/core';
import { isPlatformServer } from '@angular/common';

import { asyncSlot, type AsyncSlot } from './async-state';

/**
 * ===========================================================================
 * SUNUCUDAN ISTEMCIYE DEVREDILEN ASYNC SLOT
 * ===========================================================================
 *
 * NEDEN VAR (olculmus hata, tahmin degil).
 * Sunucu sayfayi dolu HTML olarak basar; tarayici hidre olur ama istemcideki
 * signal'ler BOSTUR. Sayfa yapicisi ayni ucu ikinci kez cagirir, yanit gelene
 * kadar `@if (data(); as x)` bloklari kapalidir ve icerik **kaybolur**; yanit
 * gelince geri gelir. Gozle gorulen sonuc: sayfa once cokup sonra acilir ve
 * altindaki her sey — ozellikle alt bilgi — ziplar.
 *
 * Uretim derlemesinde gercek Chrome ile olculdu:
 *   /de/legal/terms  (prerender, 1440) : CLS 0.60
 *   /de/rooms/DBL    (SSR, 1440)       : CLS 1.29  (icerik 835px -> 113px -> 820px)
 * Ikisi de "iyi" esiginin (0.10) kat kat ustunde ve dogrudan Core Web Vitals'a
 * girer — yani SEO icin SSR kurmus olmanin bir kismini geri verir.
 *
 * NEDEN ANGULAR'IN HTTP AKTARIM ONBELLEGI YETMIYOR.
 * `provideClientHydration()` bir aktarim onbellegi getirir ama bu uygulamada
 * hicbir zaman TUTMAZ. Sebep araci zincir sirasidir: `HttpInterceptorHandler`
 * zinciri `[...HTTP_INTERCEPTOR_FNS, ...HTTP_ROOT_INTERCEPTOR_FNS]` olarak
 * kurar, yani onbellek aracisi bizim aracilarimizdan SONRA gelir.
 *   - `apiUrlInterceptor` sunucuda adresi mutlaklastirir; onbellek anahtari
 *     adresten turedigi icin sunucu `http://host/api/...`, tarayici
 *     `/api/...` anahtarini kullanir ve eslesme olmaz.
 *     (`HTTP_TRANSFER_CACHE_ORIGIN_MAP` care degil: origin'i baska bir
 *     ORIGIN'e esler, goreli adrese esleyemez.)
 *   - `legalPrerenderInterceptor` prerender'da istegi kisa devre eder; yanit
 *     onbellek aracisina hic ulasmaz (prerender ciktisinda `ng-state` icinde
 *     tek bir HTTP girdisi yoktu — dogrulandi).
 *   - Ayrica uc `Cache-Control: no-store` dondururse aktarim onbellegi o
 *     yaniti hicbir kosulda saklamaz.
 * Bu yuzden devir ACIKCA yapilir. Anahtar adresten degil, bizim verdigimiz
 * adan turer; araci zinciri degisse de calisir.
 *
 * KAPSAM (`scope`) — ayni slot farkli kayitlar icin kullanildiginda gerekir:
 * oda tipi detayi her slug icin baska bir yanittir. Kapsam anahtarin parcasi
 * olur, boylece "DBL sayfasinda uretilen veri SUI sayfasinda devralinamaz".
 *
 * DEVIR TEK SEFERLIKTIR: okunan anahtar silinir. Silinmeseydi kullanicinin
 * bastigi "yeniden dene" ayni bayat veriyi tekrar devralir ve hicbir sey
 * yapmamis gorunurdu.
 */
export interface TransferredSlot<T> extends AsyncSlot<T> {
  /**
   * Sunucunun ilistirdigi degeri devralir. Devralindiysa `true` doner ve
   * cagiran ISTEK ACMAZ. Sunucuda her zaman `false`.
   */
  adopt(scope?: string): boolean;

  /** Sunucuda: yaniti belgeye ilistirir. Tarayicida hicbir sey yapmaz. */
  handOver(value: T, scope?: string): void;
}

/**
 * @param name Belgeye yazilacak anahtarin tabani (ornegin `hc.roomType`).
 *   Uygulama genelinde TEKIL olmali; iki slot ayni adi kullanirsa biri
 *   digerinin verisini devralir.
 */
export function transferredSlot<T>(name: string): TransferredSlot<T> {
  const slot = asyncSlot<T>();
  const transferState = inject(TransferState);
  const onServer = isPlatformServer(inject(PLATFORM_ID));

  const keyFor = (scope?: string) =>
    makeStateKey<T>(scope === undefined ? name : `${name}:${scope}`);

  return {
    ...slot,

    adopt(scope?: string): boolean {
      if (onServer) {
        return false;
      }
      const key = keyFor(scope);
      if (!transferState.hasKey(key)) {
        return false;
      }
      const value = transferState.get(key, null as T | null);
      transferState.remove(key);
      if (value === null) {
        return false;
      }
      slot.succeed(value);
      return true;
    },

    handOver(value: T, scope?: string): void {
      if (onServer) {
        transferState.set(keyFor(scope), value);
      }
    },
  };
}
