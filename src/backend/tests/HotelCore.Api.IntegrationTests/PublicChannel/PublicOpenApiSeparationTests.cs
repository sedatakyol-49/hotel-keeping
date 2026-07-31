using AwesomeAssertions;
using HotelCore.Api.IntegrationTests.Infrastructure;
using HotelCore.Api.Startup;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.Swagger;

namespace HotelCore.Api.IntegrationTests.PublicChannel;

/// <summary>
/// <b>İki OpenAPI belgesinin ayrıklığı</b> (architecture-public-booking.md §3).
///
/// <para>Misafir uygulaması client'ını public belgeden üretir ve admin şemalarının <b>tek bir
/// tipini bile</b> görmemelidir: tek bir belge, admin DTO'larını (ve dolayısıyla iç alan
/// adlarını, modül yapısını, izin kavramlarını) misafir paketine taşırdı.</para>
///
/// <para><b>Neden HTTP değil <see cref="ISwaggerProvider"/>:</b> Swagger uç noktaları yalnızca
/// Development'ta yayımlanır; test host'u "Testing" ortamında koşar. Belgeyi doğrudan üreticiden
/// almak hem ortamdan bağımsızdır hem de tam olarak Swashbuckle'ın yazacağı modeli denetler
/// (metin araması yerine şema/yol koleksiyonları).</para>
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class PublicOpenApiSeparationTests(PostgresFixture fixture)
{
    /// <summary>İki belgede de bulunması meşru olan, domain taşımayan tipler.</summary>
    private static readonly string[] SharedFrameworkSchemas = ["ProblemDetails"];

    /// <summary>
    /// Public sözleşmede geçmesi yasak alan adları (§4.3). Şema bir alanı tanımlıyorsa er ya da
    /// geç bir yanıt da taşır — sızıntı orada değil, burada yakalanmalıdır.
    /// </summary>
    private static readonly string[] ForbiddenPropertyNames =
    [
        "roomNumber", "floor", "housekeepingStatus", "isOutOfOrder", "reservationNumber",
        "roomId", "roomTypeId", "hotelId", "headOfficeId", "guestId", "folioId",
        "ratePlanId", "ratePlanName", "notes", "note", "occupancyRate", "adr", "revPar"
    ];

    [RequiresPostgresFact]
    public void The_public_document_shares_no_schema_with_the_admin_document()
    {
        var admin = GetDocument(PublicApiDocument.AdminDocumentName);
        var publicDocument = GetDocument(PublicApiDocument.DocumentName);

        var adminSchemas = SchemaNames(admin);
        var publicSchemas = SchemaNames(publicDocument);

        adminSchemas.Should().NotBeEmpty();
        publicSchemas.Should().NotBeEmpty();

        var overlap = adminSchemas
            .Intersect(publicSchemas, StringComparer.Ordinal)
            .Except(SharedFrameworkSchemas, StringComparer.Ordinal)
            .ToArray();

        overlap.Should().BeEmpty(
            "public istemci admin semalarinin tek bir tipini bile gormemelidir; ortak sema: {0}",
            string.Join(", ", overlap));
    }

    [RequiresPostgresFact]
    public void Each_document_only_contains_its_own_paths()
    {
        var adminPaths = GetDocument(PublicApiDocument.AdminDocumentName).Paths.Keys.ToArray();
        var publicPaths = GetDocument(PublicApiDocument.DocumentName).Paths.Keys.ToArray();

        adminPaths.Should().NotContain(path => path.StartsWith("/api/v1/public", StringComparison.Ordinal));
        publicPaths.Should().OnlyContain(path => path.StartsWith("/api/v1/public", StringComparison.Ordinal));

        // Sozlesmedeki 13 uc 12 yol sablonuna duser: /holds/{holdToken} GET ve DELETE ayni yolu
        // paylasir.
        publicPaths.Should().HaveCount(12);
    }

    [RequiresPostgresFact]
    public void The_public_document_never_declares_a_forbidden_field()
    {
        var document = GetDocument(PublicApiDocument.DocumentName);

        var schemas = document.Components?.Schemas ?? new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal);

        var offenders =
            from schema in schemas
            from property in schema.Value.Properties
                             ?? new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal)
            where ForbiddenPropertyNames.Contains(property.Key, StringComparer.OrdinalIgnoreCase)
            select $"{schema.Key}.{property.Key}";

        offenders.Should().BeEmpty("yasak alan listesi (§4.3) public semada gecemez");
    }

    [RequiresPostgresFact]
    public void The_public_document_declares_no_authentication_at_all()
    {
        var publicDocument = GetDocument(PublicApiDocument.DocumentName);
        var admin = GetDocument(PublicApiDocument.AdminDocumentName);

        // Misafir client'i bir Authorization basligi tasiyabilecek sekilde DOGMAMALIDIR:
        // public uclar kimligi tamamen yok sayar (architecture-public-booking.md §4.2).
        (publicDocument.Security ?? []).Should().BeEmpty();
        (publicDocument.Components?.SecuritySchemes ?? new Dictionary<string, IOpenApiSecurityScheme>(StringComparer.Ordinal))
            .Should().BeEmpty();

        foreach (var (path, item) in publicDocument.Paths)
        {
            foreach (var (method, operation) in item.Operations ?? [])
            {
                (operation.Security ?? []).Should().BeEmpty(
                    "public uc {0} {1} kimlik istemez",
                    method,
                    path);
            }
        }

        // Admin belgesi DEGISMEZ: Bearer semasi orada durur (regresyon guard'i).
        (admin.Components?.SecuritySchemes ?? new Dictionary<string, IOpenApiSecurityScheme>(StringComparer.Ordinal))
            .Should().ContainKey("Bearer");
    }

    [RequiresPostgresFact]
    public void The_public_document_never_documents_the_hotel_header()
    {
        var document = GetDocument(PublicApiDocument.DocumentName);

        // X-Hotel-Id public yolda YOK SAYILIR; semada gorunmesi istemciyi yaniltirdi.
        foreach (var (_, item) in document.Paths)
        {
            foreach (var (_, operation) in item.Operations ?? [])
            {
                (operation.Parameters ?? [])
                    .Should().NotContain(parameter =>
                        string.Equals(parameter.Name, "X-Hotel-Id", StringComparison.OrdinalIgnoreCase));
            }
        }
    }

    private OpenApiDocument GetDocument(string documentName)
    {
        using var scope = fixture.Api.Services.CreateScope();
        var provider = scope.ServiceProvider.GetRequiredService<ISwaggerProvider>();

        return provider.GetSwagger(documentName);
    }

    private static string[] SchemaNames(OpenApiDocument document) =>
        document.Components?.Schemas?.Keys.ToArray() ?? [];
}
