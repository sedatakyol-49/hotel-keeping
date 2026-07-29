import { Injectable, computed, inject } from '@angular/core';

import type { HotelSummary } from '../models/hotel.model';
import { AuthStore } from '../state/auth.store';

const ACTIVE_HOTEL_KEY = 'hotelcore.activeHotelId';

/**
 * Aktif otel yonetimi (multi-tenant baglami).
 *
 * Aktif otel `X-Hotel-Id` basligi ile her istege eklenir (api-contracts.md).
 * Head Office kullanicisi `null` secerse baslik gonderilmez -> backend
 * konsolide (tum oteller) gorunum dondurur.
 */
@Injectable({ providedIn: 'root' })
export class CurrentHotelService {
  private readonly authStore = inject(AuthStore);

  readonly hotelId = this.authStore.activeHotelId;
  readonly hotel = this.authStore.activeHotel;
  readonly hotels = this.authStore.hotels;
  readonly canSwitch = computed(
    () => this.hotels().length > 1 || this.authStore.canAccessAllHotels(),
  );
  /** Konsolide gorunum: otel secili degil ve kullanici tum otellere erisebiliyor. */
  readonly isConsolidated = computed(
    () => this.hotelId() === null && this.authStore.canAccessAllHotels(),
  );

  /** Oturum kurulurken en son secilen otel hatirlanir. */
  readPreferredHotelId(): string | null {
    try {
      return globalThis.localStorage?.getItem(ACTIVE_HOTEL_KEY) ?? null;
    } catch {
      return null;
    }
  }

  select(hotel: HotelSummary | null): boolean {
    return this.selectById(hotel?.id ?? null);
  }

  selectById(hotelId: string | null): boolean {
    const changed = this.authStore.setActiveHotel(hotelId);
    if (changed) {
      this.persist(hotelId);
    }
    return changed;
  }

  /** Oturum kapanisinda cagrilir. */
  forget(): void {
    this.persist(null);
  }

  private persist(hotelId: string | null): void {
    try {
      if (hotelId === null) {
        globalThis.localStorage?.removeItem(ACTIVE_HOTEL_KEY);
      } else {
        globalThis.localStorage?.setItem(ACTIVE_HOTEL_KEY, hotelId);
      }
    } catch {
      // Depolama yoksa secim yalnizca bu oturumda gecerli olur.
    }
  }
}
