using HotelCore.Application.Common.Interfaces;

namespace HotelCore.Application.Tests.Support;

/// <summary>
/// <see cref="ICurrentUser"/>'in test icin degistirilebilir (mutable) uygulamasi.
/// <para>
/// Mock yerine elle yazilmis bir sahte kullanilmasinin nedeni: <c>AppDbContext</c> global query
/// filter'i <c>CurrentHotelId</c>'yi <b>her sorguda yeniden</b> okur. Testin ortasinda aktif oteli
/// degistirip (ornegin A otelinin kullanicisindan B otelinin kullanicisina gecerek) izolasyonu
/// dogrulamak icin basit bir property atamasi en okunur yoldur.
/// </para>
/// </summary>
internal sealed class TestCurrentUser : ICurrentUser
{
    public Guid? UserId { get; set; } = Guid.NewGuid();

    public Guid? HotelId { get; set; }

    public bool CanAccessAllHotels { get; set; }

    public Guid? HeadOfficeId { get; set; }

    public IReadOnlyCollection<string> Permissions { get; set; } = [];

    public bool IsAuthenticated { get; set; } = true;
}
