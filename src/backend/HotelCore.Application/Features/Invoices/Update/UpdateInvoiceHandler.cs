using HotelCore.Application.Common.Exceptions;
using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Localization;
using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Invoices.Common;
using HotelCore.Domain.Entities;
using HotelCore.Domain.Enums;

namespace HotelCore.Application.Features.Invoices.Update;

/// <summary>
/// Taslak faturayı günceller (tutarlar her zaman sunucuda yeniden hesaplanır).
///
/// <para><b>PUT'un kapsamı faturanın kaynağına göre değişir — bilinçli bir karardır:</b>
/// <list type="bullet">
///   <item><b>Elle kesilen fatura</b> (<c>reservationId == null</c>): satırların tamamı
///   istemcinindir → <b>tam değişim</b>. Gövde ne diyorsa fatura odur.</item>
///   <item><b>Rezervasyondan üretilen fatura</b>: <c>RoomCharge</c> ve <c>CityTax</c> satırlarının
///   sahibi <b>sunucudur</b> (konaklama satırı folio'dan taşınır, Kurtaxe otelin vergi
///   profilinden üretilir). PUT bu satırları <b>korur</b> ve gövdeyi yalnızca faturanın kendi
///   <c>Extra</c> satırlarına uygular. Gövdede <c>RoomCharge</c>/<c>CityTax</c> gelirse
///   <b>400</b>.</item>
/// </list></para>
///
/// <para><b>Neden koruma (a), reddetme (b) değil:</b> gerçek hata şuydu — 486,00 €'luk bir taslağa
/// tek bir <c>Extra</c> PUT'lamak oda ücretini ve Kurtaxe'yi <b>sessizce siliyor</b>, fatura
/// 108,00 €'ya düşüyordu. Bu, sözleşmedeki "rezervasyondan üretilen faturada oda ücreti tam olarak
/// bir kez yer alır ve toplamı <c>reservation.totalAmount</c>'a kuruşu kuruşuna eşittir"
/// garantisini çiğniyor; finalize edildikten sonra da düzeltilemiyordu (GoBD). PUT'u tümüyle
/// reddetmek (409) garantiyi korurdu ama <b>ekstra ekleme yolunu tamamen kapatırdı</b>: folio'ya
/// satır ekleyen bir uç yoktur (<c>/reservations/{id}/folio</c> yalnızca GET), yani ekstra girmenin
/// başka yolu kalmazdı. Eksik satırları sunucunun yeniden üretmesi (c) ise folio muhasebesini
/// bozar: konaklama satırı folio'da <i>tüketilmiş</i> bir kalemdir, "yeniden üretmek" onu ikinci
/// kez yaratmak demektir (daha önce düzeltilmiş bir çift faturalama hatası). Geriye kalan tek
/// tutarlı seçenek, sunucunun sahibi olduğu satırları PUT'un kapsamı dışında tutmaktır.</para>
///
/// <para>
/// <b>GoBD §6.1:</b> yalnızca <c>Draft</c> düzenlenebilir; <c>Finalized/Paid/Cancelled</c> için
/// <b>409</b> döner. Bu kural burada anlamlı mesajla, ayrıca <c>AppDbContext</c> guard'ında
/// son savunma olarak zorlanır.
/// </para>
/// <para>
/// <b>Denetim izi:</b> değişiklik <see cref="InvoiceAuditAction.Updated"/> olarak yazılır
/// (değişen alanlar + eski/yeni tutarlar + satır sayısı). Taslak henüz <i>belge</i> olmadığı için
/// bu kayıt GoBD açısından zorunlu değildir; <i>Nachvollziehbarkeit</i> (izlenebilirlik) için
/// tutulur: bir faturanın hangi tutarla oluşup hangi tutarla kesinleştiği yalnızca
/// <c>ModifiedAt/ModifiedByUserId</c> ile açıklanamaz. Kayıt, güncellemeyle <b>aynı
/// SaveChanges</b> içinde yazılır (bkz. <see cref="InvoiceAuditWriter"/>).
/// </para>
/// </summary>
internal sealed class UpdateInvoiceHandler(
    IAppDbContext database,
    IDateTimeProvider clock,
    InvoiceReader reader,
    InvoiceLineComposer composer,
    InvoiceAuditWriter audit)
    : IRequestHandler<UpdateInvoiceRequest, InvoiceDetailResponse>
{
    /// <summary>
    /// Yeni gönderilen ekstraların geçici sıra numarası ofseti — korunan satırların
    /// (en fazla <c>MaxLineItems</c> = 200) üstünde kalmasını garanti eder.
    /// </summary>
    private const int AppendedOffset = 1_000;

    public async Task<InvoiceDetailResponse> Handle(
        UpdateInvoiceRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var invoice = await reader.GetTrackedAsync(request.Id, cancellationToken).ConfigureAwait(false);

        InvoicePersistence.EnsureDraft(invoice.Status);

        var tax = await reader.GetTaxContextAsync(invoice.HotelId, cancellationToken).ConfigureAwait(false);
        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);

        // Denetim izi icin ONCEKI hal: tutarlar ve satir sayisi degistirilmeden once okunur.
        var before = new InvoiceSnapshot(
            invoice.GuestId,
            invoice.Culture,
            invoice.LineItems.Count,
            invoice.NetAmount,
            invoice.VatAmount,
            invoice.CityTaxAmount,
            invoice.GrossAmount);

        if (request.GuestId is Guid guestId && guestId != invoice.GuestId)
        {
            if (invoice.ReservationId is not null)
            {
                throw new ConflictException(Messages.InvoiceGuestFromReservation);
            }

            _ = await reader.GetGuestAsync(guestId, cancellationToken).ConfigureAwait(false);
            invoice.GuestId = guestId;
        }

        if (SupportedCultures.IsSupported(request.Culture))
        {
            invoice.Culture = SupportedCultures.Normalize(request.Culture!);
        }

        var fromReservation = invoice.ReservationId is not null;

        if (fromReservation)
        {
            // Sunucunun sahibi oldugu satirlar (folio'dan tasinan konaklama + ekstralar, uretilen
            // Kurtaxe) DOKUNULMADAN kalir; govde yalnizca faturanin kendi Extra satirlarini
            // degistirir. Boylece oda ucreti faturada tam olarak bir kez ve dogru tutarla durur.
            EnsureOnlyExtraLines(request.LineItems);
            composer.RemoveOwnExtraLines(invoice);
        }
        else
        {
            // Tam degisim: folio kaynakli satirlar folio'ya geri doner (silinmez), faturaya ozgu
            // satirlar silinir ve yerlerine gonderilen satirlar yazilir.
            InvoiceLineComposer.ReleaseFolioLines(invoice);
            composer.RemoveOwnLines(invoice);
        }

        // Silmelerden SONRA, yeni satirlar eklenmeden ONCE okunur: EF fixup yeni satiri
        // invoice.LineItems'a kendisi ekleyecegi icin sonra okumak korunan kumeyi kirletirdi.
        var preservedLines = invoice.LineItems.ToList();

        var replacementLines = InvoiceLineComposer.BuildManualLines(
            invoice.HotelId,
            tax,
            request.LineItems,
            today);

        foreach (var line in replacementLines)
        {
            // DIKKAT (iki ayri EF Core tuzagi):
            // (1) Yeni satiri YALNIZCA navigation koleksiyonuna eklemek yetmez: anahtarlar
            //     uygulamada uretildigi icin (EntityBase.Id = Guid.NewGuid()) EF, izlenen ve
            //     durumu Modified/Unchanged olan bir ebeveynin altinda buldugu "anahtari dolu"
            //     cocugu Added degil MODIFIED sayar -> INSERT yerine UPDATE (0 satir -> hata).
            //     Bu yuzden satir DbSet'e eklenir; durumu kesin Added olur.
            // (2) InvoiceId atandiginda EF fixup satiri invoice.LineItems'a KENDISI ekler; ayrica
            //     elle eklemek koleksiyonda cift kayit ve tutarlarin iki katina cikmasina yol
            //     acar. Bu yuzden koleksiyona elle EKLENMEZ ve toplamlar asagida acik listeden
            //     hesaplanir.
            line.InvoiceId = invoice.Id;

            if (fromReservation)
            {
                // Yeni ekstralar korunan satirlarin ARDINA dussun: BuildManualLines sirayi 0'dan
                // baslattigi icin ofset olmadan mevcut sortOrder'larla cakisirdi. Nihai numaralama
                // ResequenceForDocument'te 0..n olarak yeniden yazilir.
                line.SortOrder += AppendedOffset;
            }

            database.InvoiceLineItems.Add(line);
        }

        // Faturanin NIHAI satir kumesi: korunanlar + gonderilenler.
        List<InvoiceLineItem> allLines = [.. preservedLines, .. replacementLines];

        // Satirsiz fatura anlamsizdir. Kontrol validator'da degil BURADA: rezervasyona bagli
        // faturada bos bir "lineItems" mesru bir istektir ("tum elle eklenen ekstralari kaldir")
        // ve sunucunun satirlari zaten yerinde durur; elle kesilen faturada ise bos gövde
        // gercekten satirsiz bir belge uretirdi. Ayrim ancak faturanin kaynagi bilinerek yapilir.
        if (allLines.Count == 0)
        {
            throw new ValidationException(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["LineItems"] = [Messages.InvoiceNeedsLines]
            });
        }

        if (fromReservation)
        {
            ResequenceForDocument(allLines);
        }

        // Toplamlar navigation koleksiyonundan degil ACIK listeden hesaplanir (EF fixup nedeniyle
        // koleksiyon beklenmedik sekilde degisebilir; bkz. yukaridaki not).
        InvoiceAmounts.ApplyTotals(invoice, allLines);

        audit.Append(invoice, InvoiceAuditAction.Updated, new
        {
            changedFields = CollectChangedFields(before, invoice, allLines.Count),
            guestId = new
            {
                old = before.GuestId,
                @new = invoice.GuestId
            },
            culture = new
            {
                old = before.Culture,
                @new = invoice.Culture
            },
            lineCount = new
            {
                old = before.LineCount,
                @new = allLines.Count
            },
            netAmount = new { old = before.NetAmount, @new = invoice.NetAmount },
            vatAmount = new { old = before.VatAmount, @new = invoice.VatAmount },
            cityTaxAmount = new { old = before.CityTaxAmount, @new = invoice.CityTaxAmount },
            grossAmount = new { old = before.GrossAmount, @new = invoice.GrossAmount },
            currency = invoice.Currency
        });

        // Fatura + satirlar + denetim izi TEK transaction.
        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await reader.GetDetailAsync(invoice.Id, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Rezervasyondan üretilen faturada istemci gövdesi <b>yalnızca</b> <c>Extra</c> satır
    /// taşıyabilir. <c>RoomCharge</c> ve <c>CityTax</c> sunucunun ürettiği kalemlerdir: oda ücreti
    /// folio'dan gelir ve <c>reservation.totalAmount</c>'a kuruşu kuruşuna eşittir, Kurtaxe otelin
    /// vergi profilinden hesaplanır. İstemcinin bunları göndermesine izin vermek ya ikinci bir
    /// konaklama satırı (çift faturalama) ya da tutarı elle değiştirme yolu açardı; <b>sessizce
    /// yok saymak</b> ise kullanıcıya gönderdiğini kaydettiğini düşündürürdü. Bu yüzden açıkça
    /// <b>400</b> döner.
    /// </summary>
    private static void EnsureOnlyExtraLines(IReadOnlyList<InvoiceLineInput> lineItems)
    {
        if (!lineItems.Any(line => line.Type is not InvoiceLineType.Extra))
        {
            return;
        }

        throw new ValidationException(new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["LineItems"] = [Messages.InvoiceReservationLinesServerOwned]
        });
    }

    /// <summary>
    /// Belge sırasını yeniden kurar: <b>konaklama → ekstralar → Kurtaxe</b>. Korunan satırlar
    /// kendi aralarındaki sırayı sürdürür, yeni ekstralar mevcut ekstraların ardına düşer
    /// (<see cref="AppendedOffset"/> sıralamada onları sona iter, sonra hepsi 0..n olarak yeniden
    /// numaralanır). Kurtaxe'nin faturanın <b>en altında</b> kalması alışıldık okumadır; sıra
    /// yalnızca taslakta değişir, kesinleşmiş belgeye dokunulmaz.
    /// </summary>
    private static void ResequenceForDocument(List<InvoiceLineItem> lines)
    {
        var ordered = lines
            .OrderBy(line => line.Type switch
            {
                InvoiceLineType.RoomCharge => 0,
                InvoiceLineType.CityTax => 2,
                _ => 1
            })
            .ThenBy(line => line.SortOrder)
            .ToList();

        for (var index = 0; index < ordered.Count; index++)
        {
            ordered[index].SortOrder = index;
        }
    }

    /// <summary>
    /// Gerçekten değişen alanların adları. Satırlar PUT semantiği gereği <b>her zaman</b> yeniden
    /// yazıldığı için "lineItems" değişiklik sayılır; tutar alanları yalnızca değer farklıysa
    /// listelenir (aynı satırlar yeniden gönderildiğinde iz gürültü üretmesin).
    /// </summary>
    private static List<string> CollectChangedFields(
        InvoiceSnapshot before,
        Invoice after,
        int newLineCount)
    {
        var changed = new List<string>(6) { "lineItems" };

        if (before.GuestId != after.GuestId)
        {
            changed.Add("guestId");
        }

        if (!string.Equals(before.Culture, after.Culture, StringComparison.Ordinal))
        {
            changed.Add("culture");
        }

        if (before.LineCount != newLineCount)
        {
            changed.Add("lineCount");
        }

        if (before.NetAmount != after.NetAmount)
        {
            changed.Add("netAmount");
        }

        if (before.VatAmount != after.VatAmount)
        {
            changed.Add("vatAmount");
        }

        if (before.CityTaxAmount != after.CityTaxAmount)
        {
            changed.Add("cityTaxAmount");
        }

        if (before.GrossAmount != after.GrossAmount)
        {
            changed.Add("grossAmount");
        }

        return changed;
    }

    /// <summary>Güncelleme öncesi taslak hâli (denetim izinde "eski" değerler).</summary>
    private sealed record InvoiceSnapshot(
        Guid GuestId,
        string Culture,
        int LineCount,
        decimal NetAmount,
        decimal VatAmount,
        decimal CityTaxAmount,
        decimal GrossAmount);
}
