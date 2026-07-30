namespace HotelCore.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Integration testler icin PostgreSQL kaynagini belirler.
/// <list type="number">
///   <item><description>
///     <c>ConnectionStrings__Default</c> tanimliysa (CI'daki PostgreSQL service container)
///     dogrudan o kullanilir — Docker'a gerek yoktur.
///   </description></item>
///   <item><description>
///     Aksi halde Testcontainers ile gecici bir PostgreSQL konteyneri baslatilir (Docker gerekir).
///   </description></item>
///   <item><description>
///     Ikisi de yoksa testler SKIP edilir; boylece Docker'i olmayan gelistiricide
///     <c>dotnet test</c> kirmizi olmaz.
///   </description></item>
/// </list>
/// </summary>
internal static class DatabaseAvailability
{
    /// <summary>Uygulamanin okudugu ile ayni ortam degiskeni adi.</summary>
    public const string ConnectionStringEnvironmentVariable = "ConnectionStrings__Default";

    public const string SkipReason =
        "PostgreSQL yok: ne 'ConnectionStrings__Default' ortam degiskeni tanimli ne de Docker erisilebilir. " +
        "Integration testler atlandi (CI'da service container ile kosar).";

    /// <summary>CI service container'i (veya yerel bir veritabani) icin verilen baglanti dizesi.</summary>
    public static string? ExternalConnectionString { get; } =
        Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable) is { Length: > 0 } value
            ? value
            : null;

    /// <summary>Docker daemon'i erisilebilir gorunuyor mu (ucuz, senkron sezgisel kontrol).</summary>
    public static bool IsDockerAvailable { get; } = DetectDocker();

    public static bool IsAvailable => ExternalConnectionString is not null || IsDockerAvailable;

    /// <summary>
    /// Veritabanini ZORUNLU kilan ortam degiskeni. CI'da <c>true</c> verilir.
    /// <para>
    /// Neden gerekli: atlama (skip) mekanizmasi yerelde faydali ama CI'da <b>tehlikelidir</b> —
    /// baglanti dizesi yapilandirmadan dusse tum integration testler sessizce "skipped" olur ve
    /// is akisi yesil kalir. Bu degisken tanimliyken <c>PostgresAvailabilityTests</c> kaynagin
    /// gercekten var oldugunu dogrular ve yoksa CI'yi kirar.
    /// </para>
    /// </summary>
    public const string RequireDatabaseEnvironmentVariable = "HOTELCORE_REQUIRE_POSTGRES";

    /// <summary>CI gibi ortamlarda veritabaninin bulunmasi zorunlu mu.</summary>
    public static bool IsDatabaseRequired { get; } =
        bool.TryParse(
            Environment.GetEnvironmentVariable(RequireDatabaseEnvironmentVariable),
            out var required) && required;

    private static bool DetectDocker()
    {
        if (Environment.GetEnvironmentVariable("DOCKER_HOST") is { Length: > 0 })
        {
            return true;
        }

        try
        {
            if (OperatingSystem.IsWindows())
            {
                // Windows'ta Docker Desktop bir named pipe acar.
                return Directory
                    .EnumerateFiles(@"\\.\pipe\")
                    .Any(pipe => pipe.EndsWith("docker_engine", StringComparison.OrdinalIgnoreCase));
            }

            return File.Exists("/var/run/docker.sock");
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}
