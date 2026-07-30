import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import {
  FormBuilder,
  ReactiveFormsModule,
  Validators,
  type AbstractControl,
  type ValidationErrors,
} from '@angular/forms';
import { ActivatedRoute, Router, convertToParamMap } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';

import { PERMISSIONS } from '../../core/models/permission.model';
import {
  TIME_ENTRY_LIMITS,
  formatWorkedMinutes,
  fromDateTimeLocalValue,
  grossMinutesBetween,
  toDateTimeLocalValue,
  type TimeEntryListQuery,
  type TimeEntryResponse,
  type UpdateTimeEntryRequest,
} from '../../core/models/time-entry.model';
import { HasPermissionDirective } from '../../shared/directives/has-permission.directive';
import { applyApiFieldErrors, serverErrorMessages } from '../../shared/forms/api-field-errors';
import { isIsoDate } from '../../shared/forms/date-validators';
import { integerRangeValidator, parseInteger } from '../../shared/forms/numeric-validators';
import { LocalizedDatePipe } from '../../shared/pipes/localized-date.pipe';
import { Badge } from '../../shared/ui/badge/badge';
import { Button } from '../../shared/ui/button/button';
import { EmptyState } from '../../shared/ui/empty-state/empty-state';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { Spinner } from '../../shared/ui/spinner/spinner';
import { TableShell } from '../../shared/ui/table-shell/table-shell';
import { EmployeeOptionsStore } from '../employees/employee-options.store';
import {
  TIME_ENTRY_PAGE_SIZE_OPTIONS,
  parseTimeEntryListQuery,
  timeEntryListQueryToParams,
  withTimeEntryFilterChange,
} from './time-entry-list-query';
import { TimeTrackingStore } from './time-tracking.store';

type TimeEntryFormControl = 'clockIn' | 'clockOut' | 'breakMinutes' | 'note';

/**
 * Manuel duzeltme kurallari (sozlesme): `clockOut > clockIn` ve `breakMinutes`
 * **brut** calisma suresini asamaz. Sunucu ayni kurali uygular ve mesajinda
 * mevcut sureyi soyler; istemci ayni bilgiyi onceden gosterir.
 */
export function timeEntryConsistencyValidator(group: AbstractControl): ValidationErrors | null {
  const clockIn = fromDateTimeLocalValue(String(group.get('clockIn')?.value ?? ''));
  const clockOut = fromDateTimeLocalValue(String(group.get('clockOut')?.value ?? ''));
  if (clockIn === null || clockOut === null) {
    return null;
  }
  const gross = grossMinutesBetween(clockIn, clockOut);
  if (gross === null) {
    return null;
  }
  if (gross <= 0) {
    return { clockOutOrder: true };
  }
  const breakMinutes = parseInteger(String(group.get('breakMinutes')?.value ?? ''));
  if (breakMinutes !== null && breakMinutes > gross) {
    return { breakExceedsWork: { minutes: gross } };
  }
  return null;
}

/**
 * Zeiterfassung ekrani (`GET /time-entries`).
 *
 * Uc parca: giris/cikis paneli (`TimeTracking.Record`), filtreli sayfali liste
 * ve manuel duzeltme formu (`PUT /time-entries/{id}`). Filtreler URL sorgu
 * parametrelerinde tutulur; masaustunde yogun tablo, mobilde kart listesi —
 * ikisi de ayni signal store'u okur.
 *
 * Sozlesmede tek kayit okuma ucu (`GET /time-entries/{id}`) **yoktur**, bu
 * yuzden duzeltme formu ayri bir rota degil, listedeki satirdan beslenen tek
 * bir paneldir (ayni id'ler DOM'da tekrar etmez).
 */
@Component({
  selector: 'hc-time-tracking',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    TranslatePipe,
    LocalizedDatePipe,
    PageHeader,
    TableShell,
    EmptyState,
    Spinner,
    Button,
    Badge,
    HasPermissionDirective,
  ],
  templateUrl: './time-tracking.html',
})
export class TimeTrackingPage {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly formBuilder = inject(FormBuilder);

  protected readonly store = inject(TimeTrackingStore);
  protected readonly employees = inject(EmployeeOptionsStore);

  protected readonly recordPermission = PERMISSIONS.TimeTrackingRecord;
  protected readonly pageSizeOptions = TIME_ENTRY_PAGE_SIZE_OPTIONS;
  protected readonly limits = TIME_ENTRY_LIMITS;
  protected readonly validationParams = {
    min: TIME_ENTRY_LIMITS.breakMinutesMin,
    max: TIME_ENTRY_LIMITS.breakMinutesMax,
    noteMaxLength: TIME_ENTRY_LIMITS.noteMaxLength,
  };

  /** Duzeltme paneli acik olan kayit. */
  protected readonly editingId = signal<string | null>(null);
  protected readonly pendingDeleteId = signal<string | null>(null);
  protected readonly submitted = signal(false);

  protected readonly editForm = this.formBuilder.nonNullable.group(
    {
      clockIn: ['', [Validators.required]],
      clockOut: [''],
      breakMinutes: [
        '0',
        [
          integerRangeValidator(
            TIME_ENTRY_LIMITS.breakMinutesMin,
            TIME_ENTRY_LIMITS.breakMinutesMax,
          ),
        ],
      ],
      note: ['', [Validators.maxLength(TIME_ENTRY_LIMITS.noteMaxLength)]],
    },
    { validators: [timeEntryConsistencyValidator] },
  );

  private readonly queryParams = toSignal(this.route.queryParamMap, {
    initialValue: convertToParamMap(this.route.snapshot.queryParams),
  });

  private readonly editValue = toSignal(this.editForm.valueChanges, {
    initialValue: this.editForm.getRawValue(),
  });

  /** URL tek dogruluk kaynagi; gecersiz degerler varsayilana duser. */
  protected readonly query = computed(() => parseTimeEntryListQuery(this.queryParams()));

  protected readonly editingEntry = computed(() => {
    const id = this.editingId();
    return id === null ? null : (this.store.items().find((entry) => entry.id === id) ?? null);
  });

  /** Grup dogrulamasi: cikis girise esit veya ondan once. */
  protected readonly clockOrderError = computed(() => {
    void this.editValue();
    return Boolean(this.editForm.errors?.['clockOutOrder']);
  });

  /** Grup dogrulamasi: mola brut sureyi asiyor (mesajda mevcut sure). */
  protected readonly breakExceedsMinutes = computed<number | null>(() => {
    void this.editValue();
    const error = this.editForm.errors?.['breakExceedsWork'] as { minutes: number } | undefined;
    return error ? error.minutes : null;
  });

  protected readonly rowErrorKey = computed(() => {
    const error = this.store.rowError();
    if (!error) {
      return null;
    }
    return error.status === 409 ? 'timeTracking.rowConflict' : error.messageKey;
  });

  /** Giris/cikis hatasi — 409 acik kayit durumuna gore anlamli mesaja cevrilir. */
  protected readonly clockErrorKey = computed(() => {
    const error = this.store.clockError();
    if (!error) {
      return null;
    }
    if (error.status === 409) {
      return this.store.hasOpenEntry()
        ? 'timeTracking.clock.alreadyOpen'
        : 'timeTracking.clock.notOpen';
    }
    return error.messageKey;
  });

  constructor() {
    // Sorgu degistikce liste yenilenir (ilk yukleme dahil).
    effect(() => {
      const query = this.query();
      this.editingId.set(null);
      this.pendingDeleteId.set(null);
      void this.store.load(query);
    });

    void this.employees.load();
  }

  protected applyFromForm(employeeId: string, from: string, to: string): void {
    this.applyFilters({
      employeeId: employeeId || null,
      from: isIsoDate(from) ? from : null,
      to: isIsoDate(to) ? to : null,
    });
  }

  protected onPageSizeChange(event: Event): void {
    const pageSize = parseInteger((event.target as HTMLSelectElement).value);
    if (pageSize !== null) {
      this.applyFilters({ pageSize });
    }
  }

  protected resetFilters(): void {
    void this.navigate({ ...this.query(), page: 1, employeeId: null, from: null, to: null });
  }

  protected goToPage(page: number): void {
    if (page < 1 || page > this.store.totalPages()) {
      return;
    }
    void this.navigate({ ...this.query(), page });
  }

  protected retry(): void {
    void this.store.reload();
  }

  protected onClockEmployeeChange(event: Event): void {
    const value = (event.target as HTMLSelectElement).value;
    void this.store.selectClockEmployee(value || null);
  }

  protected clockIn(note: string): void {
    void this.store.clockIn(note);
  }

  protected clockOut(breakMinutes: string, note: string): void {
    void this.store.clockOut(parseInteger(breakMinutes), note);
  }

  /** Sozlesmede tek kayit okuma ucu yok: form listedeki satirdan doldurulur. */
  protected startEdit(entry: TimeEntryResponse): void {
    this.store.clearRowError();
    this.submitted.set(false);
    this.pendingDeleteId.set(null);
    this.editingId.set(entry.id);
    this.editForm.reset({
      clockIn: toDateTimeLocalValue(entry.clockIn),
      clockOut: toDateTimeLocalValue(entry.clockOut),
      breakMinutes: String(entry.breakMinutes),
      note: entry.note ?? '',
    });
  }

  protected cancelEdit(): void {
    this.editingId.set(null);
    this.submitted.set(false);
  }

  protected async saveEdit(): Promise<void> {
    const entry = this.editingEntry();
    if (!entry) {
      return;
    }
    this.submitted.set(true);
    if (this.editForm.invalid) {
      this.editForm.markAllAsTouched();
      return;
    }

    const raw = this.editForm.getRawValue();
    const clockIn = fromDateTimeLocalValue(raw.clockIn);
    if (clockIn === null) {
      return;
    }
    const request: UpdateTimeEntryRequest = {
      clockIn,
      // Bos birakilirsa kayit acik kalir (sozlesme: `clockOut` nullable).
      clockOut: fromDateTimeLocalValue(raw.clockOut),
      breakMinutes: parseInteger(raw.breakMinutes) ?? 0,
      note: raw.note.trim() || null,
    };

    const error = await this.store.update(entry.id, request);
    if (error === null) {
      this.editingId.set(null);
      this.submitted.set(false);
    } else {
      // Sunucunun alan mesajlari (`BreakMinutes`, `ClockOut`) ilgili alana baglanir.
      applyApiFieldErrors(this.editForm, error);
    }
  }

  protected requestDelete(id: string): void {
    this.store.clearRowError();
    this.editingId.set(null);
    this.pendingDeleteId.set(id);
  }

  protected cancelDelete(): void {
    this.pendingDeleteId.set(null);
  }

  protected async confirmDelete(entry: TimeEntryResponse): Promise<void> {
    const error = await this.store.remove(entry.id);
    if (error === null) {
      this.pendingDeleteId.set(null);
    }
  }

  /** `480` -> `"8:00"`; acik kayitta `null` (sablon "devam ediyor" gosterir). */
  protected worked(entry: TimeEntryResponse): string | null {
    return formatWorkedMinutes(entry.workedMinutes);
  }

  protected errorKeyFor(controlName: TimeEntryFormControl): string | null {
    const control = this.editForm.get(controlName);
    if (!control || control.valid || (!control.touched && !this.submitted())) {
      return null;
    }
    const errors = control.errors ?? {};
    if (errors['required']) {
      return 'timeTracking.form.validation.clockInRequired';
    }
    if (errors['integerFormat']) {
      return 'timeTracking.form.validation.breakFormat';
    }
    if (errors['integerRange']) {
      return 'timeTracking.form.validation.breakRange';
    }
    if (errors['maxlength']) {
      return 'timeTracking.form.validation.noteLength';
    }
    return null;
  }

  protected serverMessagesFor(controlName: TimeEntryFormControl): readonly string[] {
    return serverErrorMessages(this.editForm.get(controlName));
  }

  private applyFilters(changes: Partial<Omit<TimeEntryListQuery, 'page'>>): void {
    void this.navigate(withTimeEntryFilterChange(this.query(), changes));
  }

  private navigate(query: TimeEntryListQuery): Promise<boolean> {
    return this.router.navigate([], {
      relativeTo: this.route,
      queryParams: timeEntryListQueryToParams(query),
    });
  }
}
