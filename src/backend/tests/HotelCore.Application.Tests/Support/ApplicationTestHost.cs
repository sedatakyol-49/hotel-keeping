using System.Globalization;
using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Localization;
using HotelCore.Application.Common.Messaging;
using HotelCore.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HotelCore.Application.Tests.Support;

/// <summary>
/// Handler testleri icin ortak mini host altyapisi (modulden bagimsiz kisim).
/// <para>
/// <b>Neden gercek <see cref="AppDbContext"/>:</b> handler'lar <c>IAppDbContext</c> uzerinden
/// calisir ama davranislarinin bir kismi DbContext'te yasar — tenant/soft-delete global query
/// filter'i, <c>Deleted -&gt; Modified</c> soft-delete donusumu, denetim alanlari. Elle yazilmis
/// bir <c>IAppDbContext</c> sahtesi bu davranislari taklit etmek zorunda kalir ve testler
/// gerceklikte dogrulanmayan seyleri dogruluyor gorunurdu.
/// </para>
/// <para>
/// <b>Neden SQLite (Npgsql degil):</b>
/// <list type="bullet">
///   <item>iliskisel bir saglayicidir; LINQ gercekten SQL'e <b>cevrilir</b>. EF Core InMemory
///         saglayicisi her seyi LINQ-to-Objects olarak degerlendirdigi icin "bu sorgu
///         cevrilemiyor" sinifindaki hatalari gizlerdi,</item>
///   <item>kismi unique index (<c>WHERE NOT "IsDeleted"</c>), FK <c>Restrict</c> ve zorunlu
///         kolonlar gercekten olusturulur,</item>
///   <item><c>:memory:</c> veritabani test basina sifirdan kurulur; Docker/daemon gerekmez ve
///         testler paralel kossa bile birbirine dokunmaz (idempotent).</item>
/// </list>
/// <b>SQLite'in taklit ETMEDIGI</b> davranislar bilincli olarak integration katmanina birakildi:
/// PostgreSQL'in dogal siralamasi/collation'i, <c>lower(...)</c>'in ASCII disi harflerdeki
/// (Almanca umlaut, Turkce karakterler) davranisi ve benzersizlik ihlalinin (SQLSTATE 23505)
/// <c>ConflictException</c>'a cevrilmesi. Bunlar <c>HotelCore.Api.IntegrationTests</c> icinde
/// gercek PostgreSQL'e karsi dogrulanir — burada sahte bir sonucla yesil gosterilmez.
/// </para>
/// <para>
/// Alt siniflar yalnizca <see cref="SeedAsync"/> ile kendi modullerinin verisini kurar; baglanti,
/// DI grafigi ve yasam dongusu burada tek yerde tutulur.
/// </para>
/// </summary>
internal abstract class ApplicationTestHost : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _provider;

    protected ApplicationTestHost()
    {
        // Baglanti acik kaldigi surece :memory: veritabani yasar; kapaninca izsiz yok olur.
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        CurrentUser = new TestCurrentUser();
        Clock = new TestClock();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        // Kimlik/saat kurucudan verilir: global query filter ifadesi DbContext ORNEGINI
        // yakalar, bu yuzden host boyunca tek bir context ornegi paylasilir.
        Database = new AppDbContext(options, CurrentUser, Clock);

        var services = new ServiceCollection();
        services.AddSingleton(Database);
        services.AddSingleton<IAppDbContext>(Database);
        services.AddSingleton<ICurrentUser>(CurrentUser);
        services.AddSingleton<IDateTimeProvider>(Clock);
        services.AddLogging();

        // Gercek DI kaydi: dispatcher + boru hatti (logging, validation) + handler'lar +
        // Mapster. Yani testler handler'i tek basina degil, uretimdeki boru hattiyla kosar.
        services.AddApplication();

        _provider = services.BuildServiceProvider();
    }

    /// <summary>Aktif kimlik baglami; testler aktif oteli buradan degistirir.</summary>
    public TestCurrentUser CurrentUser { get; }

    /// <summary>Dondurulmus saat (gelecek/gecmis tarih kurallari icin).</summary>
    public TestClock Clock { get; }

    /// <summary>Dogrulama icin dogrudan veritabani erisimi (handler'larla ayni ornek).</summary>
    public AppDbContext Database { get; }

    public IDispatcher Dispatcher => _provider.GetRequiredService<IDispatcher>();

    /// <summary>
    /// Verilen kulturu <see cref="RequestCulture.Current"/>'in okuyacagi sekilde ayarlar ve islem
    /// bitince eski kulturu geri koyar (diger testlere sizmaz).
    /// </summary>
    public static async Task<T> WithCultureAsync<T>(string culture, Func<Task<T>> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        var previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
        try
        {
            return await action();
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await Database.DisposeAsync();
        await _provider.DisposeAsync();
        await _connection.DisposeAsync();
    }

    /// <summary>Semayi kurar, ardindan modulun verisini seed eder.</summary>
    protected async Task InitialiseAsync()
    {
        await Database.Database.EnsureCreatedAsync();
        await SeedAsync();
    }

    /// <summary>Modul verisini kurar (alt sinif sorumlulugu).</summary>
    protected abstract Task SeedAsync();

    /// <summary>
    /// Kaydeder ve change tracker'i temizler: handler'lar veriyi gercekten veritabanindan okumak
    /// zorunda kalir, takip edilen ornek uzerinden "sahte" gecen test olmaz.
    /// </summary>
    protected async Task SaveAndDetachAsync()
    {
        await Database.SaveChangesAsync();
        Database.ChangeTracker.Clear();
    }
}
