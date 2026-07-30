using System.Globalization;
using HotelCore.Application.Common.Interfaces;
using HotelCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HotelCore.Application.Features.Invoices.Common;

/// <summary>
/// <see cref="IInvoiceNumberGenerator"/> implementasyonu — GoBD §6.2 boşluksuz sekans.
///
/// <para><b>Neden Application katmanında:</b> normalde bir port implementasyonu
/// Infrastructure'a aittir. Burada iki kısıt bunu engelliyor: (a) sayaç artışının faturanın
/// kendisiyle <b>aynı</b> <c>SaveChanges</c>'te olması gerekir — yani aynı DbContext birimini
/// kullanmak zorunludur, (b) persistence portu (<c>IAppDbContext</c>) bilinçli olarak yalnızca
/// DbSet'leri açar; Infrastructure'a taşımak ya porta transaction/raw-SQL üyeleri eklemeyi
/// (sözleşme testleri bunu reddediyor) ya da ikinci bir DbContext örneği kullanmayı
/// (atomikliği bozar) gerektirirdi. Bu dosya yalnızca EF Core çekirdeğini kullanır, sağlayıcıya
/// bağımlı değildir — Dependency-Rule korunur.</para>
///
/// <para><b>Eşzamanlılık garantisi:</b> <c>HotelInvoiceCounter.Version</c> optimistic concurrency
/// token'ı (EF konfigürasyonunda <c>IsConcurrencyToken()</c>, değeri
/// <c>AppDbContext.SaveChanges</c> içinde otomatik artar). Sayaç UPDATE'i
/// <c>WHERE Id = @id AND Version = @okunan</c> ile çalışır; eşzamanlı ikinci istek 0 satır
/// etkiler → <c>DbUpdateConcurrencyException</c> → <b>tüm transaction</b> geri alınır (ne sayaç
/// ne fatura yazılır) → çağıran bunu <b>409</b>'a çevirir. Böylece <b>ne atlama ne tekrar</b>
/// oluşur. Ayrıntılı gerekçe ve <c>SELECT ... FOR UPDATE</c> ile karşılaştırma:
/// <see cref="HotelInvoiceCounterContract"/>.</para>
///
/// <para><b>Numara neden yalnızca finalize'da atanır:</b> taslak fatura bir belge değildir ve
/// silinebilir/terk edilebilir. Numarayı taslakta vermek, terk edilen her taslak için sekansta
/// gerçek bir <b>boşluk</b> bırakırdı. Bu yüzden taslakta <c>InvoiceNumber = ""</c> kalır ve
/// <c>Invoice(HotelId, InvoiceNumber)</c> unique index'i <c>InvoiceNumber &lt;&gt; ''</c> ile
/// filtrelidir.</para>
/// </summary>
internal sealed class InvoiceNumberGenerator(IAppDbContext database) : IInvoiceNumberGenerator
{
    /// <summary>Sıra numarasının sıfır dolgulu hane sayısı (1.000.000 fatura/yıl kapasitesi).</summary>
    private const int SequenceDigits = 6;

    public async Task<string> NextNumberAsync(Guid hotelId, int year, CancellationToken cancellationToken)
    {
        var counter = await database.HotelInvoiceCounters
            .FirstOrDefaultAsync(
                candidate => candidate.HotelId == hotelId && candidate.Year == year,
                cancellationToken)
            .ConfigureAwait(false);

        if (counter is null)
        {
            // Yilin ilk faturasi: sayac satiri olusturulur. Iki istek ayni anda olusturmaya
            // calisirsa (HotelId, Year) unique index'i devreye girer ve AppDbContext bunu
            // 409'a cevirir -> numara tekrari olusmaz.
            counter = new HotelInvoiceCounter
            {
                HotelId = hotelId,
                Year = year,
                LastNumber = 0,
                Prefix = BuildPrefix(year)
            };

            database.HotelInvoiceCounters.Add(counter);
        }

        if (string.IsNullOrEmpty(counter.Prefix))
        {
            counter.Prefix = BuildPrefix(year);
        }

        counter.LastNumber++;

        // SaveChanges BILINCLI olarak cagrilmaz: sayac artisi ile faturanin numara/durum
        // degisikligi tek transaction'da olmali (bkz. IInvoiceNumberGenerator sozlesmesi).
        return Format(counter.Prefix, counter.LastNumber);
    }

    /// <summary>Önek: <c>{yıl}-</c> → tam numara <c>2026-000001</c>.</summary>
    private static string BuildPrefix(int year) => year.ToString(CultureInfo.InvariantCulture) + "-";

    private static string Format(string prefix, int sequence) =>
        prefix + sequence.ToString(CultureInfo.InvariantCulture).PadLeft(SequenceDigits, '0');
}
