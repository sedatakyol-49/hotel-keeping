import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { RoomTypesApi } from '../../core/api/room-types.api';
import { RoomsApi } from '../../core/api/rooms.api';
import { PERMISSIONS, type PermissionKey } from '../../core/models/permission.model';
import type { HousekeepingSummary } from '../../core/models/room.model';
import { AuthStore } from '../../core/state/auth.store';
import { NAV_SECTIONS, filterNavSections, type NavSummaryKind } from '../../layout/navigation';

/** Ozet satirlarini besleyen gercek uclar (baska uc henuz yazilmadi). */
type HubSource = 'rooms' | 'roomTypes' | 'board';

/** `idle` = izin yok veya gorunur kart bu kaynagi istemiyor -> istek hic yapilmaz. */
type SourceState = 'idle' | 'loading' | 'ready' | 'error';

/** Kartin cetvel altindaki canli ozet satiri. */
export interface HubSummaryView {
  readonly state: 'loading' | 'ready' | 'error';
  /** i18n anahtari — birim kelimesini icerir (ekran okuyucu "13" degil "13 Zimmer" duyar). */
  readonly textKey: string;
  readonly params: Readonly<Record<string, number>>;
}

const LOADING_VIEW: HubSummaryView = {
  state: 'loading',
  textKey: 'hub.summary.loading',
  params: {},
};

/** Hata: sayi yerine gosterge; kart yine tiklanabilir kalir. */
const ERROR_VIEW: HubSummaryView = {
  state: 'error',
  textKey: 'hub.summary.unavailable',
  params: {},
};

const IDLE_STATES: Readonly<Record<HubSource, SourceState>> = {
  rooms: 'idle',
  roomTypes: 'idle',
  board: 'idle',
};

/** Ucun backend policy'sinde bekledigi izin — eksikse istek gonderilmez (403 uretilmez). */
const SOURCE_PERMISSION: Readonly<Record<HubSource, PermissionKey>> = {
  rooms: PERMISSIONS.RoomsView,
  roomTypes: PERMISSIONS.RoomsView,
  board: PERMISSIONS.HousekeepingView,
};

/**
 * Kart ozeti -> ihtiyac duydugu uclar.
 *
 * `rooms` karti hem `GET /rooms` (toplam) hem `GET /rooms/board` (kirli oda)
 * degerini gosterir; `housekeeping` karti **ayni** board yanitini okur, ikinci
 * bir istek yapilmaz.
 */
const SUMMARY_SOURCES: Readonly<Record<NavSummaryKind, readonly HubSource[]>> = {
  rooms: ['rooms', 'board'],
  roomTypes: ['roomTypes'],
  housekeeping: ['board'],
};

/**
 * Hub (launcher) ozet store'u.
 *
 * Tek yerden veri ceker ve tum kartlar ayni sinyalleri okur. Hangi uclarin
 * cagrilacagi, kullanicinin **gordugu** kartlardan turetilir (`NAV_SECTIONS` +
 * izinler); gorunmeyen bir kart icin istek yapilmaz. Hata modul erisimini
 * engellemez: yalnizca ilgili ozet satiri hata gostergesine doner.
 */
@Injectable({ providedIn: 'root' })
export class HubStore {
  private readonly roomsApi = inject(RoomsApi);
  private readonly roomTypesApi = inject(RoomTypesApi);
  private readonly authStore = inject(AuthStore);

  private readonly _states = signal<Readonly<Record<HubSource, SourceState>>>(IDLE_STATES);
  private readonly _roomCount = signal<number | null>(null);
  private readonly _roomTypeCount = signal<number | null>(null);
  private readonly _board = signal<HousekeepingSummary | null>(null);

  /** Ust uste gelen yuklemelerde yalnizca en son yanit yazilir. */
  private token = 0;

  readonly loading = computed(() =>
    Object.values(this._states()).some((state) => state === 'loading'),
  );

  /**
   * Gorunur kartlarin ihtiyac duydugu ve izin verilen uclar.
   * Ornek: `Housekeeping.View` yoksa `board` bu kumede **hic** yer almaz.
   */
  readonly requiredSources = computed<ReadonlySet<HubSource>>(() => {
    const required = new Set<HubSource>();
    const visible = filterNavSections(NAV_SECTIONS, (item) =>
      this.authStore.matchesPermissions(item.permissions),
    );

    for (const section of visible) {
      for (const item of section.items) {
        const kind = item.hub?.summary;
        if (kind === undefined) {
          continue;
        }
        for (const source of SUMMARY_SOURCES[kind]) {
          if (this.authStore.hasPermission(SOURCE_PERMISSION[source])) {
            required.add(source);
          }
        }
      }
    }
    return required;
  });

  /** Kart tipine gore ozet gorunumu; `null` ise satirda hicbir sey gosterilmez. */
  readonly summaries = computed<Readonly<Record<NavSummaryKind, HubSummaryView | null>>>(() => ({
    rooms: this.roomsSummary(),
    roomTypes: this.roomTypesSummary(),
    housekeeping: this.housekeepingSummary(),
  }));

  /** Odalar: toplam oda sayisi + (izin varsa) ayni board yanitindan kirli oda. */
  private readonly roomsSummary = computed<HubSummaryView | null>(() => {
    const state = this._states().rooms;
    const total = this._roomCount();
    if (state === 'idle') {
      return null;
    }
    if (state === 'error') {
      return ERROR_VIEW;
    }
    if (total === null) {
      return LOADING_VIEW;
    }

    const board = this._states().board === 'ready' ? this._board() : null;
    return board === null
      ? { state: 'ready', textKey: 'hub.summary.rooms', params: { count: total } }
      : {
          state: 'ready',
          textKey: 'hub.summary.roomsDirty',
          params: { count: total, dirty: board.dirty },
        };
  });

  private readonly roomTypesSummary = computed<HubSummaryView | null>(() => {
    const state = this._states().roomTypes;
    const count = this._roomTypeCount();
    if (state === 'idle') {
      return null;
    }
    if (state === 'error') {
      return ERROR_VIEW;
    }
    if (count === null) {
      return LOADING_VIEW;
    }
    return { state: 'ready', textKey: 'hub.summary.roomTypes', params: { count } };
  });

  private readonly housekeepingSummary = computed<HubSummaryView | null>(() => {
    const state = this._states().board;
    const board = this._board();
    if (state === 'idle') {
      return null;
    }
    if (state === 'error') {
      return ERROR_VIEW;
    }
    if (board === null) {
      return LOADING_VIEW;
    }

    /*
     * Bekleyen is mantigi (`GET /rooms/board` -> `summary`):
     *   Dirty     -> temizlenmeyi bekliyor          -> acik is
     *   Clean     -> temizlenmis, **kontrol edilmemis** -> acik is
     *   Inspected -> kontrol edilmis, kapanmis      -> acik is DEGIL
     *   OutOfOrder-> satista degil, is kuyrugu disi -> acik is DEGIL
     * Durum akisi Dirty -> Clean -> Inspected oldugu icin `Clean` odalar
     * "kontrol bekleyen" is olarak sayilir; ayrica kirli oda sayisi ayrica
     * gosterilir ki iki is turu birbirine karismasin.
     */
    const open = board.dirty + board.clean;
    return open === 0
      ? { state: 'ready', textKey: 'hub.summary.housekeepingClear', params: {} }
      : {
          state: 'ready',
          textKey: 'hub.summary.housekeeping',
          params: { count: open, dirty: board.dirty },
        };
  });

  /** Ozetleri yeniden ceker (ilk acilis, otel degisimi, "Aktualisieren"). */
  async load(): Promise<void> {
    const token = ++this.token;
    const required = this.requiredSources();

    this._states.set({
      rooms: required.has('rooms') ? 'loading' : 'idle',
      roomTypes: required.has('roomTypes') ? 'loading' : 'idle',
      board: required.has('board') ? 'loading' : 'idle',
    });

    await Promise.all([
      required.has('rooms') ? this.loadRoomCount(token) : Promise.resolve(),
      required.has('roomTypes') ? this.loadRoomTypeCount(token) : Promise.resolve(),
      required.has('board') ? this.loadBoard(token) : Promise.resolve(),
    ]);
  }

  /** `GET /rooms` — yalnizca `totalCount` gerekli, bu yuzden en kucuk sayfa istenir. */
  private async loadRoomCount(token: number): Promise<void> {
    try {
      const result = await firstValueFrom(this.roomsApi.list({ page: 1, pageSize: 1 }));
      if (token !== this.token) {
        return;
      }
      this._roomCount.set(result.totalCount);
      this.markState('rooms', 'ready');
    } catch {
      if (token !== this.token) {
        return;
      }
      this._roomCount.set(null);
      this.markState('rooms', 'error');
    }
  }

  /** `GET /room-types` — sayfalama yok, dizi uzunlugu oda tipi sayisidir. */
  private async loadRoomTypeCount(token: number): Promise<void> {
    try {
      const types = await firstValueFrom(this.roomTypesApi.list());
      if (token !== this.token) {
        return;
      }
      this._roomTypeCount.set(types.length);
      this.markState('roomTypes', 'ready');
    } catch {
      if (token !== this.token) {
        return;
      }
      this._roomTypeCount.set(null);
      this.markState('roomTypes', 'error');
    }
  }

  /** `GET /rooms/board` — iki kart (Odalar + Housekeeping) bu tek yanitla beslenir. */
  private async loadBoard(token: number): Promise<void> {
    try {
      const board = await firstValueFrom(this.roomsApi.board());
      if (token !== this.token) {
        return;
      }
      this._board.set(board.summary);
      this.markState('board', 'ready');
    } catch {
      if (token !== this.token) {
        return;
      }
      this._board.set(null);
      this.markState('board', 'error');
    }
  }

  private markState(source: HubSource, state: SourceState): void {
    this._states.update((current) => ({ ...current, [source]: state }));
  }
}
