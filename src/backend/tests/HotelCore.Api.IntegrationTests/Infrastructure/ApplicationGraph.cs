using HotelCore.Application;
using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Messaging;
using HotelCore.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HotelCore.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Gercek PostgreSQL'e bagli, tam bir <b>Application boru hatti</b> (kendi
/// <see cref="AppDbContext"/>'i, kendi change tracker'i, <c>AddApplication()</c> ile kurulmus
/// dispatcher + validation + handler grafigi).
///
/// <para><b>Neden HTTP degil dispatcher:</b> faturalama modulunun GoBD davranislarinin bir kismi
/// HTTP yuzeyinden gozlenemez ya da deterministik kurulamaz:
/// <list type="bullet">
///   <item><b>Eszamanli finalize yarisi</b> — iki <i>ayri change tracker</i> gerektirir ve
///         adimlarin elle siralanmasi gerekir (bkz. <c>InvoiceNumberSequenceTests</c>). HTTP
///         uzerinden istekler arasina girilemez, test zamanlama sansina kalirdi.</item>
///   <item><b>Persistence guard'i</b> (<c>EnforceInvoiceImmutability</c>) — handler'lar zaten
///         onunde 409 uretir; guard'in kendisini gormek icin dogrudan <c>SaveChanges</c>
///         cagirmak gerekir.</item>
/// </list>
/// RBAC, durum kodlari ve serilestirme sozlesmesi ayrica HTTP uzerinden dogrulanir.</para>
///
/// <para><b>Neden SQLite degil PostgreSQL:</b> fatura okuma yolu
/// (<c>InvoiceReader.GetDetailAsync/ListAsync</c>) odemeleri <c>PaidAt</c>, denetim izini
/// <c>PerformedAt</c> ile siralar. EF Core'un SQLite saglayicisi <c>DateTimeOffset</c> kolonuna
/// gore <c>ORDER BY</c>'i CEVIREMEZ (<c>NotSupportedException</c>), dolayisiyla <b>her</b> fatura
/// yazma ucu SQLite uzerinde patlar. Bu davranis handler katmaninda sahte bir saglayiciyla yesil
/// gosterilemezdi; bu yuzden faturalama testleri bilincli olarak buradadir.</para>
/// </summary>
internal sealed class ApplicationGraph : IAsyncDisposable
{
    private readonly ServiceProvider _provider;

    public ApplicationGraph(string connectionString, ScenarioIdentity identity, FrozenClock clock)
    {
        CurrentUser = identity;
        Clock = clock;

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.GetName().Name))
            .Options;

        // Kimlik/saat kurucudan verilir: global query filter ifadesi DbContext ORNEGINI yakalar.
        Database = new AppDbContext(options, identity, clock);

        var services = new ServiceCollection();
        services.AddSingleton(Database);
        services.AddSingleton<IAppDbContext>(Database);
        services.AddSingleton<ICurrentUser>(identity);
        services.AddSingleton<IDateTimeProvider>(clock);
        services.AddLogging();
        services.AddApplication();

        _provider = services.BuildServiceProvider();
    }

    /// <summary>Aktif kimlik baglami; testler aktif oteli buradan degistirir.</summary>
    public ScenarioIdentity CurrentUser { get; }

    /// <summary>Dondurulmus saat — denetim izi zaman damgalari deterministik kalsin diye.</summary>
    public FrozenClock Clock { get; }

    /// <summary>Handler'larla ayni DbContext ornegi (dogrulama ve guard testleri icin).</summary>
    public AppDbContext Database { get; }

    public IDispatcher Dispatcher => _provider.GetRequiredService<IDispatcher>();

    public async ValueTask DisposeAsync()
    {
        await Database.DisposeAsync();
        await _provider.DisposeAsync();
    }
}
