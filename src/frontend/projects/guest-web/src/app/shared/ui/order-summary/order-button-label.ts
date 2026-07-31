import type { AppLanguage } from '@hotelcore/shared';

import type { PublicOrderButton } from '../../../core/api/public-models';

/**
 * ===========================================================================
 * §312j Abs. 3 BGB — "Button-Losung"
 * ===========================================================================
 *
 * Almanca arayuzde sipariş dugmesinin uzerinde **tam olarak**
 * `zahlungspflichtig buchen` (veya esdeger, acikca ucretli oldugunu bildiren)
 * bir ifade bulunmak zorundadir. Aksi halde sozlesme kurulmaz (§312j Abs. 4).
 *
 * BU DOSYADA METIN YOKTUR — VE OLMAYACAKTIR.
 * Etiket sunucudan gelir (`legal.orderButton.labelDe`) ve rezervasyon
 * isteginde `checkout.orderButtonLabel` olarak **geri gonderilip donduruLUR**
 * (kanit kaydi, mimari §9.1). Metni istemcide sabitlemek iki seyi bozar:
 *   1) Otel/hukuk ekibi ifadeyi degistirdiginde iki yerde degistirmek gerekir;
 *      biri unutulursa **gosterilen** metin ile **kaydedilen** metin ayrisir ve
 *      kanit degerini yitirir.
 *   2) Sunucu metni dogrulayamaz, yalnizca dondurur; tek dogru kaynak odur.
 *
 * DIL KURALI:
 *   - `de`  -> `labelDe` **birebir**. Ceviri katmanina hic ugramaz.
 *   - `en` / `tr` -> `labelKey` uzerinden yerellestirilmis karsilik; bu metin de
 *     odeme yukumlulugunu **acikca** bildirmek zorundadir (bkz. i18n).
 *     Anahtar bulunamazsa `labelDe`'ye duser — bilinmeyen bir metin uydurmaktansa
 *     Almanca hukuki ifadeyi gostermek dogrudur.
 */
export function resolveOrderButtonLabel(
  button: PublicOrderButton,
  language: AppLanguage,
  translate: (key: string) => string,
): string {
  if (language === 'de') {
    return button.labelDe;
  }

  const translated = translate(button.labelKey);
  const usable =
    typeof translated === 'string' && translated.length > 0 && translated !== button.labelKey;

  return usable ? translated : button.labelDe;
}
