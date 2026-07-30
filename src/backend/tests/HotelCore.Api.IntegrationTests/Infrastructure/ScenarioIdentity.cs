using HotelCore.Application.Common.Interfaces;

namespace HotelCore.Api.IntegrationTests.Infrastructure;

/// <summary>
/// <see cref="ICurrentUser"/>'in dispatcher seviyesindeki testler icin degistirilebilir
/// uygulamasi. Mock yerine elle yazilmis bir sahte kullanilir cunku <c>AppDbContext</c> global
/// query filter'i <c>CurrentHotelId</c>'yi <b>her sorguda yeniden</b> okur: testin ortasinda
/// aktif oteli degistirip izolasyonu dogrulamak icin basit bir property atamasi en okunur yoldur.
/// </summary>
internal sealed class ScenarioIdentity : ICurrentUser
{
    public Guid? UserId { get; set; } = Guid.NewGuid();

    public Guid? HotelId { get; set; }

    public bool CanAccessAllHotels { get; set; }

    public Guid? HeadOfficeId { get; set; }

    public IReadOnlyCollection<string> Permissions { get; set; } = [];

    public bool IsAuthenticated { get; set; } = true;
}

/// <summary>
/// Dondurulmus saat. Denetim izi kayitlarinin <c>PerformedAt</c> degeri ve fatura
/// <c>IssuedAt</c> damgasi testlerde birebir karsilastirilabilsin diye zaman ilerlemez.
/// <para>
/// Baslangic degeri <b>gercek</b> UTC anidir: fatura numarasi sekansi otel + <b>yil</b> bazlidir,
/// bu yuzden sahte bir yil kullanmak numara beklentisini takvimden koparirdi.
/// </para>
/// </summary>
internal sealed class FrozenClock : IDateTimeProvider
{
    /// <summary>
    /// PostgreSQL <c>timestamptz</c> cozunurlugu <b>mikrosaniyedir</b>; .NET tick'i 100 ns'dir.
    /// Saat mikrosaniyeye kirpilmazsa yazilan ve geri okunan deger tam esit olmaz ve
    /// "IssuedAt == saat" gibi iddialar rastgele kirilirdi.
    /// </summary>
    private static DateTimeOffset Truncate(DateTimeOffset value) =>
        new(value.Ticks - (value.Ticks % 10), value.Offset);

    private DateTimeOffset _utcNow = Truncate(DateTimeOffset.UtcNow);

    public DateTimeOffset UtcNow
    {
        get => _utcNow;
        set => _utcNow = Truncate(value);
    }

    /// <summary>Saatin gosterdigi takvim gunu (handler'larin <c>today</c> hesabiyla ayni).</summary>
    public DateOnly Today => DateOnly.FromDateTime(UtcNow.UtcDateTime);

    /// <summary>Numara sekansinin yili.</summary>
    public int Year => UtcNow.UtcDateTime.Year;
}
