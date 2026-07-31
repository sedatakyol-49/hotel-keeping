using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Public.Common;

namespace HotelCore.Application.Features.Public.ListBrandHotels;

/// <summary>
/// <c>GET /api/v1/public/brands/{brandSlug}/hotels</c> — bir markanın <b>public kanalı açık</b>
/// otelleri. Marka sitesinin otel seçicisini ve prerender listesini besler.
/// <para>
/// Yanıt <b>düz dizidir</b>, sayfalama yoktur: bir markadaki otel sayısı azdır ve liste build
/// anında (prerender) tek seferde okunur.
/// </para>
/// </summary>
/// <param name="BrandSlug">Marka URL anahtarı (<c>HeadOffice.PublicSlug</c>).</param>
public sealed record PublicListBrandHotelsRequest(string BrandSlug)
    : IRequest<IReadOnlyList<PublicHotelListItemResponse>>;
