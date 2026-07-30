import { ChangeDetectionStrategy, Component, computed, effect, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink, convertToParamMap } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';

import { OCCUPANCY_MAX_DAYS, addDays } from '../../core/models/availability.model';
import { PERMISSIONS } from '../../core/models/permission.model';
import { AuthStore } from '../../core/state/auth.store';
import { LocalizedDatePipe } from '../../shared/pipes/localized-date.pipe';
import { Button } from '../../shared/ui/button/button';
import { EmptyState } from '../../shared/ui/empty-state/empty-state';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { Spinner } from '../../shared/ui/spinner/spinner';
import type { OccupancyBar, OccupancySegment } from './occupancy-grid';
import {
  OCCUPANCY_RANGE_OPTIONS,
  isOccupancyRangeAtToday,
  moveOccupancyRangeToToday,
  occupancyRangeToParams,
  parseOccupancyRange,
  rangeNights,
  resizeOccupancyRange,
  shiftOccupancyRange,
  type OccupancyRange,
} from './occupancy-query';
import { OccupancyStore } from './occupancy.store';
import { OCCUPANCY_BAR_CLASSES } from './reservation-status';

/**
 * Doluluk plani (`GET /occupancy`) — oda × gun izgarasi.
 *
 * **Hizalama teknigi** (vardiya izgarasiyla ayni): gun basligi satiri ve oda
 * satirlari **ayni** tablodadir (dolayisiyla ayni yatay kaydirma kabinda),
 * sutun genislikleri `table-fixed` + `colgroup` ile birebir esitlenir; oda
 * etiketi sutunu ve gun basligi **sticky**'dir. Aksi halde cubuklar tarihten
 * kayardi.
 *
 * **Seyrek hucre -> kesintisiz cubuk**: sunucu yalnizca dolu geceler icin hucre
 * gonderir; `buildOccupancySegments` ardisik geceleri tek bir `<td colspan>`
 * segmentine cevirir (bkz. `occupancy-grid.ts`). Cikis gunu icin hucre
 * uretilmedigi icin cubuk son gecede biter.
 *
 * **92 gun siniri**: sunucu daha genis aralikta 400 doner. Aralik URL'den
 * okunurken kirpilir (`parseOccupancyRange`) ve kullaniciya aciklama gosterilir;
 * istemci hicbir zaman gecersiz araligi istemez.
 */
@Component({
  selector: 'hc-occupancy-plan',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, TranslatePipe, LocalizedDatePipe, PageHeader, EmptyState, Spinner, Button],
  templateUrl: './occupancy-plan.html',
})
export class OccupancyPlanPage {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly authStore = inject(AuthStore);

  protected readonly store = inject(OccupancyStore);

  protected readonly maxDays = OCCUPANCY_MAX_DAYS;
  protected readonly rangeOptions = OCCUPANCY_RANGE_OPTIONS;
  protected readonly createPermission = PERMISSIONS.ReservationsCreate;

  /** Bos geceden sihirbaza gecis yalnizca yazma izniyle anlamlidir. */
  protected readonly canCreate = computed(() =>
    this.authStore.hasPermission(PERMISSIONS.ReservationsCreate),
  );

  private readonly queryParams = toSignal(this.route.queryParamMap, {
    initialValue: convertToParamMap(this.route.snapshot.queryParams),
  });

  /** URL tek dogruluk kaynagi; 92 gunu asan aralik kirpilarak okunur. */
  protected readonly range = computed(() => parseOccupancyRange(this.queryParams()));
  protected readonly nights = computed(() => rangeNights(this.range()));
  protected readonly isAtToday = computed(() => isOccupancyRangeAtToday(this.range()));
  /** Aralik gosteriminde son **gece** (cikis gunu degil). */
  protected readonly lastNight = computed(() => addDays(this.range().to, -1));

  constructor() {
    // Aralik degistikce matris yenilenir (ilk yukleme dahil).
    effect(() => {
      const range = this.range();
      void this.store.load({ from: range.from, to: range.to });
    });
  }

  protected shift(direction: 1 | -1): void {
    void this.navigate(shiftOccupancyRange(this.range(), direction));
  }

  protected resize(nights: number): void {
    void this.navigate(resizeOccupancyRange(this.range(), nights));
  }

  protected goToToday(): void {
    void this.navigate(moveOccupancyRangeToToday(this.range()));
  }

  protected retry(): void {
    void this.store.reload();
  }

  protected isBar(segment: OccupancySegment): segment is OccupancyBar {
    return segment.kind === 'bar';
  }

  /** Cubuk gorunumu: durum tonu + pencere disina tasan uclarda acik kenar. */
  protected barClass(bar: OccupancyBar): string {
    const tone = OCCUPANCY_BAR_CLASSES[bar.status];
    // Kirpilmis uc: kenarlik kaldirilir, konaklamanin devam ettigi anlasilir.
    const open = [
      bar.startsInRange ? '' : 'border-l-0',
      bar.endsInRange ? '' : 'border-r-0',
    ]
      .filter(Boolean)
      .join(' ');
    return `${tone} ${open}`.trim();
  }

  /** Bos gecede sihirbaza on-doldurulmus baglanti (`from` = o gece). */
  protected newReservationParams(roomId: string, date: string): Record<string, string> {
    return { roomId, from: date, to: addDays(date, 1) };
  }

  private navigate(range: OccupancyRange): Promise<boolean> {
    return this.router.navigate([], {
      relativeTo: this.route,
      queryParams: occupancyRangeToParams(range),
      queryParamsHandling: 'merge',
    });
  }
}
