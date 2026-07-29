import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

import { CurrentHotelService } from '../../core/services/current-hotel.service';
import { AuthStore } from '../../core/state/auth.store';

/**
 * Aktif otel secici (multi-tenant). Secim `X-Hotel-Id` basligini degistirir;
 * JWT yeniden alinmaz. Head Office kullanicisi "tum oteller" secerek
 * konsolide gorunume gecebilir.
 */
@Component({
  selector: 'hc-hotel-switcher',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe],
  template: `
    @if (hotels().length > 0 || authStore.canAccessAllHotels()) {
      <div class="flex min-w-0 items-center gap-2">
        <label class="eyebrow hidden sm:block" for="hc-hotel-switcher">
          {{ 'hotel.label' | translate }}
        </label>
        <div class="relative min-w-0">
          <select
            id="hc-hotel-switcher"
            class="hc-input min-w-[10rem] appearance-none pr-8"
            [attr.aria-label]="'hotel.switcherLabel' | translate"
            [value]="currentHotel.hotelId() ?? ''"
            (change)="onChange($event)"
          >
            @if (authStore.canAccessAllHotels()) {
              <option value="">{{ 'hotel.allHotels' | translate }}</option>
            } @else if (currentHotel.hotelId() === null) {
              <option value="" disabled>{{ 'hotel.select' | translate }}</option>
            }
            @for (hotel of hotels(); track hotel.id) {
              <option [value]="hotel.id">{{ hotel.name }}</option>
            }
          </select>
          <span
            class="pointer-events-none absolute inset-y-0 right-2 flex items-center text-ink-faint"
            aria-hidden="true"
            >&#9662;</span
          >
        </div>
      </div>
    } @else {
      <p class="eyebrow">{{ 'hotel.none' | translate }}</p>
    }
  `,
})
export class HotelSwitcher {
  protected readonly currentHotel = inject(CurrentHotelService);
  protected readonly authStore = inject(AuthStore);
  protected readonly hotels = this.currentHotel.hotels;

  protected onChange(event: Event): void {
    const value = (event.target as HTMLSelectElement).value;
    this.currentHotel.selectById(value === '' ? null : value);
  }
}
