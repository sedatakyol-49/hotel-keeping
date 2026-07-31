using HotelCore.Application.Common.Localization;
using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Public.Common;

namespace HotelCore.Application.Features.Public.GetLegal;

/// <summary>
/// Hukuki bilgiler.
/// <para>
/// <b>Hiçbir alan hardcode edilmez.</b> Künye müşteri-değişkenidir: işletmeci tüzel kişilik,
/// ticaret sicili, temsilci ve USt-IdNr. otelden otele farklıdır ve yıl içinde değişir. Koda
/// gömülü bir künye, bir müşteride yanlış olduğu anda §5 DDG ihlali üretir.
/// </para>
/// <para>
/// <b>USt-IdNr. <c>Hotel.VatId</c>'den okunur</b>, <c>TaxNumber</c>'dan değil: Steuernummer ve
/// USt-IdNr. iki ayrı numaradır ve künye USt-IdNr. arar.
/// </para>
/// </summary>
internal sealed class PublicGetLegalHandler(PublicHotelReader hotels, PublicLegalReader legal)
    : IRequestHandler<PublicGetLegalRequest, PublicLegalResponse>
{
    public async Task<PublicLegalResponse> Handle(
        PublicGetLegalRequest request,
        CancellationToken cancellationToken)
    {
        var context = await hotels.RequireCurrentAsync(cancellationToken).ConfigureAwait(false);
        var hotel = context.Hotel;
        var profile = hotel.LegalProfile;

        var documents = await legal
            .GetActiveDocumentsAsync(RequestCulture.Current, hotel.DefaultCulture, cancellationToken)
            .ConfigureAwait(false);

        var versions = documents.ToDictionary(
            document => document.Key,
            document => document.Version,
            StringComparer.Ordinal);

        return new PublicLegalResponse
        {
            Imprint = new PublicImprintResponse
            {
                LegalEntityName = profile.LegalEntityName,
                LegalForm = profile.LegalForm,
                RepresentedBy = profile.RepresentedBy,
                AddressLine = profile.AddressLine,
                PostalCode = profile.PostalCode,
                City = profile.City,

                // Künye ülkesi boşsa otelin ülkesi kullanılır (entity belgesindeki kural).
                Country = (profile.Country ?? hotel.Country).ToString(),
                Phone = profile.Phone,
                Email = profile.Email,
                RegisterCourt = profile.RegisterCourt,
                RegisterNumber = profile.RegisterNumber,
                VatId = hotel.VatId,
                SupervisoryAuthority = profile.SupervisoryAuthority,
                DisputeResolution = new PublicDisputeResolutionResponse
                {
                    ParticipatesInAdr = profile.ParticipatesInDisputeResolution,

                    // §36 VSBG: katılmayan işletme de bunu BİLDİRMEK zorundadır — "bilinmiyor"
                    // hâli yoktur, bu yüzden anahtar her iki durumda da doludur.
                    NoticeKey = profile.ParticipatesInDisputeResolution
                        ? "legal.adr.participating"
                        : "legal.adr.notParticipating",
                    Notice = profile.DisputeResolutionNotice,
                    OdrPlatformUrl = profile.OnlineDisputeResolutionUrl
                }
            },
            Documents = documents,
            WithdrawalRight = PublicLegalReader.BuildWithdrawalRight(versions)
        };
    }
}
