/**
 * Personel modulu tipleri (api-contracts.md → "Personel (Employees & Departments)").
 *
 * Departman tipleri de burada durur: departman bagimsiz bir modul degil,
 * calisan kaydinin siniflandirmasidir ve ayni ekran ailesinde kullanilir.
 */

/**
 * Calisma sekli — backend enum'unun **adi** (sayi degil) tasinir.
 * Swagger: `EmploymentType` = FullTime | PartTime | MiniJob | Apprentice | Seasonal | Temporary.
 */
export const EMPLOYMENT_TYPES = [
  'FullTime',
  'PartTime',
  'MiniJob',
  'Apprentice',
  'Seasonal',
  'Temporary',
] as const;

export type EmploymentType = (typeof EMPLOYMENT_TYPES)[number];

export function isEmploymentType(value: unknown): value is EmploymentType {
  return typeof value === 'string' && (EMPLOYMENT_TYPES as readonly string[]).includes(value);
}

/** Calisma sekli -> i18n anahtari (`employees.employmentType.*`). */
export const EMPLOYMENT_TYPE_LABEL_KEYS: Readonly<Record<EmploymentType, string>> = {
  FullTime: 'employees.employmentType.fullTime',
  PartTime: 'employees.employmentType.partTime',
  MiniJob: 'employees.employmentType.miniJob',
  Apprentice: 'employees.employmentType.apprentice',
  Seasonal: 'employees.employmentType.seasonal',
  Temporary: 'employees.employmentType.temporary',
};

/**
 * `EmployeeResponse` — api-contracts.md / Personel.
 * `hiredOn` / `terminatedOn` **tarih** (saat yok): `"2024-03-01"`.
 */
export interface EmployeeResponse {
  readonly id: string;
  readonly firstName: string;
  readonly lastName: string;
  /** Sunucuda uretilir; istemci ad/soyadi kendisi birlestirmez. */
  readonly fullName: string;
  readonly email?: string | null;
  readonly phone?: string | null;
  readonly staffNumber?: string | null;
  readonly departmentId: string;
  readonly departmentName: string;
  readonly employmentType: EmploymentType;
  readonly annualLeaveDays: number;
  readonly hiredOn: string;
  readonly terminatedOn?: string | null;
  /** `terminatedOn` yok veya gelecekte — sunucu hesaplar. */
  readonly isActive: boolean;
  /** Login iliskisi; personel ekraninda yalnizca bilgi amaclidir. */
  readonly userId?: string | null;
}

/**
 * `GET /employees` filtreleri:
 * `?page=1&pageSize=20&departmentId=&employmentType=&search=&includeTerminated=false`
 * (`search` → ad, soyad ve personel numarasinda contains, buyuk/kucuk harf duyarsiz).
 */
export interface EmployeeListQuery {
  readonly page: number;
  readonly pageSize: number;
  readonly departmentId?: string | null;
  readonly employmentType?: EmploymentType | null;
  readonly search?: string | null;
  /** Varsayilan `false`: isten ayrilmislar listelenmez. */
  readonly includeTerminated: boolean;
}

/** `POST /employees` ve `PUT /employees/{id}` govdesi (alanlar birebir ayni). */
export interface EmployeeWriteRequest {
  readonly firstName: string;
  readonly lastName: string;
  readonly email?: string | null;
  readonly phone?: string | null;
  readonly staffNumber?: string | null;
  readonly departmentId: string;
  readonly employmentType: EmploymentType;
  readonly annualLeaveDays: number;
  readonly hiredOn: string;
  readonly terminatedOn?: string | null;
}

export type CreateEmployeeRequest = EmployeeWriteRequest;
export type UpdateEmployeeRequest = EmployeeWriteRequest;

/** `DepartmentResponse` — sayfalama yok, duz dizi doner. */
export interface DepartmentResponse {
  readonly id: string;
  readonly name: string;
  readonly description?: string | null;
  /** Silme onayinda uyari icin: bagli calisan varsa silme 409 doner. */
  readonly employeeCount: number;
}

/** `POST /departments` ve `PUT /departments/{id}` govdesi. */
export interface DepartmentWriteRequest {
  readonly name: string;
  readonly description?: string | null;
}

export type CreateDepartmentRequest = DepartmentWriteRequest;
export type UpdateDepartmentRequest = DepartmentWriteRequest;

/**
 * Sozlesmedeki dogrulama kurallari (400 + `errors`) — istemcide de birebir
 * uygulanir, ancak son soz backend'dedir.
 *
 * `emailMaxLength` sozlesme metninde "gecerli ≤ 200" olarak yazilidir;
 * geri kalan sinirlar dogrudan sozlesmeden gelir.
 */
export const EMPLOYEE_LIMITS = {
  firstNameMaxLength: 100,
  lastNameMaxLength: 100,
  emailMaxLength: 200,
  phoneMaxLength: 50,
  staffNumberMaxLength: 20,
  annualLeaveDaysMin: 0,
  annualLeaveDaysMax: 60,
} as const;

export const DEPARTMENT_LIMITS = {
  nameMaxLength: 100,
  descriptionMaxLength: 500,
} as const;
