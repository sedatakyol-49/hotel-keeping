import type { AppLanguage } from '@hotelcore/shared';

/**
 * Faturalama (Rechnung, GoBD) tipleri
 * (docs/api-contracts-invoices.md).
 *
 * Iki degismez kural istemciyi bicimlendirir:
 * 1. **Yalnizca `Draft` duzenlenebilir.** Kesinlesmis fatura hicbir yoldan
 *    degistirilemez; duzeltme yalnizca iptal faturasi (Stornorechnung) iledir.
 * 2. **Tutarlari sunucu hesaplar.** Istemci `vatRate`, `lineNet`, `lineVat`
 *    veya fatura toplami **gonderemez** — bu alanlar yazma sozlesmesinde yoktur.
 */

/** Fatura durumu — backend enum'unun **adi**. */
export const INVOICE_STATUSES = ['Draft', 'Finalized', 'Paid', 'Cancelled'] as const;

export type InvoiceStatus = (typeof INVOICE_STATUSES)[number];

export function isInvoiceStatus(value: unknown): value is InvoiceStatus {
  return typeof value === 'string' && (INVOICE_STATUSES as readonly string[]).includes(value);
}

export const INVOICE_STATUS_LABEL_KEYS: Readonly<Record<InvoiceStatus, string>> = {
  Draft: 'invoices.status.draft',
  Finalized: 'invoices.status.finalized',
  Paid: 'invoices.status.paid',
  Cancelled: 'invoices.status.cancelled',
};

/** Fatura satir tipi. KDV orani **sunucu** belirler (istemci gonderemez). */
export const INVOICE_LINE_TYPES = ['RoomCharge', 'Extra', 'CityTax'] as const;

export type InvoiceLineType = (typeof INVOICE_LINE_TYPES)[number];

export function isInvoiceLineType(value: unknown): value is InvoiceLineType {
  return typeof value === 'string' && (INVOICE_LINE_TYPES as readonly string[]).includes(value);
}

export const INVOICE_LINE_TYPE_LABEL_KEYS: Readonly<Record<InvoiceLineType, string>> = {
  RoomCharge: 'invoices.lineType.roomCharge',
  Extra: 'invoices.lineType.extra',
  CityTax: 'invoices.lineType.cityTax',
};

/** Odeme yontemi. */
export const PAYMENT_METHODS = ['Cash', 'Card', 'Transfer'] as const;

export type PaymentMethod = (typeof PAYMENT_METHODS)[number];

export function isPaymentMethod(value: unknown): value is PaymentMethod {
  return typeof value === 'string' && (PAYMENT_METHODS as readonly string[]).includes(value);
}

export const PAYMENT_METHOD_LABEL_KEYS: Readonly<Record<PaymentMethod, string>> = {
  Cash: 'invoices.paymentMethod.cash',
  Card: 'invoices.paymentMethod.card',
  Transfer: 'invoices.paymentMethod.transfer',
};

/**
 * Denetim izi eylemi (GoBD §6.3, append-only).
 *
 * Sozlesmede belirtilen **bilinen sinir**: Domain'de `Updated` enum degeri
 * yoktur, taslak guncellemesi denetim izine yazilmaz. Yine de sunucunun
 * ileride ekleyebilecegi bir deger ekrani kirmasin diye tip listede tutulur ve
 * bilinmeyen eylem icin genel bir etiket kullanilir.
 */
export const INVOICE_AUDIT_ACTIONS = [
  'Created',
  'Finalized',
  'Paid',
  'Cancelled',
  'Updated',
  'PaymentRecorded',
] as const;

export type InvoiceAuditAction = (typeof INVOICE_AUDIT_ACTIONS)[number];

export const INVOICE_AUDIT_ACTION_LABEL_KEYS: Readonly<Record<InvoiceAuditAction, string>> = {
  Created: 'invoices.audit.action.created',
  Finalized: 'invoices.audit.action.finalized',
  Paid: 'invoices.audit.action.paid',
  Cancelled: 'invoices.audit.action.cancelled',
  Updated: 'invoices.audit.action.updated',
  PaymentRecorded: 'invoices.audit.action.paymentRecorded',
};

/** Bilinmeyen eylem adinda ham deger gosterilir (uydurma etiket uretilmez). */
export function auditActionLabelKey(action: string): string | null {
  return (INVOICE_AUDIT_ACTION_LABEL_KEYS as Readonly<Record<string, string>>)[action] ?? null;
}

/** `InvoiceResponse` — liste satiri ve detay ortak govdesi. */
export interface InvoiceResponse {
  readonly id: string;
  /** **Taslakta `null`** — numara yalnizca finalize aninda atanir. */
  readonly invoiceNumber?: string | null;
  readonly status: InvoiceStatus;
  /** Taslakta `null`. */
  readonly issuedAt?: string | null;
  readonly guestId: string;
  readonly guestName: string;
  readonly reservationId?: string | null;
  readonly reservationNumber?: string | null;
  readonly culture: string;
  readonly currency: string;
  /** KDV'li satirlarin net toplami — **Kurtaxe haric**. */
  readonly netAmount: number;
  readonly vatAmount: number;
  /** Kurtaxe — KDV matrahina dahil **degil**. */
  readonly cityTaxAmount: number;
  /** net + KDV + Kurtaxe. */
  readonly grossAmount: number;
  readonly paidAmount: number;
  readonly outstandingAmount: number;
  /** Bu faturayi iptal eden Stornorechnung. */
  readonly cancelledByInvoiceId?: string | null;
  /** Bu fatura bir storno ise iptal ettigi fatura. */
  readonly cancelsInvoiceId?: string | null;
  readonly isCancellationInvoice: boolean;
  readonly createdAt: string;
}

export interface InvoiceLineItemResponse {
  readonly id: string;
  readonly type: InvoiceLineType;
  readonly description: string;
  readonly quantity: number;
  /** **BRUT** birim fiyat (KDV dahil). */
  readonly unitPrice: number;
  /** Sunucu belirler; istemci gonderemez. */
  readonly vatRate: number;
  readonly lineNet: number;
  readonly lineVat: number;
  readonly lineGross: number;
  /** Leistungsdatum (GoBD). */
  readonly serviceDate: string;
  readonly sortOrder: number;
}

export interface InvoicePaymentResponse {
  readonly id: string;
  readonly method: PaymentMethod;
  readonly amount: number;
  readonly paidAt: string;
  readonly reference?: string | null;
}

export interface InvoiceAuditEntryResponse {
  readonly id: string;
  readonly action: string;
  readonly performedByUserId?: string | null;
  readonly performedAt: string;
  /** JSON metni — ham gosterilir, istemci yorumlamaz. */
  readonly details?: string | null;
}

/** `GET /invoices/{id}` — liste alanlari + uc koleksiyon. */
export interface InvoiceDetailResponse extends InvoiceResponse {
  readonly lineItems: readonly InvoiceLineItemResponse[];
  readonly payments: readonly InvoicePaymentResponse[];
  /** Append-only, **en eskiden yeniye**. */
  readonly auditTrail: readonly InvoiceAuditEntryResponse[];
}

/**
 * `GET /invoices` filtreleri.
 *
 * `from`/`to` **`issuedAt`** uzerinde gun bazli, **her iki uc dahil** araliktir
 * ve tarih filtresi verildiginde **taslaklar listelenmez** (taslagin fatura
 * tarihi yoktur). Ekran bunu kullaniciya bilgi metniyle soyler.
 */
export interface InvoiceListQuery {
  readonly page: number;
  readonly pageSize: number;
  readonly status?: InvoiceStatus | null;
  readonly guestId?: string | null;
  readonly reservationId?: string | null;
  readonly from?: string | null;
  readonly to?: string | null;
  readonly search?: string | null;
}

/**
 * Yazma satiri — istemci **yalnizca** bu dort alani gonderir.
 * `vatRate`/`lineNet`/`lineVat` bilincli olarak yoktur (vergi matrahi
 * manipule edilemez); negatif miktar/fiyat kabul edilmez (eksi tutari yalnizca
 * sunucu storno icin uretir).
 */
export interface InvoiceLineItemRequest {
  readonly type: InvoiceLineType;
  readonly description: string;
  readonly quantity: number;
  /** BRUT birim fiyat (KDV dahil). */
  readonly unitPrice: number;
  /** Verilmezse fatura gunu kullanilir. */
  readonly serviceDate?: string | null;
}

/**
 * `POST /invoices` — **YOL A**: rezervasyondan uretim.
 * `lineItems` gonderilirse sunucu 400 doner (iki yol birbirini disler).
 */
export interface CreateInvoiceFromReservationRequest {
  readonly reservationId: string;
  readonly culture?: AppLanguage | null;
}

/** `POST /invoices` — **YOL B**: elle satir girisi. `guestId` zorunludur. */
export interface CreateInvoiceManualRequest {
  readonly guestId: string;
  readonly culture?: AppLanguage | null;
  readonly lineItems: readonly InvoiceLineItemRequest[];
}

export type CreateInvoiceRequest =
  | CreateInvoiceFromReservationRequest
  | CreateInvoiceManualRequest;

/**
 * `PUT /invoices/{id}` — yalnizca `Draft`; satirlar **tamamen** degistirilir.
 * `guestId`/`culture` `null`/eksik ise degismez. Rezervasyona bagli faturada
 * misafir degistirilemez (sunucu 409).
 */
export interface UpdateInvoiceRequest {
  readonly guestId?: string | null;
  readonly culture?: AppLanguage | null;
  readonly lineItems: readonly InvoiceLineItemRequest[];
}

/** `POST /invoices/{id}/cancel` — govde ve alan opsiyoneldir. */
export interface CancelInvoiceRequest {
  readonly reason?: string | null;
}

/** `POST /invoices/{id}/payments` → 200 + `InvoiceDetailResponse`. */
export interface RecordPaymentRequest {
  readonly method: PaymentMethod;
  readonly amount: number;
  /** Opsiyonel; verilmezse sunucu saati. **Gelecek tarih 400 doner.** */
  readonly paidAt?: string | null;
  readonly reference?: string | null;
}

// --- Durum makinesi turevleri (kullaniciya yasak yol gosterilmez) ----------

/** Yalnizca taslak duzenlenebilir (GoBD §6.1). */
export function isEditableInvoice(status: InvoiceStatus): boolean {
  return status === 'Draft';
}

/** Finalize yalnizca taslakta anlamlidir. */
export function canFinalizeInvoice(status: InvoiceStatus): boolean {
  return status === 'Draft';
}

/** Zaten iptal edilmis fatura ikinci kez iptal edilemez (409). */
export function canCancelInvoice(status: InvoiceStatus): boolean {
  return status !== 'Cancelled';
}

/**
 * Kesinlesmis/odenmis faturanin iptali **yeni bir Stornorechnung** uretir ve
 * orijinal korunur; taslak iptalinde storno **uretilmez** (numarasi olmayan
 * taslak belge degildir). Onay metni bu ayrima gore dallanir.
 */
export function producesCancellationInvoice(status: InvoiceStatus): boolean {
  return status === 'Finalized' || status === 'Paid';
}

/**
 * Odeme **yalnizca `Finalized`** faturaya kaydedilir: `Draft` → 409
 * ("once finalize"), `Cancelled` → 409, tamamen odenmis → 409. Brut tutari
 * `<= 0` olan belgeye (Stornorechnung) odeme kaydedilemez — iade akisi yok.
 */
export function canRecordPayment(invoice: Pick<InvoiceResponse, 'status' | 'grossAmount'>): boolean {
  return invoice.status === 'Finalized' && invoice.grossAmount > 0;
}

/** Sozlesmedeki dogrulama sinirlari (400 + `errors`). */
export const INVOICE_LIMITS = {
  lineItemsMin: 1,
  lineItemsMax: 200,
  descriptionMaxLength: 500,
  quantityMin: 0.01,
  quantityMax: 9999,
  unitPriceMin: 0,
  unitPriceMax: 1_000_000,
  amountMin: 0.01,
  amountMax: 1_000_000,
  referenceMaxLength: 128,
  reasonMaxLength: 500,
} as const;
