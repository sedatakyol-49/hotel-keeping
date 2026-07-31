using HotelCore.Application.Common.Interfaces;
using HotelCore.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HotelCore.Application.Features.Public.Common;

/// <summary>Hukuki belge anahtarları — <c>HotelLegalDocument.Key</c> ile birebir.</summary>
internal static class PublicLegalDocumentKeys
{
    public const string Terms = "terms";

    public const string Privacy = "privacy";

    public const string Withdrawal = "withdrawal";
}

/// <summary>
/// Yayımlanmış hukuki belgelerin okuma yolu (<c>GET /public/hotels/{slug}/legal</c> ve rıza
/// versiyonlarının doğrulanması).
///
/// <para><b>Versiyon neden ayrıca okunuyor:</b> rezervasyon isteğindeki
/// <c>consents.termsVersion</c> otelin <b>güncel</b> versiyonuyla karşılaştırılır; uyuşmazsa
/// <c>409 LEGAL_TEXT_CHANGED</c>. Bu karşılaştırma, sorgulanabilir bir "güncel versiyon" kaydına
/// ihtiyaç duyar — eski versiyonlar silinmez, <c>IsActive = false</c> ile saklanır (DSGVO Art. 7
/// Abs. 1 hesap verebilirlik: onaylanan metnin kendisi kanıttır).</para>
///
/// <para><b>Dil düşüşü:</b> istenen dilde yayın yoksa otelin varsayılan dilindeki yayın döner —
/// hukuki metnin hiç görünmemesi (§5 DDG "unmittelbar erreichbar") kabul edilemez.</para>
/// </summary>
internal sealed class PublicLegalReader(IAppDbContext database)
{
    /// <summary>Otelin güncel belgeleri, dil düşüşü uygulanmış hâlde.</summary>
    public async Task<IReadOnlyList<PublicLegalDocumentResponse>> GetActiveDocumentsAsync(
        string culture,
        string defaultCulture,
        CancellationToken cancellationToken)
    {
        // Tenant filtresi HotelLegalDocument üzerinde otomatiktir; HotelId koşulu yazılmaz.
        var rows = await database.HotelLegalDocuments
            .AsNoTracking()
            .Where(document => document.IsActive
                               && (document.Culture == culture || document.Culture == defaultCulture))
            .Select(document => new
            {
                document.Key,
                document.Culture,
                document.Version,
                document.Title,
                document.BodyHtml
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows
            .GroupBy(row => row.Key, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group =>
            {
                var preferred = group.FirstOrDefault(row =>
                                    string.Equals(row.Culture, culture, StringComparison.Ordinal))
                                ?? group.First();

                return new PublicLegalDocumentResponse
                {
                    Key = preferred.Key,
                    Title = preferred.Title,
                    Version = preferred.Version,
                    Culture = preferred.Culture,

                    // Gövde YAZMA anında sanitize edilmiş hâliyle saklanır (bkz. entity belgesi);
                    // okuma yolunda tekrar işlenmez.
                    BodyHtml = preferred.BodyHtml
                };
            })
            .ToArray();
    }

    /// <summary>Belge anahtarı → güncel versiyon (rıza doğrulaması ve dondurma için).</summary>
    public async Task<IReadOnlyDictionary<string, string>> GetActiveVersionsAsync(
        string culture,
        string defaultCulture,
        CancellationToken cancellationToken)
    {
        var documents = await GetActiveDocumentsAsync(culture, defaultCulture, cancellationToken)
            .ConfigureAwait(false);

        return documents.ToDictionary(
            document => document.Key,
            document => document.Version,
            StringComparer.Ordinal);
    }

    /// <summary>
    /// Hold ve rezervasyon yanıtlarındaki <c>legal</c> bloğu. Versiyonlar <b>dondurulur</b>:
    /// otel yarın AGB'sini değiştirdiğinde geçmiş rezervasyonun kanıtı değişmez.
    /// </summary>
    public static PublicLegalNoticesResponse BuildNotices(
        PublicHotelContext hotel,
        IReadOnlyDictionary<string, string> versions)
    {
        ArgumentNullException.ThrowIfNull(hotel);
        ArgumentNullException.ThrowIfNull(versions);

        return new PublicLegalNoticesResponse
        {
            WithdrawalRight = BuildWithdrawalRight(versions),
            OrderButton = new PublicOrderButtonResponse(),
            Terms = new PublicLegalDocumentRefResponse
            {
                Key = PublicLegalDocumentKeys.Terms,
                Version = Version(versions, PublicLegalDocumentKeys.Terms)
            },
            PrivacyNotice = new PublicLegalDocumentRefResponse
            {
                Key = PublicLegalDocumentKeys.Privacy,
                Version = Version(versions, PublicLegalDocumentKeys.Privacy)
            },

            // Sözleşmenin kurulma anı bir OTEL AYARIDIR (§10 madde 3): anında onay modelinde
            // onay e-postası Annahme'dir; otel kabulü modelinde ilk e-posta yalnızca
            // Zugangsbestätigung olur.
            ContractConclusion =
                hotel.Hotel.PublicBookingSettings.ConfirmationMode is PublicBookingConfirmationMode.Instant
                    ? "OnConfirmationEmail"
                    : "OnHotelAcceptance"
        };
    }

    /// <summary>
    /// §312g Abs. 2 Nr. 9 BGB — tarihli konaklamada <b>yasal cayma hakkı yoktur</b>, ama bunun
    /// <i>bildirilmesi</i> gerekir. Genel bir Widerrufsbelehrung gösterilmez: var olmayan bir
    /// hakkı anlatmak yanıltıcıdır.
    /// </summary>
    public static PublicWithdrawalRightResponse BuildWithdrawalRight(
        IReadOnlyDictionary<string, string> versions)
    {
        ArgumentNullException.ThrowIfNull(versions);

        return new PublicWithdrawalRightResponse
        {
            Applies = false,
            NoticeVersion = Version(versions, PublicLegalDocumentKeys.Withdrawal)
        };
    }

    private static string? Version(IReadOnlyDictionary<string, string> versions, string key) =>
        versions.TryGetValue(key, out var version) ? version : null;
}
