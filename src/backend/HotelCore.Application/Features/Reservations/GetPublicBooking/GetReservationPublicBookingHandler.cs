using System.Text.Json;
using System.Text.Json.Nodes;
using HotelCore.Application.Common.Exceptions;
using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Localization;
using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Common.Security;
using HotelCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HotelCore.Application.Features.Reservations.GetPublicBooking;

/// <summary>
/// Rezervasyonun public kanal kanıtını okur.
/// <para>
/// Rezervasyon resepsiyondan girilmişse (public kaydı yoksa) <b>404</b> döner: "rıza alınmamış"
/// ile "rıza sorulmamış" ayrımı korunmalıdır — boş bir gövde döndürmek ikisini birbirine
/// karıştırırdı.
/// </para>
/// <para>
/// Anlık görüntüler <b>ham JSON</b> olarak döner ve yeniden yorumlanmaz: kanıtın değeri, o gün
/// misafire gönderilen gövdenin <i>aynısı</i> olmasındadır. Bugünkü DTO'lara deserialize edip
/// yeniden serileştirmek, aradaki her şema değişikliğini geçmişe geriye dönük uygulardı.
/// </para>
/// </summary>
internal sealed class GetReservationPublicBookingHandler(IAppDbContext database)
    : IRequestHandler<GetReservationPublicBookingRequest, ReservationPublicBookingResponse>
{
    public async Task<ReservationPublicBookingResponse> Handle(
        GetReservationPublicBookingRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Tenant izolasyonu global query filter'dan gelir; HotelId koşulu yazılmaz.
        var booking = await database.PublicBookings
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.ReservationId == request.ReservationId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(PublicBooking), request.ReservationId);

        return new ReservationPublicBookingResponse
        {
            ReservationId = booking.ReservationId,
            BookingReference = PublicBookingReference.Format(booking.BookingReference),
            AccessTokenExpiresAt = booking.AccessTokenExpiresAt,
            Culture = booking.Culture,
            CountryOfResidence = booking.CountryOfResidence?.ToString(),
            EstimatedArrivalLocalTime = booking.EstimatedArrivalLocalTime,
            InvoiceAddress = booking.InvoiceAddress.HasValue
                ? new PublicBookingInvoiceAddressResponse
                {
                    Company = booking.InvoiceAddress.Company,
                    AddressLine = booking.InvoiceAddress.AddressLine,
                    PostalCode = booking.InvoiceAddress.PostalCode,
                    City = booking.InvoiceAddress.City,
                    Country = booking.InvoiceAddress.Country?.ToString(),
                    VatId = booking.InvoiceAddress.VatId
                }
                : null,
            Consents = new PublicBookingConsentsResponse
            {
                TermsAccepted = booking.TermsAccepted,
                TermsVersion = booking.TermsVersion,
                PrivacyNoticeAcknowledged = booking.PrivacyNoticeAcknowledged,
                PrivacyNoticeVersion = booking.PrivacyNoticeVersion,
                WithdrawalNoticeAcknowledged = booking.WithdrawalNoticeAcknowledged,
                WithdrawalNoticeVersion = booking.WithdrawalNoticeVersion,
                BookerIsAdult = booking.BookerIsAdult,
                MarketingOptIn = booking.MarketingOptIn,
                RecordedAt = booking.ConsentRecordedAt
            },
            OrderButtonLabel = booking.OrderButtonLabel,
            SummaryHash = booking.SummaryHash,
            OrderSummary = ParseSnapshot(booking.OrderSummaryJson),
            Price = ParseSnapshot(booking.PriceSnapshotJson),
            CancellationPolicy = ParseSnapshot(booking.CancellationPolicySnapshotJson),
            Legal = ParseSnapshot(booking.LegalSnapshotJson),
            ConfirmationMode = booking.ConfirmationMode.ToString(),
            Confirmation = new PublicBookingConfirmationRecordResponse
            {
                SentAt = booking.ConfirmationSentAt,
                DocumentHash = booking.ConfirmationDocumentHash,
                DocumentVersion = booking.ConfirmationDocumentVersion,
                Culture = booking.ConfirmationCulture
            },
            CancelledAt = booking.CancelledAt,
            CancellationFeeAmount = booking.CancellationFeeAmount
        };
    }

    /// <summary>
    /// Anlık görüntüyü olduğu gibi geçirir. Okunamayan bir kayıt <c>null</c> döner: kanıtın
    /// bozulmuş olması bir teşhis konusudur, isteği 500 ile düşürmez.
    /// </summary>
    private static JsonNode? ParseSnapshot(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
