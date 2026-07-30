using System.Globalization;
using HotelCore.Application.Common.Exceptions;
using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Localization;
using HotelCore.Domain.Entities;
using HotelCore.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HotelCore.Application.Features.Invoices.Common;

/// <summary>
/// Fatura satırlarını üretir: (a) elle girilen satırlardan, (b) rezervasyon + folio'dan.
/// Tutarlar ve KDV oranları <b>her zaman</b> burada (sunucuda) hesaplanır — bkz.
/// <see cref="InvoiceAmounts"/>.
///
/// <para><b>Oda ücretinin tek kaynağı folio'dur.</b> Konaklama satırını
/// <c>ReservationFolioService.SyncRoomChargeAsync</c> yazar ve rezervasyon güncellendikçe (PUT)
/// günceller; fatura bu satırı <b>tüketir</b> (<c>InvoiceId</c> atanır), yeniden üretmez. Bu kural
/// gerçek bir hatanın karşılığıdır: composer hem kendi <c>RoomCharge</c> satırını üretip hem de
/// folio'nun tüm satırlarını taşıdığında oda ücreti <b>iki kez</b> faturalanıyordu
/// (2 gece × 120,00 → 480,00).</para>
///
/// <para><b>Neden folio kazandı (fiyatlamanın tek yeri):</b> konaklama tutarı
/// <c>ReservationPricingService</c> tarafından <i>gece gece</i> hesaplanır ve sezon geçişinde
/// geceler farklı planlara düşebilir. Composer'ın eski hesabı (<c>nights × RatePlan.Price</c>)
/// ise <c>Reservation.RatePlanId</c>'yi — yani yalnızca <b>ilk gecenin</b> planını — tüm
/// konaklamaya uygulardı; iki hesap tek planlı konaklamada aynı, çok planlı konaklamada
/// <b>farklı</b> sonuç verirdi. Fiyatlama tek yerde (rezervasyon tarafında) kalır.</para>
/// </summary>
internal sealed class InvoiceLineComposer(IAppDbContext database)
{
    /// <summary>Elle girilen satırları entity'ye çevirir.</summary>
    public static List<InvoiceLineItem> BuildManualLines(
        Guid hotelId,
        InvoiceTaxContext tax,
        IReadOnlyList<InvoiceLineInput> inputs,
        DateOnly fallbackServiceDate)
    {
        ArgumentNullException.ThrowIfNull(inputs);

        var lines = new List<InvoiceLineItem>(inputs.Count);
        var sortOrder = 0;

        foreach (var input in inputs)
        {
            var line = new InvoiceLineItem
            {
                HotelId = hotelId,
                Type = input.Type,
                Description = input.Description.Trim(),
                Quantity = InvoiceAmounts.Round(input.Quantity),
                UnitPrice = InvoiceAmounts.Round(input.UnitPrice),
                // Leistungsdatum (GoBD): verilmezse fatura gunu.
                ServiceDate = input.ServiceDate ?? fallbackServiceDate,
                SortOrder = sortOrder++
            };

            // KDV orani ISTEMCIDEN ALINMAZ: satir turune gore otelin vergi profilinden cozulur.
            InvoiceAmounts.ApplyLineAmounts(line, InvoiceAmounts.ResolveVatRate(line.Type, tax));

            lines.Add(line);
        }

        return lines;
    }

    /// <summary>
    /// Rezervasyondan fatura satırları üretir:
    /// <list type="number">
    ///   <item><b>Konaklama + ekstralar folio'dan gelir:</b> folio'nun henüz faturalanmamış
    ///   satırları (<c>FolioId = folio ve InvoiceId = null</c>) faturaya <b>taşınır</b> (Domain
    ///   tasarımı: satır hem folio'yu hem faturayı işaret eder). Folio <i>defterdir</i>, fatura
    ///   defteri tüketir — böylece aynı masraf iki kez faturalanamaz.</item>
    ///   <item><b>Oda ücreti burada YENİDEN ÜRETİLMEZ</b> (bkz. sınıf belgesi "tek kaynak"):
    ///   konaklama satırının sahibi <c>ReservationFolioService.SyncRoomChargeAsync</c>'tir.
    ///   Yalnızca folio'da faturalanmamış bir <c>RoomCharge</c> <b>yoksa</b> geri düşülür ve satır
    ///   burada üretilir (<see cref="BuildFallbackRoomCharge"/>).</item>
    ///   <item><b>Kurtaxe:</b> otelde etkinse (vergiye tabi kişi × gece) ×
    ///   <c>CityTaxPerPersonNight</c>, <c>Type = CityTax</c> ve <b>KDV'siz</b>. Vergiye tabi kişi
    ///   sayısı <b>domain'de</b> hesaplanır (<see cref="TaxProfile.CountTaxablePersons"/>): otel
    ///   çocuk muafiyetini açtıysa yalnızca yetişkinler sayılır, aksi hâlde yetişkin + çocuk.
    ///   Kurtaxe folio'da tutulmadığı için tek üretim yeri burasıdır.</item>
    /// </list>
    /// </summary>
    public async Task<ReservationCharges> BuildFromReservationAsync(
        Guid reservationId,
        InvoiceTaxContext tax,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tax);

        var source = await database.Reservations
            .Where(reservation => reservation.Id == reservationId)
            .Select(reservation => new ReservationChargeSource(
                reservation.Id,
                reservation.HotelId,
                reservation.GuestId,
                reservation.ReservationNumber,
                reservation.CheckIn,
                reservation.CheckOut,
                reservation.Adults,
                reservation.Children,
                reservation.Room.Number,
                reservation.Room.RoomType.Code,
                reservation.TotalAmount,
                reservation.RatePlan == null ? null : (decimal?)reservation.RatePlan.Price,
                reservation.Room.RoomType.BasePrice,
                reservation.Folio == null ? null : (Guid?)reservation.Folio.Id))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(Messages.ReservationNotFound);

        // Ayni faturanin iki kez uretilmesini engelle: iptal edilmemis bir faturasi varsa 409.
        var alreadyInvoiced = await database.Invoices
            .AnyAsync(
                invoice => invoice.ReservationId == reservationId
                           && invoice.Status != InvoiceStatus.Cancelled,
                cancellationToken)
            .ConfigureAwait(false);

        if (alreadyInvoiced)
        {
            throw new ConflictException(Messages.ReservationAlreadyInvoiced);
        }

        var nights = Math.Max(1, source.CheckOut.DayNumber - source.CheckIn.DayNumber);

        // Kurtaxe'ye TABI kisi sayisi bir vergi profili kuralidir: Application katmani
        // "adults + children" toplamini kendi basina YORUMLAMAZ, domain metoduna sorar.
        // Muafiyet acikken sonuc yalnizca yetiskin sayisidir (cocuklar sayilmaz).
        var taxablePersons = tax.ToTaxProfile().CountTaxablePersons(source.Adults, source.Children);

        // Folio'nun henuz faturalanmamis TUM satirlari: konaklama satiri (RoomCharge) DAHIL,
        // ekstralar dahil. Folio defterdir; fatura defteri tuketir.
        var folioLines = source.FolioId is Guid folioId
            ? await database.InvoiceLineItems
                .Where(line => line.FolioId == folioId && line.InvoiceId == null)
                .OrderBy(line => line.SortOrder)
                .ThenBy(line => line.Id)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false)
            : [];

        var roomChargeComesFromFolio = folioLines.Exists(line => line.Type is InvoiceLineType.RoomCharge);

        var lines = new List<InvoiceLineItem>(2);
        var sortOrder = 0;

        // CIFT FATURALAMA GUARD'I: konaklama satiri ya folio'dan gelir ya burada uretilir,
        // ASLA ikisi birden olmaz.
        if (!roomChargeComesFromFolio)
        {
            lines.Add(BuildFallbackRoomCharge(source, nights, tax, sortOrder++));
        }

        foreach (var folioLine in folioLines)
        {
            folioLine.SortOrder = sortOrder++;

            // Folio satirlari baska bir modulden gelir; tutarlari BURADA yeniden hesaplanir.
            // Satirda anlamli bir oran varsa (0'dan buyuk) korunur — orn. ozel oranli bir kalem;
            // yoksa satir turunden cozulur.
            var vatRate = folioLine.Type is InvoiceLineType.CityTax
                ? 0m
                : folioLine.VatRate > 0m
                    ? folioLine.VatRate
                    : InvoiceAmounts.ResolveVatRate(folioLine.Type, tax);

            var folioGross = folioLine.LineNet + folioLine.LineVat;

            if (folioLine.Type is InvoiceLineType.RoomCharge && folioGross > 0m)
            {
                // Konaklamada BRUT TOPLAM otoriterdir (gece gece fiyatlanir), birim fiyat yalnizca
                // gosterim ortalamasidir; carpimi yeniden yapmak kurus kacagi uretirdi.
                // Aciklama faturada zenginlestirilir: GoBD hizmetin belgeden anlasilmasini bekler
                // (tarih araligi + oda no/tipi). Taslak iptalinde satir folio'ya bu metinle doner,
                // bir sonraki SyncRoomChargeAsync onu yeniden yazar.
                folioLine.Description = DescribeRoomCharge(source, nights);
                InvoiceAmounts.ApplyLineAmountsFromGross(folioLine, folioGross, vatRate);
            }
            else
            {
                InvoiceAmounts.ApplyLineAmounts(folioLine, vatRate);
            }
        }

        // Kurtaxe (City Tax) — otelde etkinse. KDV'ye TABI DEGILDIR (bkz. InvoiceAmounts §3).
        // Vergiye tabi kisi kalmadiysa (muafiyet acik + yalnizca cocuk) satir HIC uretilmez:
        // sifir tutarli kalem faturayi kirletir.
        if (tax.CityTaxEnabled && tax.CityTaxPerPersonNight > 0m && taxablePersons > 0)
        {
            var cityTax = new InvoiceLineItem
            {
                HotelId = source.HotelId,
                Type = InvoiceLineType.CityTax,
                Description = string.Format(
                    CultureInfo.InvariantCulture,
                    "City tax (Kurtaxe) {0} person(s) x {1} night(s){2}",
                    taxablePersons,
                    nights,
                    DescribeChildExemption(tax)),
                Quantity = taxablePersons * nights,
                UnitPrice = InvoiceAmounts.Round(tax.CityTaxPerPersonNight),
                ServiceDate = source.CheckIn,
                SortOrder = sortOrder
            };

            InvoiceAmounts.ApplyLineAmounts(cityTax, 0m);

            lines.Add(cityTax);
        }

        return new ReservationCharges(source, lines, folioLines);
    }

    /// <summary>
    /// <b>Geri düşüş (fallback):</b> folio'da faturalanmamış bir konaklama satırı yoksa oda
    /// ücretini faturanın kendisi üretir. Bu yol normalde çalışmaz; devreye girdiği durumlar:
    /// <list type="bullet">
    ///   <item>folio hiç açılmamış eski kayıtlar (<c>reservation.Folio == null</c>),</item>
    ///   <item>konaklama satırı zaten <b>kesinleşmiş</b> bir faturaya bağlanmış ve o fatura
    ///   Stornorechnung ile iptal edilmiş — satır orijinal belgede kalır (GoBD: kesinleşmiş
    ///   faturanın satırı koparılamaz), folio'ya geri dönmez.</item>
    /// </list>
    /// <para>
    /// <b>Fiyat kaynağı:</b> <c>Reservation.TotalAmount</c> — konaklamanın sunucuda, <b>gece gece</b>
    /// hesaplanmış brüt toplamı (<c>ReservationPricingService</c>). Ekstralar folio'da durduğu için
    /// bu tutar yalnızca odayı içerir. <c>nights × RatePlan.Price</c> bilinçli olarak <b>tercih
    /// edilmez</b>: <c>Reservation.RatePlanId</c> yalnızca <i>ilk gecenin</i> planıdır, sezon
    /// geçişinde tüm konaklamaya uygulanması yanlış tutar üretir. Son çare olarak (tutar 0 ise)
    /// düz <c>gece × plan/BasePrice</c> hesabı kullanılır, böylece satır asla 0,00 kalmaz.
    /// </para>
    /// </summary>
    private static InvoiceLineItem BuildFallbackRoomCharge(
        ReservationChargeSource source,
        int nights,
        InvoiceTaxContext tax,
        int sortOrder)
    {
        var gross = source.TotalAmount > 0m
            ? source.TotalAmount
            : InvoiceAmounts.Round(nights * (source.RatePlanPrice ?? source.BasePrice));

        var line = new InvoiceLineItem
        {
            HotelId = source.HotelId,
            Type = InvoiceLineType.RoomCharge,
            Description = DescribeRoomCharge(source, nights),
            Quantity = nights,
            // Birim fiyat gosterim icindir; kesin tutar LineNet + LineVat'tir (bkz.
            // InvoiceAmounts.ApplyLineAmountsFromGross).
            UnitPrice = InvoiceAmounts.Round(gross / nights),
            ServiceDate = source.CheckIn,
            SortOrder = sortOrder
        };

        InvoiceAmounts.ApplyLineAmountsFromGross(
            line,
            gross,
            InvoiceAmounts.ResolveVatRate(InvoiceLineType.RoomCharge, tax));

        return line;
    }

    /// <summary>
    /// Konaklama satırının fatura açıklaması (Leistungsbeschreibung): tarih aralığı, gece sayısı,
    /// oda numarası ve oda tipi. Açıklamalar bu fazda <b>dil-nötr ASCII</b>'dir.
    /// </summary>
    private static string DescribeRoomCharge(ReservationChargeSource source, int nights) =>
        string.Format(
            CultureInfo.InvariantCulture,
            "Room charge {0:yyyy-MM-dd} - {1:yyyy-MM-dd} ({2} x night, room {3}/{4})",
            source.CheckIn,
            source.CheckOut,
            nights,
            source.RoomNumber,
            source.RoomTypeCode);

    /// <summary>
    /// Kurtaxe satır açıklamasına eklenen <b>muafiyet notu</b>. Muafiyetin hukuki dayanağı
    /// belgede görünmelidir: aksi hâlde "2 kişi" yazan satırın neden 4 kişilik rezervasyonda
    /// 2 kişi olduğu faturadan anlaşılmaz (Kurtaxe beyanı da bu açıklamayı bekler).
    /// Yaş sınırı bilinmiyorsa (<c>null</c>) sınır belirtilmeden yazılır.
    /// Açıklamalar bu fazda <b>dil-nötr ASCII</b>'dir; yerelleştirme PDF/exporter fazında.
    /// </summary>
    private static string DescribeChildExemption(InvoiceTaxContext tax) =>
        tax.CityTaxExemptChildren
            ? tax.CityTaxChildAgeLimit is int ageLimit
                ? string.Format(
                    CultureInfo.InvariantCulture,
                    " - children under {0} exempt",
                    ageLimit)
                : " - children exempt"
            : string.Empty;

    /// <summary>
    /// Folio kaynaklı satırları faturadan <b>koparır</b> (silmez): <c>InvoiceId = null</c> ile
    /// masraf folio'da kalır ve ileride yeniden faturalanabilir. Taslak iptalinde ve satır
    /// değişiminde kullanılır — aksi hâlde folio masrafları kaybolurdu.
    /// </summary>
    public static void ReleaseFolioLines(Invoice invoice)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        foreach (var line in invoice.LineItems.Where(line => line.FolioId is not null).ToList())
        {
            line.InvoiceId = null;
            line.Invoice = null;
            invoice.LineItems.Remove(line);
        }
    }

    /// <summary>
    /// Faturaya özgü (folio kaynaklı olmayan) satırları siler — PUT'un "tam değişim" semantiği.
    /// Yalnızca taslak faturada çağrılır; kesinleşmiş faturada <c>AppDbContext</c> guard'ı zaten
    /// reddeder.
    /// </summary>
    public void RemoveOwnLines(Invoice invoice)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        foreach (var line in invoice.LineItems.Where(line => line.FolioId is null).ToList())
        {
            database.InvoiceLineItems.Remove(line);
            invoice.LineItems.Remove(line);
        }
    }
}

/// <summary>Rezervasyondan üretilen fatura girdisi.</summary>
/// <param name="Source">Rezervasyon özeti (misafir, otel, tarihler).</param>
/// <param name="NewLines">
/// Faturaya eklenecek <b>yeni</b> satırlar: Kurtaxe ve (yalnızca folio'da konaklama satırı yoksa)
/// geri düşüş oda ücreti. Normal akışta oda ücreti burada <b>yer almaz</b> — folio'dan taşınır.
/// </param>
/// <param name="FolioLines">
/// Faturaya taşınacak mevcut folio satırları: konaklama satırı + ekstralar.
/// </param>
internal sealed record ReservationCharges(
    ReservationChargeSource Source,
    List<InvoiceLineItem> NewLines,
    List<InvoiceLineItem> FolioLines);

/// <summary>Faturalama için gereken rezervasyon alanları (yalnızca bu kolonlar okunur).</summary>
/// <param name="TotalAmount">
/// Konaklamanın sunucuda gece gece hesaplanmış brüt toplamı — yalnızca geri düşüş yolunda kullanılır.
/// </param>
/// <param name="RatePlanPrice">
/// <b>İlk gecenin</b> plan fiyatı. Tüm konaklamaya uygulanamaz (sezon geçişi); son çare hesabıdır.
/// </param>
internal sealed record ReservationChargeSource(
    Guid Id,
    Guid HotelId,
    Guid GuestId,
    string ReservationNumber,
    DateOnly CheckIn,
    DateOnly CheckOut,
    int Adults,
    int Children,
    string RoomNumber,
    string RoomTypeCode,
    decimal TotalAmount,
    decimal? RatePlanPrice,
    decimal BasePrice,
    Guid? FolioId);
