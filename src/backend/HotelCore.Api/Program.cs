using System.Diagnostics;
using System.Text;
using HotelCore.Api.Middleware;
using HotelCore.Api.Services;
using HotelCore.Api.Startup;
using HotelCore.Application;
using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Security;
using HotelCore.Domain.Common;
using HotelCore.Infrastructure;
using HotelCore.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Serilog;

const string CorsPolicyName = "HotelCoreFrontend";

var builder = WebApplication.CreateBuilder(args);

// Serilog — yapılandırma appsettings.json > "Serilog" bölümünden okunur.
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services));

// JWT ayarları başlangıçta doğrulanır: eksik/kısa secret ile ayağa kalkmak yerine
// açıklayıcı hata ile durulur (sessizce zayıf anahtara düşülmez).
var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
jwtOptions.Validate();

// --- Katman kayıtları -------------------------------------------------------
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<IDateTimeProvider, DateTimeProvider>();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// --- Kimlik doğrulama -------------------------------------------------------
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Claim adları OLDUĞU GİBİ korunur: "sub" -> ClaimTypes.NameIdentifier eşlemesi
        // yapılmaz, böylece api-contracts.md'deki şema kodda birebir kullanılabilir.
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Secret)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = JwtClaimNames.Email,
            RoleClaimType = "role"
        };
    });

// --- Yetkilendirme: her izin anahtarı için bir policy ------------------------
// Roller controller'a HARDCODE EDİLMEZ (architecture.md §7); policy adı = izin anahtarı.
var authorization = builder.Services.AddAuthorizationBuilder();
foreach (var permission in Permissions.All)
{
    authorization.AddPolicy(permission, policy => policy
        .RequireAuthenticatedUser()
        .RequireClaim(JwtClaimNames.Permission, permission));
}

// --- CORS -------------------------------------------------------------------
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddPolicy(CorsPolicyName, policy =>
{
    if (allowedOrigins.Length == 0)
    {
        return;
    }

    policy.WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials();
}));

// --- i18n -------------------------------------------------------------------
var localizationOptions = LocalizationConfiguration.Create(builder.Configuration);

// --- MVC / hata / OpenAPI ---------------------------------------------------
builder.Services.AddControllers();

builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
{
    // Her ProblemDetails yanıtında korelasyon kimliği bulunur (framework'ün ürettiği 401/403 dâhil).
    context.ProblemDetails.Instance ??=
        $"{context.HttpContext.Request.Method} {context.HttpContext.Request.Path}";
    context.ProblemDetails.Extensions["traceId"] =
        Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;
});

builder.Services.AddExceptionHandler<ApiExceptionHandler>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(SwaggerConfiguration.Configure);

var app = builder.Build();

// EN DIŞTA: istisna dönüştürücüden de dışarıda olmalı ki log'daki durum kodu istemcinin
// gerçekten aldığı kod olsun (aksi hâlde her ProblemDetails yanıtı 500 olarak loglanır).
app.UseSerilogRequestLogging();

// Tüm hatalar RFC 7807 ProblemDetails formatında döner.
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "HotelCore API v1"));

    // Development: bekleyen migration'lar uygulanır + seed çalıştırılır (demo veri dâhil).
    await DatabaseInitializer.InitializeDevelopmentAsync(app.Services).ConfigureAwait(false);
}
else
{
    // HTTPS zorlaması yalnızca Development dışında: yerel http profilinde gereksiz 307 üretmez.
    app.UseHttpsRedirection();
    app.UseHsts();
}

app.UseCors(CorsPolicyName);

app.UseAuthentication();

// Localization, kullanıcı profilindeki culture claim'ine düşebilmek için authentication'dan SONRA.
app.UseRequestLocalization(localizationOptions);

// X-Hotel-Id doğrulaması: yetkisiz otel isteği endpoint'e hiç ulaşmadan 403 ile reddedilir.
app.UseMiddleware<HotelContextMiddleware>();

app.UseAuthorization();
app.MapControllers();

await app.RunAsync().ConfigureAwait(false);

// Integration test'lerin WebApplicationFactory<Program> ile host'u ayağa kaldırabilmesi için.
public partial class Program;
