using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Public.Common;

namespace HotelCore.Application.Features.Public.GetLegal;

/// <summary>
/// <c>GET /api/v1/public/hotels/{hotelSlug}/legal</c> — §5 DDG (Impressum), DSGVO Art. 13
/// (aydınlatma) ve AGB. Prerender edilen <c>/impressum</c>, <c>/datenschutz</c>, <c>/agb</c>
/// sayfalarının kaynağıdır: JS kapalıyken de erişilebilir olmalıdır.
/// </summary>
public sealed record PublicGetLegalRequest : IRequest<PublicLegalResponse>;
