import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, convertToParamMap } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';

import { PERMISSIONS } from '../../core/models/permission.model';
import {
  SHIFT_LIMITS,
  SHIFT_TYPES,
  SHIFT_TYPE_LABEL_KEYS,
  SHIFT_TYPE_SHORT_KEYS,
  isShiftType,
  type ShiftResponse,
  type ShiftType,
  type ShiftWriteRequest,
} from '../../core/models/shift.model';
import { AuthStore } from '../../core/state/auth.store';
import { LocalizedDatePipe } from '../../shared/pipes/localized-date.pipe';
import { Badge } from '../../shared/ui/badge/badge';
import { Button } from '../../shared/ui/button/button';
import { EmptyState } from '../../shared/ui/empty-state/empty-state';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { Spinner } from '../../shared/ui/spinner/spinner';
import { currentIsoWeekLabel, shiftIsoWeekLabel } from './iso-week';
import { isCurrentIsoWeek, parseShiftWeekParam, shiftWeekToParams } from './shift-week-query';
import { ShiftsStore } from './shifts.store';

/** Vardiya tipi -> hucre gorunumu (defter paletindeki tokenlar; yeni renk yok). */
const CELL_CLASSES: Readonly<Record<ShiftType, string>> = {
  Morning: 'border-brass bg-brass-tint text-brass',
  Evening: 'border-copper bg-copper-tint text-copper',
  Night: 'border-navy bg-navy-tint text-navy',
  Off: 'border-dashed border-rule-strong bg-paper text-ink-faint',
};

/**
 * Haftalik vardiya plani (`GET /shifts?week=YYYY-Www`).
 *
 * Secili hafta **URL'de** tutulur (`?week=2026-W32`): ileri/geri gezinme adres
 * cubugunu gunceller, sayfa yenilendiginde ayni hafta acilir. "Bu hafta"
 * varsayilan oldugu icin adrese yazilmaz.
 *
 * Izgara teknik notu: oda/calisan etiketi sutunu ve gun basligi satiri
 * **sticky**'dir; baslik ve satirlar **ayni** tabloda (dolayisiyla ayni yatay
 * kaydirma kabinda) durur ve sutun genislikleri `table-fixed` ile birebir
 * esitlenir — aksi halde hucreler tarihten kayardi.
 */
@Component({
  selector: 'hc-shifts',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    TranslatePipe,
    LocalizedDatePipe,
    PageHeader,
    EmptyState,
    Spinner,
    Button,
    Badge,
  ],
  templateUrl: './shifts.html',
})
export class ShiftsPage {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly formBuilder = inject(FormBuilder);
  private readonly authStore = inject(AuthStore);

  protected readonly store = inject(ShiftsStore);

  /**
   * Yazma yetkisi — hucre dugmesi yerine salt okunur tipografi cizilir.
   * Asil kontrol backend policy'sindedir (mimari §7).
   */
  protected readonly canEdit = computed(() => this.authStore.hasPermission(PERMISSIONS.ShiftsEdit));

  protected readonly shiftTypes = SHIFT_TYPES;
  protected readonly typeLabelKeys = SHIFT_TYPE_LABEL_KEYS;
  protected readonly typeShortKeys = SHIFT_TYPE_SHORT_KEYS;
  protected readonly limits = SHIFT_LIMITS;

  /** Duzenleme paneli acik mi (hucreye tiklanarak acilir). */
  protected readonly editorOpen = signal(false);

  private readonly queryParams = toSignal(this.route.queryParamMap, {
    initialValue: convertToParamMap(this.route.snapshot.queryParams),
  });

  /** URL tek dogruluk kaynagi; gecersiz etiket bu haftaya duser. */
  protected readonly week = computed(() => parseShiftWeekParam(this.queryParams()));
  protected readonly isCurrentWeek = computed(() => isCurrentIsoWeek(this.week()));

  protected readonly form = this.formBuilder.nonNullable.group({
    employeeId: ['', [Validators.required]],
    date: ['', [Validators.required]],
    shiftType: ['Morning' as ShiftType, [Validators.required]],
    note: ['', [Validators.maxLength(SHIFT_LIMITS.noteMaxLength)]],
  });

  private readonly formValue = toSignal(this.form.valueChanges, {
    initialValue: this.form.getRawValue(),
  });

  /** Panelde secili hucrede halihazirda bir vardiya var mi (PUT/DELETE icin). */
  protected readonly existingShift = computed<ShiftResponse | null>(() => {
    const value = this.formValue();
    if (!value.employeeId || !value.date) {
      return null;
    }
    return this.store.shiftFor(value.employeeId, value.date);
  });

  protected readonly writeErrorKey = computed(() => {
    const error = this.store.writeError();
    if (!error) {
      return null;
    }
    if (error.status === 409) {
      return 'shifts.editor.conflict';
    }
    if (error.status === 404) {
      return 'shifts.editor.notFound';
    }
    return error.messageKey;
  });

  constructor() {
    // Hafta degistikce plan yenilenir (ilk yukleme dahil).
    effect(() => {
      const week = this.week();
      this.editorOpen.set(false);
      void this.store.load(week);
    });
  }

  protected goToWeek(delta: number): void {
    void this.navigate(shiftIsoWeekLabel(this.week(), delta));
  }

  protected goToCurrentWeek(): void {
    void this.navigate(currentIsoWeekLabel());
  }

  protected retry(): void {
    void this.store.reload();
  }

  protected shiftFor(employeeId: string, date: string): ShiftResponse | null {
    return this.store.shiftFor(employeeId, date);
  }

  protected cellClass(type: ShiftType | null): string {
    return type === null ? 'border-transparent text-ink-faint' : CELL_CLASSES[type];
  }

  /** Hucreye tiklama: panel secili calisan/gun ile acilir. */
  protected openEditor(employeeId: string, date: string): void {
    const existing = this.store.shiftFor(employeeId, date);
    this.store.clearWriteError();
    this.form.reset({
      employeeId,
      date,
      shiftType: existing?.shiftType ?? 'Morning',
      note: existing?.note ?? '',
    });
    this.editorOpen.set(true);
  }

  /**
   * Bos panel — mobil yerlesimde tiklanacak hucre yoktur, atama buradan
   * baslatilir. Gun varsayilan olarak haftanin ilk gunudur.
   */
  protected openBlankEditor(): void {
    this.store.clearWriteError();
    this.form.reset({
      employeeId: '',
      date: this.store.days()[0]?.date ?? '',
      shiftType: 'Morning',
      note: '',
    });
    this.editorOpen.set(true);
  }

  protected closeEditor(): void {
    this.editorOpen.set(false);
    this.store.clearWriteError();
  }

  protected async save(): Promise<void> {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const raw = this.form.getRawValue();
    const request: ShiftWriteRequest = {
      employeeId: raw.employeeId,
      date: raw.date,
      shiftType: isShiftType(raw.shiftType) ? raw.shiftType : 'Morning',
      note: raw.note.trim() || null,
    };
    const existing = this.existingShift();
    const error = await this.store.save(request, existing?.id ?? null);
    if (error === null) {
      this.editorOpen.set(false);
    }
  }

  protected async remove(): Promise<void> {
    const existing = this.existingShift();
    if (!existing) {
      return;
    }
    const error = await this.store.remove(existing.id);
    if (error === null) {
      this.editorOpen.set(false);
    }
  }

  private navigate(week: string): Promise<boolean> {
    return this.router.navigate([], {
      relativeTo: this.route,
      queryParams: shiftWeekToParams(week),
    });
  }
}
