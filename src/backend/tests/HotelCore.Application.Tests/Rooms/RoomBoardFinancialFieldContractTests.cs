using System.Collections;
using System.Reflection;
using AwesomeAssertions;
using HotelCore.Application.Features.RoomTypes.Common;
using HotelCore.Application.Features.Rooms.Common;

namespace HotelCore.Application.Tests.Rooms;

/// <summary>
/// <b>RBAC sozlesme testi</b> (architecture.md §7 / api-contracts.md → Rooms &amp; Housekeeping):
/// Housekeeping rolu fiyat/ciro GORMEZ, bu yuzden kat hizmetleri panosunun yanit tipinde
/// <b>hicbir finansal alan tanimli degildir</b> — alan gizlenmiyor, DTO'da hic yok.
/// <para>
/// Test reflection ile <see cref="RoomBoardResponse"/> tipinin tum gecis grafigini (kat listesi →
/// oda karti → ozet) tarar. Kalici bir guard'dir: birisi panoya "sadece bilgi olsun" diye
/// <c>basePrice</c> eklerse derleme gecse bile bu test kirmizi olur. Frontend'de gizlemek
/// yeterli degildir; kural backend'de uygulanir.
/// </para>
/// </summary>
public sealed class RoomBoardFinancialFieldContractTests
{
    /// <summary>
    /// Bir property adinin "para" kokenli oldugunu gosteren parcalar. Liste bilincli olarak
    /// dar tutuldu: <c>Total</c> gibi <b>sayac</b> alanlari finansal degildir ve panoda mesrudur.
    /// </summary>
    private static readonly string[] FinancialNameFragments =
    [
        "price",
        "currency",
        "amount",
        "cost",
        "revenue",
        "tax",
        "vat",
        "fee",
        "discount",
        "deposit",
        "balance",
        "rate"
    ];

    [Fact]
    public void Board_response_graph_exposes_no_financial_property()
    {
        var offenders = FinancialPropertiesOf(typeof(RoomBoardResponse));

        offenders.Should().BeEmpty(
            "kat hizmetleri panosu finansal veri tasimaz (architecture.md §7): {0}",
            string.Join(", ", offenders));
    }

    [Fact]
    public void Board_response_graph_still_exposes_the_operational_fields_it_needs()
    {
        // Guvenlik testinin "her seyi eledigi icin" yesil gorunmedigini kanitlar.
        var itemProperties = typeof(RoomBoardItemDto)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name)
            .ToArray();

        itemProperties.Should().BeEquivalentTo(
            nameof(RoomBoardItemDto.Id),
            nameof(RoomBoardItemDto.Number),
            nameof(RoomBoardItemDto.RoomTypeCode),
            nameof(RoomBoardItemDto.HousekeepingStatus),
            nameof(RoomBoardItemDto.IsOutOfOrder),
            nameof(RoomBoardItemDto.Note));
    }

    [Fact]
    public void The_detector_really_detects_financial_properties()
    {
        // Negatif kontrol: ayni tarayici, fiyat alani OLMASI GEREKEN oda tipi yanitinda
        // basePrice/currency'yi bulmalidir. Bulmazsa yukaridaki testler bos bir guvence olurdu.
        var offenders = FinancialPropertiesOf(typeof(RoomTypeResponse));

        offenders.Should().Contain($"{nameof(RoomTypeResponse)}.{nameof(RoomTypeResponse.BasePrice)}");
        offenders.Should().Contain($"{nameof(RoomTypeResponse)}.{nameof(RoomTypeResponse.Currency)}");
    }

    [Fact]
    public void Board_response_graph_carries_no_monetary_clr_type()
    {
        // Ad temelli tarama yaniltilabilir (ornegin "Extra" adli bir decimal). Ikinci savunma:
        // pano grafiginde hic decimal/para tipi property bulunmamalidir.
        var monetary = new List<string>();
        CollectProperties(typeof(RoomBoardResponse), monetary, [], property =>
            Nullable.GetUnderlyingType(property.PropertyType) is { } underlying
                ? underlying == typeof(decimal)
                : property.PropertyType == typeof(decimal));

        monetary.Should().BeEmpty("panoda decimal (para) tipinde alan olmamalidir");
    }

    private static List<string> FinancialPropertiesOf(Type root)
    {
        var offenders = new List<string>();
        CollectProperties(root, offenders, [], property => FinancialNameFragments.Any(fragment =>
            property.Name.Contains(fragment, StringComparison.OrdinalIgnoreCase)));

        return offenders;
    }

    /// <summary>
    /// DTO grafigini derinlemesine tarar; <paramref name="isOffending"/> kosuluna uyan
    /// property'lerin <c>Tip.Property</c> adini toplar. Koleksiyonlarin oge tipine inilir.
    /// </summary>
    private static void CollectProperties(
        Type type,
        List<string> offenders,
        HashSet<Type> visited,
        Func<PropertyInfo, bool> isOffending)
    {
        if (!visited.Add(type))
        {
            return;
        }

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (isOffending(property))
            {
                offenders.Add($"{type.Name}.{property.Name}");
            }

            foreach (var nested in DtoTypesOf(property.PropertyType))
            {
                CollectProperties(nested, offenders, visited, isOffending);
            }
        }
    }

    /// <summary>Property tipinden taranmaya deger DTO tiplerini cikarir (koleksiyon ogeleri dahil).</summary>
    private static IEnumerable<Type> DtoTypesOf(Type type)
    {
        if (type.IsPrimitive || type == typeof(string) || type == typeof(Guid) || type == typeof(decimal))
        {
            yield break;
        }

        if (typeof(IEnumerable).IsAssignableFrom(type) && type.IsGenericType)
        {
            foreach (var argument in type.GetGenericArguments())
            {
                foreach (var nested in DtoTypesOf(argument))
                {
                    yield return nested;
                }
            }

            yield break;
        }

        if (type.Namespace?.StartsWith("HotelCore.", StringComparison.Ordinal) == true)
        {
            yield return type;
        }
    }
}
