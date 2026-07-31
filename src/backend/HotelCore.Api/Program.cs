using System.Diagnostics;
using System.Text;
using HotelCore.Api.Middleware;
using HotelCore.Api.Services;
using HotelCore.Api.Startup;
using HotelCore.Application;
using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Localization;
using HotelCore.Application.Common.Security;
using HotelCore.Application.Features.Public.Common;
using HotelCore.Domain.Common;
using HotelCore.Infrastructure;
using HotelCore.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
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

// --- Misafire açık (public) rezervasyon kanalı ------------------------------
// Soyutlamalar Application'da, taşıyıcılar burada: gerçek bir PSP/bot sağlayıcısı/e-posta
// taşıyıcısı bu fazda YOKTUR ve geliştirme implementasyonları bunu görünür kılar
// (architecture-public-booking.md §6.1, §9.8).
builder.Services.AddPublicChannel(builder.Configuration);

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
// Enum'lar sözleşmede ADIYLA taşınır ("Dirty"), sayı olarak DEĞİL (api-contracts.md → Şekiller).
// Dönüştürücü hem gövde okumasını ("status": "Inspected") hem OpenAPI şemasındaki enum
// değerlerini üretir; hatalı değerde tip adı sızdırmayan bir mesaj döner.
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new StringEnumConverterFactory());

    // Saat alanları sözleşmede "HH:mm"dir (checkInFromLocal, estimatedArrivalLocalTime ...).
    // Varsayılan biçim saniyeyi de yazar; fark kozmetik DEĞİLDİR: orderSummary.hash kanonik
    // JSON üzerinden hesaplandığı için biçim değişikliği hash'i değiştirir ve istemciyle sunucu
    // asla uzlaşamaz (409 SUMMARY_CHANGED).
    options.JsonSerializerOptions.Converters.Add(new PublicTimeOnlyConverter());
    options.JsonSerializerOptions.Converters.Add(new PublicNullableTimeOnlyConverter());
});

builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
{
    // Her ProblemDetails yanıtında korelasyon kimliği bulunur (framework'ün ürettiği 401/403 dâhil).
    context.ProblemDetails.Instance ??=
        $"{context.HttpContext.Request.Method} {context.HttpContext.Request.Path}";
    context.ProblemDetails.Extensions["traceId"] =
        Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;

    // i18n: framework'ün kendi ürettiği yanıtlarda (istisna olmadan dönen 401/403 gibi) başlık
    // İngilizce sabittir. Bilinen durum kodları için isteğin dilindeki başlıkla değiştirilir;
    // ApiExceptionHandler zaten aynı metni yazdığı için orada bir değişiklik olmaz.
    // Kültür kapsamı şart: bu geri çağırım da localization middleware'inin dışında çalışabilir.
    using var culture = RequestCultureScope.Apply(context.HttpContext);

    // Yanıt aktif dili başlıkla da bildirir (api-contracts.md). Bu geri çağırım, istisnadan
    // doğmayan ProblemDetails yanıtlarını da (UseStatusCodePages, 401/403) kapsar;
    // ApiExceptionHandler ayrıca kendi yolunda çağırır — işlem etkisizdir (idempotent).
    ProblemDetailsResponseHeaders.Apply(context.HttpContext);

    var localizedTitle = Messages.TitleForStatusCode(context.ProblemDetails.Status);
    if (localizedTitle is not null)
    {
        context.ProblemDetails.Title = localizedTitle;
    }
});

// Model binding hataları (bozuk JSON, tanınmayan enum) ApiExceptionHandler'a uğramaz; MVC kendi
// 400'ünü üretir ve başlığı İngilizce sabittir. Karışık dilli yanıt oluşmasın diye başlık burada
// da isteğin diline çevrilir (errors sözlüğündeki metinler zaten yerelleştirilmiştir).
builder.Services.PostConfigure<ApiBehaviorOptions>(options =>
{
    var defaultFactory = options.InvalidModelStateResponseFactory;

    options.InvalidModelStateResponseFactory = context =>
    {
        var result = defaultFactory(context);

        if (result is ObjectResult { Value: ProblemDetails problem })
        {
            problem.Title = Messages.TitleForStatusCode(problem.Status) ?? problem.Title;
        }

        return result;
    };
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
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "HotelCore API v1");

        // İKİNCİ belge: misafir uygulaması client'ını buradan üretir ve admin şemalarının
        // tek bir tipini bile görmez (architecture-public-booking.md §3).
        options.SwaggerEndpoint(
            $"/swagger/{PublicApiDocument.DocumentName}/swagger.json",
            "HotelCore Public Booking API");
    });

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

// Public tenant kapsamı: yoldaki hotelSlug -> HotelId. Localization'dan ÖNCE olmak zorunda,
// çünkü Accept-Language yoksa yanıt OTELİN varsayılan dilinde üretilir ve o dil bu middleware'in
// çözdüğü otelden gelir. Ayrıca kapsam, kimliği public yolda tamamen bastırır: "admin token +
// public uç = daha geniş veri" yolu hiç açılmaz.
app.UseMiddleware<PublicTenantMiddleware>();

// Localization, kullanıcı profilindeki culture claim'ine ve otelin varsayılan diline
// düşebilmek için authentication ve public tenant çözümünden SONRA.
app.UseRequestLocalization(localizationOptions);

// Hız sınırı: uç bazında (hotelSlug, istemci IP). Aşımda 429 + Retry-After. Localization'dan
// sonra, böylece 429 gövdesi de isteğin dilinde döner.
app.UseMiddleware<PublicRateLimitMiddleware>();

// PCI tuzak teli: public gövdede kart alanı adı geçerse 400 CARD_DATA_NOT_ACCEPTED ve gövde
// LOGLANMAZ. Model binding'den ÖNCE çalışır — alan sözleşmeye girse bile gövde bu kapıdan geçemez.
app.UseMiddleware<PublicCardDataTripwireMiddleware>();

// X-Hotel-Id doğrulaması: yetkisiz otel isteği endpoint'e hiç ulaşmadan 403 ile reddedilir.
// (Public yollarda atlanır — orada otorite yoldadır.)
app.UseMiddleware<HotelContextMiddleware>();

app.UseAuthorization();
app.MapControllers();

await app.RunAsync().ConfigureAwait(false);

// Integration test'lerin WebApplicationFactory<Program> ile host'u ayağa kaldırabilmesi için.
public partial class Program;
