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

        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "HotelCore API",
            Version = "v1",
            Description =
                "Coklu otel yonetim sistemi API'si. Tum uc noktalar /api/v1 altindadir, " +
                "hatalar RFC 7807 ProblemDetails formatindadir."
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
