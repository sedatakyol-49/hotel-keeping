using HotelCore.Api.Services;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace HotelCore.Api.Startup;

/// <summary>
/// OpenAPI/Swagger üretimi. Frontend'in tip-güvenli client'ı bu şemadan üretildiği için
/// (api-contracts.md §Frontend Client Üretimi) şema eksiksiz olmalıdır:
/// Bearer güvenlik tanımı ve <c>X-Hotel-Id</c> header'ı dokümante edilir.
/// </summary>
public static class SwaggerConfiguration
{
    private const string BearerSchemeId = "Bearer";

    public static void Configure(SwaggerGenOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.SwaggerDoc(PublicApiDocument.AdminDocumentName, new OpenApiInfo
        {
            Title = "HotelCore API",
            Version = "v1",
            Description =
                "Coklu otel yonetim sistemi API'si. Tum uc noktalar /api/v1 altindadir, " +
                "hatalar RFC 7807 ProblemDetails formatindadir. " +
                "Misafire acik (public) uclar BU BELGEDE YOKTUR: /swagger/public-v1/swagger.json."
        });

        options.SwaggerDoc(PublicApiDocument.DocumentName, new OpenApiInfo
        {
            Title = "HotelCore Public Booking API",
            Version = "public-v1",
            Description =
                "Misafire acik rezervasyon kanali (/api/v1/public/**). Tum uclar anonimdir; " +
                "aktif otel yoldaki hotelSlug ile belirlenir. Hatalar RFC 7807 ProblemDetails " +
                "formatinda ve extensions.code alaninda dilden bagimsiz bir anahtar tasir. " +
                "Bu belge admin semalarindan HICBIR tip icermez."
        });

        // Belge ayrimi: public controller'lar GroupName = "public" tasir. Admin belgesi bu grubu
        // DISLAR — yeni bir public uc eklenirken grup adi unutulursa uc admin belgesine duser ve
        // DTO ayrikligi testi kirilir (sessizce sizmaz).
        options.DocInclusionPredicate((documentName, api) =>
        {
            var isPublic = string.Equals(api.GroupName, PublicApiDocument.GroupName, StringComparison.Ordinal);

            return string.Equals(documentName, PublicApiDocument.DocumentName, StringComparison.Ordinal)
                ? isPublic
                : !isPublic;
        });

        options.AddSecurityDefinition(BearerSchemeId, new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "JWT access token. /api/v1/auth/login uc noktasindan alinir."
        });

        // Microsoft.OpenApi 2.x: gereksinim, dokümanı alan bir fabrika ile eklenir ve
        // şemaya referansla (id) bağlanır.
        options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(BearerSchemeId, document)] = []
        });

        options.OperationFilter<HotelHeaderOperationFilter>();
        options.OperationFilter<CamelCaseQueryParameterFilter>();
        options.DocumentFilter<PublicDocumentFilter>();
    }

    /// <summary>
    /// Public belgeden <b>kimlik doğrulama izlerini</b> siler.
    /// <para>
    /// <c>AddSecurityDefinition</c>/<c>AddSecurityRequirement</c> Swashbuckle'da belge başına
    /// değil <b>global</b> uygulanır; müdahale edilmezse misafir belgesi de bir Bearer şeması
    /// bildirir. Bu yalnızca kozmetik bir fazlalık değildir: üretilen misafir client'ı bir
    /// <c>Authorization</c> başlığı taşıyabilecek şekilde doğar ve "public uçta token gönderme"
    /// fikrini normalleştirir — oysa public uçlar kimliği <b>tamamen</b> yok sayar.
    /// </para>
    /// </summary>
    private sealed class PublicDocumentFilter : IDocumentFilter
    {
        public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
        {
            ArgumentNullException.ThrowIfNull(swaggerDoc);
            ArgumentNullException.ThrowIfNull(context);

            if (!string.Equals(context.DocumentName, PublicApiDocument.DocumentName, StringComparison.Ordinal))
            {
                return;
            }

            swaggerDoc.Security?.Clear();
            swaggerDoc.Components?.SecuritySchemes?.Clear();
        }
    }

    /// <summary>
    /// Sorgu parametrelerini sözleşmedeki gibi camelCase adlarla dokümante eder
    /// (<c>?page=1&amp;pageSize=20</c>). ApiExplorer adları C# property adlarından (PascalCase)
    /// türetir; model binding büyük/küçük harf duyarsız olduğu için iki biçim de bağlanır,
    /// ancak üretilen frontend client'ın sözleşmeyle aynı sorgu dizesini kurması gerekir.
    /// Yalnızca query parametreleri değişir: route şablonundaki adlar ve header'lar korunur.
    /// </summary>
    private sealed class CamelCaseQueryParameterFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            ArgumentNullException.ThrowIfNull(operation);

            if (operation.Parameters is null)
            {
                return;
            }

            // Microsoft.OpenApi 2.x'te koleksiyon IOpenApiParameter tutar ve arayüzdeki Name
            // salt-okunurdur; ad yalnızca somut OpenApiParameter üzerinden değiştirilebilir.
            foreach (var parameter in operation.Parameters)
            {
                if (parameter is OpenApiParameter { In: ParameterLocation.Query } queryParameter
                    && !string.IsNullOrEmpty(queryParameter.Name))
                {
                    queryParameter.Name =
                        char.ToLowerInvariant(queryParameter.Name[0]) + queryParameter.Name[1..];
                }
            }
        }
    }

    /// <summary>
    /// Aktif oteli değiştiren opsiyonel <c>X-Hotel-Id</c> header'ını her uç noktaya ekler
    /// (anonim uç noktalar hariç — orada tenant bağlamı yoktur).
    /// </summary>
    private sealed class HotelHeaderOperationFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            ArgumentNullException.ThrowIfNull(operation);
            ArgumentNullException.ThrowIfNull(context);

            var allowsAnonymous = context.ApiDescription.ActionDescriptor.EndpointMetadata
                .Any(metadata => metadata is Microsoft.AspNetCore.Authorization.IAllowAnonymous);

            if (allowsAnonymous)
            {
                return;
            }

            operation.Parameters ??= [];
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = CurrentUser.HotelHeaderName,
                In = ParameterLocation.Header,
                Required = false,
                Description =
                    "Aktif otel. Bos birakilirsa JWT'deki varsayilan otel; " +
                    "Head Office kullanicisinda konsolide (tum oteller) gorunum kullanilir.",
                Schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "uuid" }
            });
        }
    }
}
