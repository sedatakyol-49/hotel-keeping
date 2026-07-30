import type {
  OccupancyCellResponse,
  OccupancyRoomResponse,
} from '../../core/models/availability.model';
import type { ReservationStatus } from '../../core/models/reservation.model';

/**
 * Doluluk izgarasi: **seyrek hucrelerden kesintisiz cubuk** kurma.
 *
 * Sunucu yalnizca **dolu** geceler icin hucre gonderir (`cells` sparse) ve her
 * hucre `isArrival` / `isDeparture` bayraklari tasir. Ekranda konaklamanin tek
 * bir kesintisiz cubuk olarak gorunmesi gerekir; bunu tabloda `colspan` ile
 * yapiyoruz:
 *
 * - Ayni rezervasyonun **ardisik kolonlardaki** geceleri tek bir `<td colspan>`
 *   olur → cubuk bolunmez, arada 1px cetvel cizgisi gorunmez.
 * - Bos geceler **tek gecelik** hucre kalir (her gece ayri ayri tiklanabilir
 *   olsun diye).
 * - Segmentlerin `nights` toplami her zaman `days.length`'e esittir →
 *   `table-fixed` + `colgroup` ile sutunlar birebir hizali kalir.
 *
 * Eslesme **kolon konumuna** gore yapilir (`days[index]`), tarih aritmetigine
 * gore degil: sunucu `days` dizisini kolon ekseni olarak tanimladigi icin
 * hizalamanin tek dogruluk kaynagi bu dizidir.
 *
 * Cikis gunu: `isDeparture` **son gece** demektir (misafir ertesi sabah cikar);
 * cikis gunu icin hucre uretilmez, bu yuzden cubuk son gecenin sonunda biter ve
 * ayni odaya ayni gun yapilan yeni giris yan yana durabilir.
 */

/** Bir konaklamanin izgaradaki kesintisiz gorunumu. */
export interface OccupancyBar {
  readonly kind: 'bar';
  readonly reservationId: string;
  readonly reservationNumber: string;
  readonly guestName: string;
  readonly status: ReservationStatus;
  /** `days` dizisindeki ilk gece indeksi. */
  readonly startIndex: number;
  /** Kolon sayisi (`colspan`) = bu pencerede gorunen gece sayisi. */
  readonly nights: number;
  /** Ilk gorunen gece `isArrival`: konaklama bu pencerede **basliyor**. */
  readonly startsInRange: boolean;
  /** Son gorunen gece `isDeparture`: konaklama bu pencerede **bitiyor**. */
  readonly endsInRange: boolean;
  /** Pencere disina tasan konaklamada ilk/son gun kirpilmistir. */
  readonly firstDate: string;
  readonly lastDate: string;
}

/** Bos (satilabilir) tek gece. */
export interface OccupancyGap {
  readonly kind: 'gap';
  readonly startIndex: number;
  /** Her zaman 1 — bos geceler birlestirilmez. */
  readonly nights: 1;
  readonly date: string;
}

export type OccupancySegment = OccupancyBar | OccupancyGap;

/** Bir oda satirinin izgara segmentleri (kolon sirasinda). */
export interface OccupancyRowView {
  readonly room: OccupancyRoomResponse;
  readonly segments: readonly OccupancySegment[];
  /** Bu odada bu pencerede gorunen konaklama sayisi. */
  readonly barCount: number;
}

/**
 * Seyrek hucreleri kolon sirasinda segmentlere cevirir.
 *
 * Degismez: `segments.reduce((sum, s) => sum + s.nights, 0) === days.length`.
 */
export function buildOccupancySegments(
  days: readonly string[],
  cells: readonly OccupancyCellResponse[],
): readonly OccupancySegment[] {
  const byDate = new Map<string, OccupancyCellResponse>();
  for (const cell of cells) {
    // Ayni gece icin ikinci hucre gelmez; gelirse ilki korunur (savunmaci).
    if (!byDate.has(cell.date)) {
      byDate.set(cell.date, cell);
    }
  }

  const segments: OccupancySegment[] = [];
  let index = 0;

  while (index < days.length) {
    const date = days[index];
    const cell = byDate.get(date);

    if (!cell) {
      segments.push({ kind: 'gap', startIndex: index, nights: 1, date });
      index += 1;
      continue;
    }

    // Cubugu ardisik kolonlar boyunca uzat: ayni rezervasyon + onceki gece
    // `isDeparture` degil (cikis gecesi cubugu kapatir).
    let end = index;
    while (end + 1 < days.length && !byDate.get(days[end])!.isDeparture) {
      const next = byDate.get(days[end + 1]);
      if (!next || next.reservationId !== cell.reservationId) {
        break;
      }
      end += 1;
    }

    const last = byDate.get(days[end])!;
    segments.push({
      kind: 'bar',
      reservationId: cell.reservationId,
      reservationNumber: cell.reservationNumber,
      guestName: cell.guestName,
      status: cell.status,
      startIndex: index,
      nights: end - index + 1,
      startsInRange: cell.isArrival,
      endsInRange: last.isDeparture,
      firstDate: days[index],
      lastDate: days[end],
    });
    index = end + 1;
  }

  return segments;
}

/** Oda satirlarini izgara gorunumune cevirir (sunucunun oda sirasi korunur). */
export function buildOccupancyRows(
  days: readonly string[],
  rooms: readonly OccupancyRoomResponse[],
): readonly OccupancyRowView[] {
  return rooms.map((room) => {
    const segments = buildOccupancySegments(days, room.cells);
    return {
      room,
      segments,
      barCount: segments.filter((segment) => segment.kind === 'bar').length,
    };
  });
}

/** Segment tip daraltmasi (sablonda `@if` icin). */
export function isOccupancyBar(segment: OccupancySegment): segment is OccupancyBar {
  return segment.kind === 'bar';
}
