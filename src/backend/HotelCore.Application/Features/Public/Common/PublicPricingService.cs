using HotelCore.Application.Features.Invoices.Common;
using HotelCore.Application.Features.Reservations.Common;
using HotelCore.Domain.Entities;
using HotelCore.Domain.Enums;

namespace HotelCore.Application.Features.Public.Common;

/// <summary>
/// Misafire gösterilen PAngV fiyat nesnesini üretir.
///
/// <para><b>İkinci bir fiyat motoru YOKTUR</b> (architecture-public-booking.md §8). Her parça
/// mevcut sahibinden gelir:
/// <list type="bullet">
///   <item>gece gece konaklama fiyatı, sezon geçişi, kanal önceliği →
///   <see cref="ReservationPricingService.CalculateForRoomTypeAsync"/>,</item>
///   <item>net/KDV ayrıştırma ve yuvarlama → <see cref="InvoiceAmounts.ComputeLine"/> (faturanın
///   kullandığı <b>aynı</b> matematik),</item>
///   <item>KDV oranı eşlemesi → <see cref="InvoiceAmounts.ResolveVatRate"/> (konaklama =
///   indirimli oran),</item>
///   <item>Kurtaxe'ye tabi kişi sayısı → <see cref="TaxProfile.CountTaxablePersons"/> (çocuk
///   muafiyeti dâhil),</item>
///   <item>Kurtaxe doğar mı → <see cref="CityTaxLiability.ArisesFrom"/>.</item>
/// </list></para>
///
/// <para><b>Fatura ile uzlaşma neden kuruşu kuruşuna tutar:</b> faturadaki konaklama satırı
/// <c>ApplyLineAmountsFromGross(brüt)</c> ile yazılır, yani <b>brüt toplam otoriterdir</b> ve
/// buradaki <c>ComputeLine(1, brüt, oran)</c> ile birebir aynı hesabı yapar. Kurtaxe satırı
/// faturada <c>miktar × birim fiyat</c> olarak hesaplanır; burada da <b>aynı</b> çarpım ve aynı
/// yuvarlama uygulanır — <c>taxablePersons × nights</c> adet × <c>Round(perPersonNight)</c>.
/// Toplam fatura brütü = <c>net + KDV + Kurtaxe</c> = <c>accommodationGross + cityTax</c> =
/// <c>totalGross</c>.</para>
/// </summary>
internal sealed class PublicPricingService(ReservationPricingService pricing)
{
    /// <summary>Public kanalın fiyatlandığı rezervasyon kanalı.</summary>
    public const ReservationChannel Channel = ReservationChannel.Website;

    /// <summary>
    /// Oda tipi + tarih + kişi sayısı için PAngV fiyat nesnesini üretir.
    /// </summary>
    public async Task<PublicPriceResponse> BuildAsync(
        PublicHotelContext hotel,
        Guid roomTypeId,
        decimal basePrice,
        DateOnly checkIn,
        DateOnly checkOut,
        int adults,
        int children,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(hotel);

        var pricingResult = await pricing
            .CalculateForRoomTypeAsync(roomTypeId, basePrice, checkIn, checkOut, Channel, cancellationToken)
            .ConfigureAwait(false);

        return Compose(hotel, pricingResult, adults, children);
    }

    /// <summary>Hesaplanmış konaklama tutarından fiyat nesnesini kurar (test edilebilir çekirdek).</summary>
    public static PublicPriceResponse Compose(
        PublicHotelContext hotel,
        ReservationPricing pricingResult,
        int adults,
        int children)
    {
        ArgumentNullException.ThrowIfNull(hotel);
        ArgumentNullException.ThrowIfNull(pricingResult);

        var taxProfile = hotel.Hotel.TaxProfile;
        var tax = ToTaxContext(hotel);

        // KDV oranı ISTEMCIDEN alınmaz ve burada yorumlanmaz: satır türünden (konaklama)
        // otelin vergi profiline göre çözülür — faturanın kullandığı aynı eşleme.
        var accommodationVatRate = InvoiceAmounts.ResolveVatRate(InvoiceLineType.RoomCharge, tax);

        // Miktar 1 × brüt toplam: faturadaki ApplyLineAmountsFromGross ile AYNI hesap.
        // (Brüt toplam otoriterdir; gecelik birim fiyat yalnızca gösterim ortalamasıdır.)
        var accommodation = InvoiceAmounts.ComputeLine(1m, pricingResult.TotalAmount, accommodationVatRate);

        var taxablePersons = taxProfile.CountTaxablePersons(adults, children);
        var cityTaxApplies = taxProfile.CityTaxEnabled
                             && taxProfile.CityTaxPerPersonNight > 0m
                             && taxablePersons > 0;

        // Faturadaki Kurtaxe satırının birebir aynısı: miktar = kişi × gece, birim fiyat
        // yuvarlanmış gecelik tutar, KDV %0 (durchlaufender Posten).
        var cityTaxAmount = cityTaxApplies
            ? InvoiceAmounts.ComputeLine(
                taxablePersons * pricingResult.Nights,
                InvoiceAmounts.Round(taxProfile.CityTaxPerPersonNight),
                0m).Gross
            : 0m;

        var totalGross = accommodation.Gross + cityTaxAmount;

        return new PublicPriceResponse
        {
            Currency = hotel.Hotel.Currency,
            TotalGross = totalGross,
            VatIncluded = true,
            MandatoryExtrasIncluded = true,
            AccommodationGross = accommodation.Gross,
            AccommodationNet = accommodation.Net,
            AccommodationVat = accommodation.Vat,
            AccommodationVatRate = accommodationVatRate,
            CityTax = new PublicCityTaxResponse
            {
                Applies = cityTaxApplies,
                Amount = cityTaxAmount,
                PerPersonNight = InvoiceAmounts.Round(taxProfile.CityTaxPerPersonNight),
                TaxablePersons = cityTaxApplies ? taxablePersons : 0,
                Nights = pricingResult.Nights,
                VatRate = 0m,
                IncludedInTotal = true,

                // İptal/no-show'da Kurtaxe DOĞMAZ — kural tek yerdedir (CityTaxLiability) ve
                // burada yeniden yorumlanmaz, yalnızca beyan edilir.
                ChargedOnlyIfStayTakesPlace =
                    !CityTaxLiability.ArisesFrom(ReservationStatus.Cancelled),
                ChildExemptionApplied = taxProfile.CityTaxExemptChildren,
                ChildAgeLimit = taxProfile.CityTaxChildAgeLimit
            },
            Nightly = pricingResult.Nightly
                .Select(night => new PublicNightlyRateResponse { Date = night.Date, Gross = night.Gross })
                .ToArray(),
            AverageNightlyGross = pricingResult.Nights > 0
                ? InvoiceAmounts.Round(accommodation.Gross / pricingResult.Nights)
                : accommodation.Gross,

            // Girişte ödeme: ön ödeme yok, tamamı otelde ödenir.
            DepositPercent = 0m,
            AmountDueAtProperty = totalGross,
            PrepaidAmount = 0m,
            OptionalExtras = []
        };
    }

    /// <summary>Otelin vergi profilini faturalama bağlamına çevirir (oranlar koda gömülmez).</summary>
    private static InvoiceTaxContext ToTaxContext(PublicHotelContext hotel)
    {
        var profile = hotel.Hotel.TaxProfile;

        return new InvoiceTaxContext(
            hotel.HotelId,
            hotel.Hotel.Currency,
            hotel.Hotel.DefaultCulture,
            profile.VatRate,
            profile.ReducedVatRate,
            profile.CityTaxPerPersonNight,
            profile.CityTaxEnabled,
            profile.CityTaxExemptChildren,
            profile.CityTaxChildAgeLimit);
    }
}
