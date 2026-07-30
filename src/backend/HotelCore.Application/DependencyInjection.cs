using System.Reflection;
using FluentValidation;
using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Localization;
using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Common.Messaging.Behaviors;
using HotelCore.Application.Common.Services;
using HotelCore.Application.Features.Auth.Common;
using HotelCore.Application.Features.Availability.Common;
using HotelCore.Application.Features.Departments.Common;
using HotelCore.Application.Features.Employees.Common;
using HotelCore.Application.Features.Guests.Common;
using HotelCore.Application.Features.HeadOffices.Common;
using HotelCore.Application.Features.Hotels.Common;
using HotelCore.Application.Features.Hr.Common;
using HotelCore.Application.Features.Invoices.Common;
using HotelCore.Application.Features.RatePlans.Common;
using HotelCore.Application.Features.Reservations.Common;
using HotelCore.Application.Features.Rooms.Common;
using HotelCore.Application.Features.RoomTypes.Common;
using HotelCore.Application.Features.Shifts.Common;
using HotelCore.Application.Features.TimeEntries.Common;
using HotelCore.Application.Features.Vacations.Common;
using Mapster;
using MapsterMapper;
using Microsoft.Extensions.DependencyInjection;

namespace HotelCore.Application;

/// <summary>Application katmanının DI kaydı (dispatcher, boru hattı, validator'lar, Mapster).</summary>
public static class DependencyInjection
{
    /// <summary>
    /// Use-case altyapısını kaydeder:
    /// <list type="bullet">
    ///   <item><see cref="IDispatcher"/> ve assembly'deki tüm <see cref="IRequestHandler{TRequest,TResponse}"/>'lar,</item>
    ///   <item>boru hattı davranışları (logging → validation → handler),</item>
    ///   <item>FluentValidation validator'ları,</item>
    ///   <item>Mapster konfigürasyonu (<c>IRegister</c> taraması) ve DI-farkında <see cref="IMapper"/>.</item>
    /// </list>
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var assembly = typeof(DependencyInjection).Assembly;

        services.AddScoped<IDispatcher, Dispatcher>();

        // Kayıt sırası = çalışma sırası (dıştan içe): önce logging, sonra validation.
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        AddRequestHandlers(services, assembly);

        services.AddValidatorsFromAssembly(assembly, ServiceLifetime.Scoped, includeInternalTypes: true);

        AddMapster(services, assembly);

        // Auth slice'ının paylaşılan gövdesi (login + refresh + me).
        services.AddScoped<AuthSessionService>();

        // Dinamik içerik çevirileri (architecture.md §4.6) — modüller arası paylaşılır.
        services.AddScoped<TranslationService>();

        // Oda yönetimi slice'larının paylaşılan okuma gövdeleri (liste/detay/pano yanıtları).
        services.AddScoped<RoomTypeReader>();
        services.AddScoped<RoomReader>();

        // Ayarlar slice'ları: erişim kapsamı (UserHotelAccess) ve projeksiyonlar tek yerde.
        services.AddScoped<HotelReader>();
        services.AddScoped<HeadOfficeReader>();

        // Personel slice'ları: okuma gövdeleri ve paylaşılan benzersizlik kontrolleri.
        services.AddScoped<DepartmentReader>();
        services.AddScoped<EmployeeReader>();

        // İK slice'ları (izin / Zeiterfassung / vardiya): okuma gövdeleri + paylaşılan
        // çalışan araması ("çalışan aktif otelde mi?" sorusu tek yerde yanıtlanır).
        services.AddScoped<EmployeeLookup>();
        services.AddScoped<VacationReader>();
        services.AddScoped<TimeEntryReader>();
        services.AddScoped<ShiftReader>();

        // --- Faturalama (GoBD) ---------------------------------------------------------------
        // Okuma gövdesi, satır üretimi ve denetim izi yazıcısı slice'lar arasında paylaşılır.
        services.AddScoped<InvoiceReader>();
        services.AddScoped<InvoiceLineComposer>();
        services.AddScoped<InvoiceAuditWriter>();

        // Fatura numarası üreticisi: sayaç artışının fatura ile AYNI DbContext biriminde (aynı
        // SaveChanges/transaction) olması zorunlu olduğu için scoped ve Application katmanında
        // (gerekçe: InvoiceNumberGenerator sınıf yorumu).
        services.AddScoped<IInvoiceNumberGenerator, InvoiceNumberGenerator>();

        // IInvoiceExporter BİLİNÇLİ olarak kayıtlı DEĞİL: PDF/ZUGFeRD üretimi bu fazda yok,
        // GET /invoices/{id}/pdf 501 döner (sahte PDF üretilmez).

        // Rezervasyon modülü: müsaitlik motoru (iş kuralı olduğu için Application'da uygulanır),
        // okuma gövdeleri ve rezervasyonun paylaşılan yazma yardımcıları.
        services.AddScoped<IAvailabilityService, AvailabilityService>();
        services.AddScoped<AvailabilityReader>();
        services.AddScoped<GuestReader>();
        services.AddScoped<RatePlanReader>();
        services.AddScoped<ReservationReader>();
        services.AddScoped<ReservationPricingService>();
        services.AddScoped<ReservationNumberGenerator>();
        services.AddScoped<ReservationFolioService>();

        return services;
    }

    /// <summary>
    /// <c>IRequestHandler&lt;,&gt;</c> implementasyonlarını tarayıp kaydeder. Handler'lar
    /// scoped'dır: DbContext ile aynı yaşam süresini paylaşırlar.
    /// </summary>
    private static void AddRequestHandlers(IServiceCollection services, Assembly assembly)
    {
        foreach (var type in assembly.GetTypes())
        {
            if (type.IsAbstract || type.IsInterface || type.IsGenericTypeDefinition)
            {
                continue;
            }

            foreach (var handlerInterface in type.GetInterfaces())
            {
                if (handlerInterface.IsGenericType
                    && handlerInterface.GetGenericTypeDefinition() == typeof(IRequestHandler<,>))
                {
                    services.AddScoped(handlerInterface, type);
                }
            }
        }
    }

    /// <summary>
    /// Mapster: global statik ayar yerine izole bir <see cref="TypeAdapterConfig"/> kullanılır
    /// (aynı süreçte birden çok host ayağa kalkabilen testler için önemli).
    /// <see cref="ServiceMapper"/>, mapping sırasında DI'dan servis çözebilmeyi sağlar.
    /// </summary>
    private static void AddMapster(IServiceCollection services, Assembly assembly)
    {
        var config = new TypeAdapterConfig();
        config.Scan(assembly);

        services.AddSingleton(config);
        services.AddScoped<IMapper, ServiceMapper>();
    }
}
