using System.Reflection;
using AwesomeAssertions;
using HotelCore.Application.Common.Interfaces;

namespace HotelCore.Application.Tests.Public;

/// <summary>
/// <b>Public DTO'lar admin DTO'larından ayrıdır</b> (architecture-public-booking.md §4.3).
///
/// <para><b>Gerekçe (paylaşmanın somut zararı):</b> admin DTO'ları zamanla büyür —
/// <c>RoomTypeResponse</c>'a yarın maliyet, doluluk veya iç not eklenir. Paylaşılan bir tip o
/// alanı <b>sessizce</b> public yanıta taşır; kimse bir güvenlik kararı vermediği hâlde veri
/// sızar. Ayrılık, sızıntıyı bir <i>unutma</i> hatasından bir <i>bilinçli ekleme</i> hatasına
/// dönüştürür — ve bu test o bilinçli eklemeyi görünür kılar.</para>
/// </summary>
public sealed class PublicDtoIsolationTests
{
    private const string PublicNamespace = "HotelCore.Application.Features.Public";

    private const string FeaturesNamespace = "HotelCore.Application.Features.";

    private static readonly Assembly ApplicationAssembly = typeof(IAppDbContext).Assembly;

    /// <summary>
    /// Public yanıtlarda <b>bulunması yasak</b> alan adları. Liste sözleşmenin kendisidir; bir
    /// alan buraya değil DTO'ya eklenirse test kırılır.
    /// </summary>
    private static readonly string[] ForbiddenPropertyNames =
    [
        "RoomNumber", "Floor", "HousekeepingStatus", "IsOutOfOrder",
        "ReservationNumber", "Notes", "Note", "RoomId", "RoomTypeId",
        "HotelId", "HeadOfficeId", "GuestId", "FolioId", "RatePlanId", "RatePlanName",
        "OccupancyRate", "Adr", "RevPar", "Cost", "TokenHash", "AccessTokenHash", "ClientIpHash"
    ];

    private static IEnumerable<Type> PublicTypes =>
        ApplicationAssembly.GetTypes()
            .Where(type => type.Namespace?.StartsWith(PublicNamespace, StringComparison.Ordinal) == true);

    /// <summary>Sözleşme yüzeyi: <c>Public*</c> önekli, dışa açık kayıt/sınıflar.</summary>
    private static IEnumerable<Type> PublicContractTypes =>
        PublicTypes.Where(type => type.IsPublic && type.Name.StartsWith("Public", StringComparison.Ordinal));

    [Fact]
    public void Public_contract_types_exist_and_all_carry_the_Public_prefix()
    {
        var responses = PublicContractTypes
            .Where(type => type.Name.EndsWith("Response", StringComparison.Ordinal))
            .ToArray();

        responses.Should().NotBeEmpty();
        responses.Should().OnlyContain(type => type.Name.StartsWith("Public", StringComparison.Ordinal));
    }

    [Fact]
    public void No_public_type_references_a_type_from_another_feature_module()
    {
        var offenders = new List<string>();

        foreach (var type in PublicTypes)
        {
            foreach (var referenced in ReferencedTypes(type))
            {
                var ns = referenced.Namespace;
                if (ns is null || !ns.StartsWith(FeaturesNamespace, StringComparison.Ordinal))
                {
                    continue;
                }

                if (ns.StartsWith(PublicNamespace, StringComparison.Ordinal))
                {
                    continue;
                }

                // Fiyat ve müsaitlik motorlarının YENIDEN KULLANIMI zorunludur (§8): bunlar DTO
                // degil SERVIS/hesaptir ve public tarafa kopyalanmalari yasaktir. Yalnizca
                // dogrudan bu tipler beklenir.
                if (AllowedReuse.Contains(referenced.Name, StringComparer.Ordinal))
                {
                    continue;
                }

                offenders.Add($"{type.FullName} -> {referenced.FullName}");
            }
        }

        offenders.Should().BeEmpty(
            "public tipler admin feature modullerinden tip referanslamaz; ihlaller: {0}",
            string.Join(" | ", offenders.Distinct(StringComparer.Ordinal)));
    }

    /// <summary>
    /// Yeniden kullanılması <b>istenen</b> (ve kopyalanması yasak olan) hesap tipleri —
    /// architecture-public-booking.md §8. Bunlar DTO değildir; sözleşme yüzeyine çıkmazlar.
    /// </summary>
    private static readonly string[] AllowedReuse =
    [
        "ReservationPricingService",
        "ReservationPricing",
        "NightlyRate",
        "InvoiceAmounts",
        "InvoiceTaxContext",
        "LineAmounts",
        "CityTaxLiability",
        "ReservationNumberGenerator",
        "ReservationFolioService",
        "ReservationStatusMachine",
        "AmenityList"
    ];

    [Fact]
    public void No_public_response_type_declares_a_forbidden_property()
    {
        var offenders =
            from type in PublicContractTypes
            where type.Name.EndsWith("Response", StringComparison.Ordinal)
            from property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            where ForbiddenPropertyNames.Contains(property.Name, StringComparer.Ordinal)
            select $"{type.Name}.{property.Name}";

        offenders.Should().BeEmpty("yasak alan listesi (§4.3) public yanit tiplerinde gecemez");
    }

    [Fact]
    public void No_public_response_type_exposes_a_Guid()
    {
        // Kimlikler public tarafta GUID DEGIL, stabil METIN anahtarlaridir: otel -> hotelSlug,
        // oda tipi -> roomTypeCode, rezervasyon -> bookingReference. Bir GUID sizmasi hem ic
        // kimlikleri disari verir hem de numaralandirma yuzeyi acar.
        var offenders =
            from type in PublicContractTypes
            where type.Name.EndsWith("Response", StringComparison.Ordinal)
            from property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            where property.PropertyType == typeof(Guid) || property.PropertyType == typeof(Guid?)
            select $"{type.Name}.{property.Name}";

        offenders.Should().BeEmpty();
    }

    /// <summary>Tipin property/alan/metot imzalarında geçen tüm tipler (generic argümanlar dâhil).</summary>
    private static IEnumerable<Type> ReferencedTypes(Type type)
    {
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic
                                                    | BindingFlags.Instance | BindingFlags.Static))
        {
            foreach (var candidate in Unwrap(property.PropertyType))
            {
                yield return candidate;
            }
        }

        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic
                                             | BindingFlags.Instance | BindingFlags.Static))
        {
            foreach (var candidate in Unwrap(field.FieldType))
            {
                yield return candidate;
            }
        }

        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic
                                               | BindingFlags.Instance | BindingFlags.Static
                                               | BindingFlags.DeclaredOnly))
        {
            foreach (var candidate in Unwrap(method.ReturnType))
            {
                yield return candidate;
            }

            foreach (var parameter in method.GetParameters())
            {
                foreach (var candidate in Unwrap(parameter.ParameterType))
                {
                    yield return candidate;
                }
            }
        }

        foreach (var constructor in type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic
                                                         | BindingFlags.Instance))
        {
            foreach (var parameter in constructor.GetParameters())
            {
                foreach (var candidate in Unwrap(parameter.ParameterType))
                {
                    yield return candidate;
                }
            }
        }
    }

    private static IEnumerable<Type> Unwrap(Type type)
    {
        var current = type;

        while (current.IsByRef || current.IsArray)
        {
            current = current.GetElementType()!;
        }

        yield return current;

        if (!current.IsGenericType)
        {
            yield break;
        }

        foreach (var argument in current.GetGenericArguments())
        {
            foreach (var nested in Unwrap(argument))
            {
                yield return nested;
            }
        }
    }
}
