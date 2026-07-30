import { describe, expect, it } from 'vitest';

import type {
  OccupancyCellResponse,
  OccupancyRoomResponse,
} from '../../core/models/availability.model';
import { buildOccupancyRows, buildOccupancySegments, isOccupancyBar } from './occupancy-grid';

/** `2026-08-09` .. `2026-08-15` (7 kolon). */
const DAYS = [
  '2026-08-09',
  '2026-08-10',
  '2026-08-11',
  '2026-08-12',
  '2026-08-13',
  '2026-08-14',
  '2026-08-15',
];

function cell(
  date: string,
  overrides: Partial<OccupancyCellResponse> = {},
): OccupancyCellResponse {
  return {
    date,
    reservationId: 'res-1',
    reservationNumber: 'RES-2026-00001',
    guestName: 'Jürgen Müller',
    status: 'Confirmed',
    isArrival: false,
    isDeparture: false,
    ...overrides,
  };
}

describe('buildOccupancySegments — seyrek hucrelerden kesintisiz cubuk', () => {
  it('ardisik geceleri TEK bir cubukta birlestirir ve cikis gunu once bitirir', () => {
    // Sozlesme ornegi: 2026-08-10 giris, 2026-08-12 cikis -> iki gece hucresi.
    // `isDeparture` SON GECEdir (11), cikis gunu (12) icin hucre uretilmez.
    const segments = buildOccupancySegments(DAYS, [
      cell('2026-08-10', { isArrival: true }),
      cell('2026-08-11', { isDeparture: true }),
    ]);

    const bars = segments.filter(isOccupancyBar);
    expect(bars).toHaveLength(1);
    expect(bars[0]).toMatchObject({
      reservationNumber: 'RES-2026-00001',
      startIndex: 1,
      nights: 2,
      startsInRange: true,
      endsInRange: true,
      firstDate: '2026-08-10',
      lastDate: '2026-08-11',
    });

    // Cubuk cikis gununde (indeks 3) bitmis olmali: o kolon bos gecedir.
    const gapDates = segments.filter((segment) => !isOccupancyBar(segment)).map((s) => s.date);
    expect(gapDates).toContain('2026-08-12');
  });

  it('segmentlerin toplam gece sayisi kolon sayisina esittir (sutunlar kaymaz)', () => {
    const segments = buildOccupancySegments(DAYS, [
      cell('2026-08-10', { isArrival: true }),
      cell('2026-08-11'),
      cell('2026-08-12', { isDeparture: true }),
      cell('2026-08-14', {
        reservationId: 'res-2',
        reservationNumber: 'RES-2026-00002',
        guestName: 'Anna Becker',
        status: 'Option',
        isArrival: true,
        isDeparture: true,
      }),
    ]);

    // Bu degismez bozulursa `colspan` toplami `colgroup`'tan sapar ve
    // cubuklar tarihten kayar.
    const total = segments.reduce((sum, segment) => sum + segment.nights, 0);
    expect(total).toBe(DAYS.length);

    const bars = segments.filter(isOccupancyBar);
    expect(bars.map((bar) => [bar.startIndex, bar.nights])).toEqual([
      [1, 3],
      [5, 1],
    ]);
    expect(bars[1].status).toBe('Option');
  });

  it('ardisik satista iki konaklamayi AYRI cubuk yapar (cikis gunu = yeni giris)', () => {
    // Bir misafirin cikis gunu, ayni odada baska bir rezervasyonun giris gunu
    // olabilir; bu ardisik satis serbesttir ve tek cubuga birlesmemelidir.
    const segments = buildOccupancySegments(DAYS, [
      cell('2026-08-09', { isArrival: true }),
      cell('2026-08-10', { isDeparture: true }),
      cell('2026-08-11', {
        reservationId: 'res-2',
        reservationNumber: 'RES-2026-00002',
        guestName: 'Anna Becker',
        isArrival: true,
      }),
      cell('2026-08-12', {
        reservationId: 'res-2',
        reservationNumber: 'RES-2026-00002',
        guestName: 'Anna Becker',
        isDeparture: true,
      }),
    ]);

    const bars = segments.filter(isOccupancyBar);
    expect(bars).toHaveLength(2);
    expect(bars[0].reservationId).toBe('res-1');
    expect(bars[0].nights).toBe(2);
    expect(bars[1].reservationId).toBe('res-2');
    expect(bars[1].startIndex).toBe(2);
    expect(bars[1].nights).toBe(2);
  });

  it('pencere disina tasan konaklamayi kirpar ve acik uclari isaretler', () => {
    // Hucrelerde `isArrival`/`isDeparture` yok: konaklama pencerenin her iki
    // yaninda da devam ediyor demektir.
    const segments = buildOccupancySegments(DAYS, DAYS.map((day) => cell(day)));

    const bars = segments.filter(isOccupancyBar);
    expect(bars).toHaveLength(1);
    expect(bars[0]).toMatchObject({
      startIndex: 0,
      nights: DAYS.length,
      startsInRange: false,
      endsInRange: false,
    });
  });

  it('bos odada yalnizca tek gecelik bosluklar uretir', () => {
    const segments = buildOccupancySegments(DAYS, []);

    expect(segments).toHaveLength(DAYS.length);
    expect(segments.every((segment) => segment.kind === 'gap' && segment.nights === 1)).toBe(true);
  });
});

describe('buildOccupancyRows', () => {
  it('sunucunun oda sirasini korur ve konaklama sayisini sayar', () => {
    const rooms: readonly OccupancyRoomResponse[] = [
      {
        roomId: 'r-1',
        roomNumber: '201',
        floor: 2,
        roomTypeId: 't-1',
        roomTypeCode: 'DBL',
        isOutOfOrder: false,
        cells: [cell('2026-08-10', { isArrival: true }), cell('2026-08-11', { isDeparture: true })],
      },
      {
        roomId: 'r-2',
        roomNumber: '204',
        floor: 2,
        roomTypeId: 't-1',
        roomTypeCode: 'DBL',
        isOutOfOrder: true,
        cells: [],
      },
    ];

    const rows = buildOccupancyRows(DAYS, rooms);
    expect(rows.map((row) => row.room.roomNumber)).toEqual(['201', '204']);
    expect(rows[0].barCount).toBe(1);
    expect(rows[1].barCount).toBe(0);
    // Servis disi oda satiri yine cizilir (planlamada gorunur kalmali).
    expect(rows[1].segments).toHaveLength(DAYS.length);
  });
});
