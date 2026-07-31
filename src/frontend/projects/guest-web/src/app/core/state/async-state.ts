import { computed, signal, type Signal } from '@angular/core';

import type { PublicApiError } from '../api/public-error';

/**
 * Yukleme/hata/veri uclusu icin **tek** desen.
 *
 * Her ekranin kendi `loading` bayragini uydurmasi, er ya da gec "yukleniyor
 * kaldi" veya "hata gorunurken veri de gorunuyor" durumlarini uretir. Burada
 * durum bir **birlesim (union)** olarak modellenir: ayni anda hem hata hem veri
 * gosterilemez, cunku `status` tek bir degerdir.
 *
 * `retry` disarida tutulur: her cagri kendi istegini bilir, store karar verir.
 */
export type AsyncStatus = 'idle' | 'loading' | 'ready' | 'error';

export interface AsyncState<T> {
  readonly status: AsyncStatus;
  readonly data: T | null;
  readonly error: PublicApiError | null;
}

export interface AsyncSlot<T> {
  readonly state: Signal<AsyncState<T>>;
  readonly data: Signal<T | null>;
  readonly error: Signal<PublicApiError | null>;
  readonly loading: Signal<boolean>;
  readonly ready: Signal<boolean>;
  begin(): void;
  succeed(data: T): void;
  fail(error: PublicApiError): void;
  reset(): void;
}

export function asyncSlot<T>(): AsyncSlot<T> {
  const state = signal<AsyncState<T>>({ status: 'idle', data: null, error: null });

  return {
    state: state.asReadonly(),
    data: computed(() => state().data),
    error: computed(() => state().error),
    loading: computed(() => state().status === 'loading'),
    ready: computed(() => state().status === 'ready'),

    begin(): void {
      /*
       * Onceki veri KORUNUR: yeniden arama sirasinda ekranin bosalmasi
       * (ve sayfanin ziplamasi) kotu bir deneyimdir. Hata ise temizlenir —
       * eski bir hata yeni bir istegin sonucu gibi durmamalidir.
       */
      state.update((current) => ({ status: 'loading', data: current.data, error: null }));
    },
    succeed(data: T): void {
      state.set({ status: 'ready', data, error: null });
    },
    fail(error: PublicApiError): void {
      /* Hata halinde eski veri DUSURULUR: yanlis fiyat gostermektense hicbir
         sey gostermemek dogrudur (PAngV). */
      state.set({ status: 'error', data: null, error });
    },
    reset(): void {
      state.set({ status: 'idle', data: null, error: null });
    },
  };
}
