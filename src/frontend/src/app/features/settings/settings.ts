import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import {
  FormBuilder,
  FormGroup,
  ReactiveFormsModule,
  Validators,
  type AbstractControl,
} from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';

import { SettingsApi } from '../../core/api/settings.api';
import { toApiError } from '../../core/interceptors/problem-details.mapper';
import { SUPPORTED_LANGUAGES, type AppLanguage } from '@hotelcore/shared';
import { PERMISSIONS } from '../../core/models/permission.model';
import type { ApiError } from '../../core/models/problem-details.model';
import {
  COUNTRIES,
  SETTINGS_LIMITS,
  type HeadOfficeSettingsResponse,
  type HotelListItemResponse,
  type HotelResponse,
} from '../../core/models/settings.model';
import { AuthStore } from '../../core/state/auth.store';
import { LanguagePicker } from '../../layout/language-picker/language-picker';
import { applyApiFieldErrors, serverErrorMessages } from '../../shared/forms/api-field-errors';
import { decimalRangeValidator, parseDecimal } from '../../shared/forms/numeric-validators';
import { Button } from '../../shared/ui/button/button';
import { Card } from '../../shared/ui/card/card';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { Spinner } from '../../shared/ui/spinner/spinner';

/** Ekrandaki iki bagimsiz form. */
type SettingsFormName = 'hotel' | 'headOffice';

/**
 * Ayarlar ekrani — dil tercihi, otel kunyesi + vergi profili, marka ayarlari.
 *
 * IKI KATMANLI EKRAN:
 * 1. **Darstellung / Gorunum** karti (arayuz dili) — kisisel tercih, izin
 *    gerektirmez, oturum acmis herkese gorunur.
 * 2. **Otel kunyesi + Head Office** kartlari — `Settings.Manage` gerektirir.
 *
 * IZINSIZ KULLANICIDA KARTLAR **GIZLENIR** (salt-okunur gosterilmez): salt-okunur
 * bir kart yine de `GET /hotels` ve `GET /head-office/settings` verisine
 * ihtiyac duyardi, sunucu ise bu uclari ayni izinle koruyor — yani ekranda bos
 * ya da kirik bir kart kalirdi. Bu yuzden izin yoksa **istek de atilmaz**.
 *
 * Bu istemci tarafi ayrim bir **guvenlik siniri degildir**, yalnizca gurultu
 * azaltmadir: kaydetme uclarinin yetki denetimi sunucuda oldugu gibi durur.
 *
 * Otel listesi `GET /hotels` ile gelir (JWT'deki otel listesi degil): erisim
 * `UserHotelAccess` tablosundan dogrulanir, boylece erisim iptali token suresinin
 * bitmesini beklemez. Birden fazla otele yetkili kullanici hangi otelin ayarlarini
 * duzenledigini secer.
 *
 * **Vergi oranlari koda hardcode edilmez** (architecture.md §4.1); faturalama bu
 * ekranda yonetilen degerleri okur.
 */
@Component({
  selector: 'hc-settings',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, TranslatePipe, PageHeader, Card, Button, Spinner, LanguagePicker],
  templateUrl: './settings.html',
})
export class SettingsPage {
  private readonly api = inject(SettingsApi);
  private readonly formBuilder = inject(FormBuilder);
  private readonly authStore = inject(AuthStore);

  /** Yonetim kartlari (otel kunyesi, vergi profili, Head Office) gorunur mu. */
  protected readonly canManageSettings = computed(() =>
    this.authStore.hasPermission(PERMISSIONS.SettingsManage),
  );

  protected readonly languages = SUPPORTED_LANGUAGES;
  protected readonly countries = COUNTRIES;
  protected readonly limits = SETTINGS_LIMITS;

  protected readonly loading = signal(true);
  protected readonly loadError = signal<ApiError | null>(null);
  protected readonly hotels = signal<readonly HotelListItemResponse[]>([]);
  protected readonly selectedHotelId = signal<string | null>(null);

  protected readonly hotelSaving = signal(false);
  protected readonly hotelSaved = signal(false);
  protected readonly hotelFormErrors = signal<readonly string[]>([]);

  protected readonly headOfficeSaving = signal(false);
  protected readonly headOfficeSaved = signal(false);
  protected readonly headOffice = signal<HeadOfficeSettingsResponse | null>(null);
  protected readonly headOfficeFormErrors = signal<readonly string[]>([]);
  /** `Settings.Manage` var ama kimlikte Head Office yoksa sunucu 403 doner. */
  protected readonly headOfficeUnavailable = signal(false);

  protected readonly hotelForm = this.formBuilder.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(SETTINGS_LIMITS.nameMaxLength)]],
    country: ['DE', [Validators.required]],
    city: ['', [Validators.required, Validators.maxLength(SETTINGS_LIMITS.cityMaxLength)]],
    addressLine: ['', [Validators.maxLength(SETTINGS_LIMITS.addressMaxLength)]],
    postalCode: ['', [Validators.maxLength(SETTINGS_LIMITS.postalCodeMaxLength)]],
    phone: ['', [Validators.maxLength(SETTINGS_LIMITS.phoneMaxLength)]],
    email: ['', [Validators.email, Validators.maxLength(SETTINGS_LIMITS.emailMaxLength)]],
    taxNumber: ['', [Validators.maxLength(SETTINGS_LIMITS.taxNumberMaxLength)]],
    defaultCulture: ['de' as AppLanguage, [Validators.required]],
    currency: ['EUR', [Validators.required, Validators.pattern(/^[A-Za-z]{3}$/)]],
    vatRate: [
      '',
      [Validators.required, decimalRangeValidator({ min: 0, max: SETTINGS_LIMITS.ratePercentMax })],
    ],
    reducedVatRate: [
      '',
      [Validators.required, decimalRangeValidator({ min: 0, max: SETTINGS_LIMITS.ratePercentMax })],
    ],
    cityTaxPerPersonNight: ['', [Validators.required, decimalRangeValidator({ min: 0 })]],
    cityTaxEnabled: [false],
  });

  protected readonly headOfficeForm = this.formBuilder.nonNullable.group({
    brandName: [
      '',
      [Validators.required, Validators.maxLength(SETTINGS_LIMITS.brandNameMaxLength)],
    ],
    defaultCulture: ['de' as AppLanguage, [Validators.required]],
  });

  /** Otel secici yalnizca birden fazla otele yetkili kullanicida anlamlidir. */
  protected readonly canSwitchHotel = computed(() => this.hotels().length > 1);

  constructor() {
    void this.load();
  }

  protected async load(): Promise<void> {
    // Izin yoksa yonetim kartlari hic cizilmez; bos yere 403 toplamayalim.
    if (!this.canManageSettings()) {
      this.loading.set(false);
      return;
    }

    this.loading.set(true);
    this.loadError.set(null);

    try {
      const [hotels, headOffice] = await Promise.all([
        firstValueFrom(this.api.listHotels()),
        this.loadHeadOffice(),
      ]);

      this.hotels.set(hotels);
      this.headOffice.set(headOffice);

      if (headOffice) {
        this.headOfficeForm.reset({
          brandName: headOffice.brandName,
          defaultCulture: this.asLanguage(headOffice.defaultCulture),
        });
      }

      const first = hotels[0];
      if (first) {
        await this.selectHotel(first.id);
      }
    } catch (error) {
      this.loadError.set(toApiError(error));
    } finally {
      this.loading.set(false);
    }
  }

  protected async selectHotel(id: string): Promise<void> {
    this.selectedHotelId.set(id);
    this.hotelSaved.set(false);
    this.hotelFormErrors.set([]);

    try {
      const hotel = await firstValueFrom(this.api.getHotel(id));
      this.patchHotelForm(hotel);
    } catch (error) {
      this.loadError.set(toApiError(error));
    }
  }

  protected async saveHotel(): Promise<void> {
    const id = this.selectedHotelId();
    if (id === null || this.hotelSaving()) {
      return;
    }

    this.hotelForm.markAllAsTouched();
    if (this.hotelForm.invalid) {
      return;
    }

    this.hotelSaving.set(true);
    this.hotelSaved.set(false);
    this.hotelFormErrors.set([]);

    const value = this.hotelForm.getRawValue();

    try {
      const updated = await firstValueFrom(
        this.api.updateHotel(id, {
          name: value.name.trim(),
          country: value.country,
          city: value.city.trim(),
          addressLine: this.orNull(value.addressLine),
          postalCode: this.orNull(value.postalCode),
          phone: this.orNull(value.phone),
          email: this.orNull(value.email),
          taxNumber: this.orNull(value.taxNumber),
          defaultCulture: value.defaultCulture,
          currency: value.currency.trim().toUpperCase(),
          taxProfile: {
            vatRate: parseDecimal(value.vatRate) ?? 0,
            reducedVatRate: parseDecimal(value.reducedVatRate) ?? 0,
            cityTaxPerPersonNight: parseDecimal(value.cityTaxPerPersonNight) ?? 0,
            cityTaxEnabled: value.cityTaxEnabled,
          },
        }),
      );

      this.patchHotelForm(updated);
      // Liste satirindaki ad/sehir de degismis olabilir; taze tut.
      this.hotels.set(await firstValueFrom(this.api.listHotels()));
      this.hotelSaved.set(true);
    } catch (error) {
      this.hotelFormErrors.set(applyApiFieldErrors(this.hotelForm, toApiError(error)));
    } finally {
      this.hotelSaving.set(false);
    }
  }

  protected async saveHeadOffice(): Promise<void> {
    if (this.headOfficeSaving()) {
      return;
    }

    this.headOfficeForm.markAllAsTouched();
    if (this.headOfficeForm.invalid) {
      return;
    }

    this.headOfficeSaving.set(true);
    this.headOfficeSaved.set(false);
    this.headOfficeFormErrors.set([]);

    const value = this.headOfficeForm.getRawValue();

    try {
      const updated = await firstValueFrom(
        this.api.updateHeadOffice({
          brandName: value.brandName.trim(),
          defaultCulture: value.defaultCulture,
        }),
      );

      this.headOffice.set(updated);
      this.headOfficeSaved.set(true);
    } catch (error) {
      this.headOfficeFormErrors.set(applyApiFieldErrors(this.headOfficeForm, toApiError(error)));
    } finally {
      this.headOfficeSaving.set(false);
    }
  }

  protected serverErrors(form: SettingsFormName, field: string): readonly string[] {
    return serverErrorMessages(this.control(form, field));
  }

  protected invalid(form: SettingsFormName, field: string): boolean {
    const control = this.control(form, field);

    return control !== null && control.invalid && control.touched;
  }

  /**
   * Iki form da guclu tipli oldugu icin birlesim tipi uzerinde `get` cagrilamaz
   * (asiri yuklemeler uyusmaz); once `FormGroup` tipine genisletilir.
   */
  private control(form: SettingsFormName, field: string): AbstractControl | null {
    const group: FormGroup = form === 'hotel' ? this.hotelForm : this.headOfficeForm;

    return group.get(field);
  }

  private patchHotelForm(hotel: HotelResponse): void {
    this.hotelForm.reset({
      name: hotel.name,
      country: hotel.country,
      city: hotel.city,
      addressLine: hotel.addressLine ?? '',
      postalCode: hotel.postalCode ?? '',
      phone: hotel.phone ?? '',
      email: hotel.email ?? '',
      taxNumber: hotel.taxNumber ?? '',
      defaultCulture: this.asLanguage(hotel.defaultCulture),
      currency: hotel.currency,
      vatRate: String(hotel.taxProfile.vatRate),
      reducedVatRate: String(hotel.taxProfile.reducedVatRate),
      cityTaxPerPersonNight: String(hotel.taxProfile.cityTaxPerPersonNight),
      cityTaxEnabled: hotel.taxProfile.cityTaxEnabled,
    });
  }

  /**
   * Head Office ayarlari `Settings.Manage` gerektirir; kimlikte Head Office yoksa
   * sunucu 403 doner. Bu, ekranin geri kalanini (otel ayarlari) bozmamali.
   */
  private async loadHeadOffice(): Promise<HeadOfficeSettingsResponse | null> {
    try {
      return await firstValueFrom(this.api.getHeadOffice());
    } catch (error) {
      const apiError = toApiError(error);
      if (apiError.status === 403 || apiError.status === 404) {
        this.headOfficeUnavailable.set(true);
        return null;
      }

      throw error;
    }
  }

  private asLanguage(culture: string): AppLanguage {
    const normalized = culture.slice(0, 2).toLowerCase();

    return (SUPPORTED_LANGUAGES as readonly string[]).includes(normalized)
      ? (normalized as AppLanguage)
      : 'de';
  }

  private orNull(value: string): string | null {
    const trimmed = value.trim();

    return trimmed.length === 0 ? null : trimmed;
  }
}
