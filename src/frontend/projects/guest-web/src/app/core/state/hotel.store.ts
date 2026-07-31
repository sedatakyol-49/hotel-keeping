import { Injectable, computed, inject } from '@angular/core';

import { PublicBookingApi } from '../api/public-booking.api';
import type { PublicHotel, PublicLegalResponse } from '../api/public-models';
import { toPublicError } from '../api/public-error';
import { transferredSlot } from './transferred-slot';

/**
 * ===========================================================================
 * OTEL KUNYESI + HUKUKI BELGELER (signal store)
 * ===========================================================================
 *
 * Bu iki uc **her sayfada** gerekir (alt bilgi, fiyat notlari, form sinirlari),
 * bu yuzden istek **tekillestirilir**: `load()` kac kez cagrilirsa cagrilsin
 * ag trafigi bir kezdir.
 *
 * ---------------------------------------------------------------------------
 * SUNUCUDAN ISTEMCIYE DEVIR — ve neden Angular'in HTTP aktarim onbellegi
 * bu uygulamada ISE YARAMIYOR
 * ---------------------------------------------------------------------------
 * Olculdu (gercek Chrome, uretim derlemesi): `/de/legal/terms` masaustunde
 * **CLS 0.60** uretiyordu. Kaynak `HCG-GUEST-FOOTER` gorunuyordu ama alt bilgi
 * susuzdu — yalnizca TASINIYORDU. Zincir soyle:
 *
 *   1. Sunucu (prerender) hukuki metni HTML'e basar; sayfa uzundur.
 *   2. Tarayici hidre olur, ama istemci deposu BOSTUR — bu store'un signal'leri
 *      sunucudan tasinmaz.
 *   3. `LegalPage` yapicisi `loadLegal()` cagirir, `GET /legal` **ikinci kez**
 *      gider ve yanit gelince makale yeniden cizilir.
 *   4. Canli metin ile derleme anindaki metin ayni uzunlukta degilse sayfa
 *      kisalir/uzar; altindaki her sey — yani alt bilgi — ziplar. Olculen
 *      sicrama 348px'di; ustune dikey kaydirma cubugu kayboldugu icin sayfa
 *      7px de yana kaydi.
 *
 * Angular'in `provideClientHydration()` ile gelen HTTP aktarim onbellegi bunu
 * kapatmiyor — nedeni (araci zincir sirasi, mutlaklastirilan adres, prerender
 * kisa devresi) `transferred-slot.ts` icinde ayrintili. Devir bu yuzden ACIKCA
 * yapilir: her iki slot da `transferredSlot`tur, sunucu yaniti belgeye ilistirir
 * ve tarayici onu devralir. Sonuc: istemci ayni veriyi ikinci kez cekmez,
 * makale yeniden cizilmez, alt bilgi hic kimildamaz (olculdu: CLS 0).
 *
 * ARKA PLANDA TAZELEME YOK — bilincli. Tazeleme, tam da kaldirdigimiz ikinci
 * cizimi geri getirirdi. Tazelik su iki mekanizmadan gelir: SSR sayfalarinda
 * veri zaten milisaniyeler once alinmistir; prerender edilen hukuki sayfalarda
 * ise metin **derleme anindaki yayimlanmis metindir** — bu, o rotanin
 * `RenderMode.Prerender` secilmesinin gerekcesinin ta kendisidir
 * (app.routes.server.ts) ve JS calistirmayan ziyaretcinin gordugu metinle
 * calistiranin gordugu metni ayni yapar.
 */
@Injectable({ providedIn: 'root' })
export class HotelStore {
  private readonly api = inject(PublicBookingApi);

  private readonly hotelSlot = transferredSlot<PublicHotel>('hc.hotel');
  private readonly legalSlot = transferredSlot<PublicLegalResponse>('hc.legal');

  readonly hotel = this.hotelSlot.data;
  readonly hotelState = this.hotelSlot.state;
  readonly legal = this.legalSlot.data;
  readonly legalState = this.legalSlot.state;

  /** Rezervasyon sinirlari — form bunlari kendi uydurmaz. */
  readonly limits = computed(() => {
    const booking = this.hotel()?.booking;
    return {
      minNights: booking?.minNights ?? 1,
      maxNights: booking?.maxNights ?? 30,
      maxAdults: booking?.maxAdults ?? 6,
      maxChildren: booking?.maxChildren ?? 6,
      maxAdvanceDays: booking?.maxAdvanceDays ?? 365,
    };
  });

  /** Kurtaxe bilgilendirmesi arama ekraninda da gorunur (PAngV). */
  readonly cityTax = computed(() => this.hotel()?.cityTax ?? null);

  /*
   * `adopt()` cagrisi tekilleştirme kontrolunun HEMEN yanindadir: devir
   * basarili olursa slot 'ready' olur ve istek hic acilmaz. Yapiciya
   * konmadi cunku devrin gerekcesi ile istegin gerekcesi ayni yerde
   * okunabilmeli — biri digeri olmadan anlamli degil.
   */
  load(): void {
    if (this.hotelSlot.state().status !== 'idle' || this.hotelSlot.adopt()) {
      return;
    }
    this.hotelSlot.begin();
    this.api.getHotel().subscribe({
      next: (hotel) => {
        this.hotelSlot.succeed(hotel);
        this.hotelSlot.handOver(hotel);
      },
      error: (error: unknown) => this.hotelSlot.fail(toPublicError(error)),
    });
  }

  loadLegal(): void {
    if (this.legalSlot.state().status !== 'idle' || this.legalSlot.adopt()) {
      return;
    }
    this.legalSlot.begin();
    this.api.getLegal().subscribe({
      next: (legal) => {
        this.legalSlot.succeed(legal);
        this.legalSlot.handOver(legal);
      },
      error: (error: unknown) => this.legalSlot.fail(toPublicError(error)),
    });
  }

  /** Hata sonrasi "yeniden dene" — durumu sifirlayip tekrar ister. */
  retry(): void {
    this.hotelSlot.reset();
    this.load();
  }

  retryLegal(): void {
    this.legalSlot.reset();
    this.loadLegal();
  }

}
