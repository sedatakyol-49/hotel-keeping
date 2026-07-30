namespace HotelCore.Application.Common.Interfaces;

/// <summary>
/// Boşluksuz (kesintisiz) fatura numarası üreticisi — GoBD §6.2 (architecture.md).
/// <para>
/// <b>Sözleşme:</b> numara <see cref="HotelInvoiceCounterContract"/>'ta anlatılan sayaç satırı
/// artırılarak üretilir ve <b>kaydetme çağıranın sorumluluğundadır</b>: bu metot
/// <c>SaveChangesAsync</c> ÇAĞIRMAZ. Böylece sayaç artışı ile faturanın numarası/durumu
/// <b>tek bir SaveChanges (tek transaction)</b> içinde kalıcı olur. Fatura kaydı başarısız
/// olursa sayaç artışı da geri alınır → <b>numara atlanmaz</b>.
/// </para>
/// <para>
/// <b>Numara biçimi:</b> <c>{yıl}-{6 haneli sıra}</c> → örn. <c>2026-000001</c>. Sekans
/// <b>otel + yıl</b> bazındadır; her yıl 1'den başlar (Almanya'da yaygın uygulama).
/// </para>
/// </summary>
public interface IInvoiceNumberGenerator
{
    /// <summary>
    /// İlgili otel/yıl sayacını bir artırır ve atanacak numarayı döndürür.
    /// </summary>
    /// <param name="hotelId">
    /// Numaranın alınacağı otel. <b>Aktif otel değil, faturanın oteli</b> verilir: Head Office
    /// kullanıcısı konsolide modda başka bir otelin faturasını kesinleştirebilir ve numara o
    /// otelin sekansından gelmelidir.
    /// </param>
    /// <param name="year">Fatura tarihinin (IssuedAt) yılı — sekans yıl bazında sıfırlanır.</param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    /// <returns>Faturaya yazılacak numara (örn. <c>2026-000001</c>).</returns>
    Task<string> NextNumberAsync(Guid hotelId, int year, CancellationToken cancellationToken);
}

/// <summary>
/// Yalnızca dokümantasyon amaçlı işaretçi tip: eşzamanlılık garantisinin nerede sağlandığını
/// tek yerde anlatır (kod içinden referans verilebilmesi için tip olarak tutulur).
/// <para>
/// <b>Neden <c>SELECT ... FOR UPDATE</c> değil:</b> Application katmanı persistence portu olarak
/// yalnızca <c>IAppDbContext</c>'i görür ve bu port bilinçli olarak <b>sadece DbSet'ler +
/// SaveChangesAsync</b> açar (bkz. <c>AppDbContextPortContractTests</c>): ne
/// <c>DbContext.Database</c> (transaction/raw SQL), ne <c>Entry()</c>/<c>Reload()</c> erişimi
/// vardır. <c>FromSqlRaw("... FOR UPDATE")</c> ayrıca EF Core <i>Relational</i> paketini
/// gerektirir; Application'a sağlayıcıya yakın bir paket eklemek Dependency-Rule testini bozar.
/// </para>
/// <para>
/// <b>Kullanılan garanti:</b> <c>HotelInvoiceCounter.Version</c> optimistic concurrency token'ı.
/// Sayaç satırı okunur, <c>LastNumber</c> artırılır ve UPDATE cümlesi
/// <c>WHERE Id = @id AND Version = @okunanVersion</c> ile çalışır. Eşzamanlı iki finalize
/// isteğinde ikinci UPDATE 0 satır etkiler → <c>DbUpdateConcurrencyException</c> → tüm
/// transaction geri alınır (sayaç DA fatura DA yazılmaz) ve istemciye <b>409</b> döner.
/// Sonuç: <b>ne tekrar ne atlama</b> olur; kaybeden istek isteği yeniden gönderir.
/// </para>
/// <para>
/// <b>Neden otomatik retry yok:</b> başarısız <c>SaveChanges</c> sonrasında yeniden denemek için
/// takip edilen varlığın <i>orijinal</i> değerlerinin tazelenmesi (<c>entry.ReloadAsync()</c>)
/// gerekir; port bunu açmaz, tazelenmeden yapılan retry aynı eski <c>Version</c> ile sonsuza dek
/// başarısız olur. Ayrıca aynı faturayı eşzamanlı finalize eden iki istekte "körlemesine retry"
/// ikinci bir numara tüketip <b>gerçek bir boşluk</b> yaratabilirdi (birinci numara hiçbir belgeye
/// yazılmamış olarak kalırdı). Bu yüzden retry istemciye bırakılmıştır — 409 yanıtı bunu söyler.
/// </para>
/// <para>
/// <b>İleriye dönük:</b> numaralandırmanın Infrastructure'a taşınıp
/// <c>SELECT ... FOR UPDATE</c> + otomatik retry ile pesimist kilide çevrilmesi
/// (architecture.md §6.2'nin sözü) ayrı bir iş kalemidir; bu port değişmeden yapılabilir.
/// </para>
/// </summary>
public static class HotelInvoiceCounterContract
{
    /// <summary>Numara biçimi: önek + sıfır dolgulu sıra (örn. <c>2026-000001</c>).</summary>
    public const string NumberFormatDescription = "{year}-{sequence:D6}";
}
