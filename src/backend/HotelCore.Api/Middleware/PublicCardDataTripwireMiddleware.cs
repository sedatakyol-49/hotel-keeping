using System.Text.Json;
using HotelCore.Api.Startup;
using HotelCore.Application.Common.Exceptions;
using HotelCore.Application.Common.Localization;

namespace HotelCore.Api.Middleware;

/// <summary>
/// <b>PCI-DSS tuzak teli</b> (architecture-public-booking.md §6.2).
///
/// <para>Public bir POST/PUT/PATCH gövdesinde kart alanı adlarından biri geçerse istek
/// <b>400 <c>CARD_DATA_NOT_ACCEPTED</c></b> ile reddedilir ve <b>gövde loglanmaz</b>.</para>
///
/// <para><b>Neden bir middleware, neden bir validator değil:</b> validator yalnızca <i>tanımlı</i>
/// alanları görür; kart verisi tam da <b>tanımsız</b> bir alanla ("geçici olarak ekleyelim")
/// gelir. Tuzak telinin amacı, iyi niyetli bir geliştiricinin böyle bir alanı eklemesini
/// <i>imkânsız</i> kılmaktır: alan sözleşmeye girse bile istek gövdesi bu kapıdan geçemez.</para>
///
/// <para><b>Neden bu kadar sert:</b> kart verisi sistemlerimize <i>hiç</i> girmezse PCI-DSS
/// kapsamı dışında kalırız (SAQ-A sınıfı). Bir kez bile PAN kabul etmek tüm API'yi, tüm log
/// altyapısını, tüm yedekleri ve tüm geliştirme ortamlarını kapsama sokar — geri dönüşü çok
/// pahalı bir eşiktir.</para>
///
/// <para><b>Tarama JSON anahtar adları üzerindedir</b>, ham metin üzerinde değil: misafirin
/// serbest metin notunda "cardNumber" kelimesinin geçmesi rezervasyonu engellememelidir. Gövde
/// geçerli JSON değilse tel devreye girmez ve model binding kendi 400'ünü üretir.</para>
/// </summary>
public sealed class PublicCardDataTripwireMiddleware(
    RequestDelegate next,
    ILogger<PublicCardDataTripwireMiddleware> logger)
{
    /// <summary>
    /// Yasak alan adları. Liste <b>sözleşmenin parçasıdır</b>: yeni bir kart alanı adı eklemek,
    /// onu kabul etmek anlamına gelmez — buraya eklenmesi gerekir.
    /// </summary>
    private static readonly HashSet<string> ForbiddenFieldNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "pan",
        "cardNumber",
        "creditCardNumber",
        "cardnumber",
        "cvc",
        "cvv",
        "cvv2",
        "cid",
        "expiryMonth",
        "expiryYear",
        "expirationMonth",
        "expirationYear",
        "cardholderName",
        "cardHolder",
        "cardExpiry",
        "track1",
        "track2"
    };

    /// <summary>Taranacak en büyük gövde (64 KB). Public isteklerin hiçbiri buna yaklaşmaz.</summary>
    private const int MaxInspectedBytes = 64 * 1024;

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!ShouldInspect(context.Request))
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        // Gövde MVC tarafından tekrar okunacağı için tamponlama açılır ve başa sarılır.
        context.Request.EnableBuffering();

        var offender = await FindForbiddenFieldAsync(context.Request.Body, context.RequestAborted)
            .ConfigureAwait(false);

        context.Request.Body.Position = 0;

        if (offender is not null)
        {
            // YALNIZCA alan ADI loglanır — gövde hiçbir seviyede yazılmaz.
            logger.CardDataRejected(context.Request.Path, offender);

            throw PublicApiException.BadRequest(
                PublicErrorCodes.CardDataNotAccepted,
                Messages.PublicCardDataNotAccepted);
        }

        await next(context).ConfigureAwait(false);
    }

    private static bool ShouldInspect(HttpRequest request) =>
        PublicTenantMiddleware.IsPublicRequest(request.Path)
        && (HttpMethods.IsPost(request.Method)
            || HttpMethods.IsPut(request.Method)
            || HttpMethods.IsPatch(request.Method));

    /// <summary>Gövdeyi JSON olarak gezip yasaklı bir <b>anahtar adı</b> arar.</summary>
    private static async Task<string?> FindForbiddenFieldAsync(Stream body, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var limited = new byte[8192];
        int read;

        while ((read = await body.ReadAsync(limited, cancellationToken).ConfigureAwait(false)) > 0)
        {
            buffer.Write(limited, 0, read);

            if (buffer.Length > MaxInspectedBytes)
            {
                break;
            }
        }

        if (buffer.Length == 0)
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(buffer.ToArray());

            return FindForbiddenField(document.RootElement);
        }
        catch (JsonException)
        {
            // Bozuk JSON: tel devreye girmez, model binding kendi 400'ünü üretir.
            return null;
        }
    }

    private static string? FindForbiddenField(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    if (ForbiddenFieldNames.Contains(property.Name))
                    {
                        return property.Name;
                    }

                    var nested = FindForbiddenField(property.Value);
                    if (nested is not null)
                    {
                        return nested;
                    }
                }

                return null;

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    var nested = FindForbiddenField(item);
                    if (nested is not null)
                    {
                        return nested;
                    }
                }

                return null;

            default:
                return null;
        }
    }
}
