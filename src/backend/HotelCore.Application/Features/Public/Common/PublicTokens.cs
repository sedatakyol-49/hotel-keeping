using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using HotelCore.Application.Common.Security;

namespace HotelCore.Application.Features.Public.Common;

/// <summary>
/// Misafir kanalının kimlik üreteçleri ve özetleme yardımcıları
/// (api-contracts-public-booking.md §7.1).
///
/// <para><b>İki kimliğin rolleri ayrıdır:</b>
/// <list type="bullet">
///   <item><c>accessToken</c> bir <b>taşıyıcı kimlik bilgisidir</b>: tek başına okuma + iptal
///   yetkisi verir. Veritabanında yalnızca SHA-256 özeti saklanır (mevcut <c>RefreshToken</c>
///   deseni) ve karşılaştırma <b>sabit zamanlıdır</b>.</item>
///   <item><c>bookingReference</c> taşıyıcı kimlik bilgisi <b>değildir</b>: tek başına veri
///   döndürmez, yalnızca <c>lookup</c> ucunda e-postayla birlikte kullanılır.</item>
/// </list></para>
///
/// <para><b><c>RES-2026-00042</c> public tarafta ASLA kullanılmaz:</b> sıralı ve tahmin
/// edilebilirdir; sorgulama anahtarı yapılırsa saldırgan tüm rezervasyonları sırayla okur.</para>
/// </summary>
internal static class PublicTokens
{
    /// <summary>Hold token'ının ham bayt uzunluğu → base64url'de 22 karakter (128 bit).</summary>
    public const int HoldTokenBytes = 16;

    /// <summary>Erişim token'ının ham bayt uzunluğu → base64url'de 27 karakter (160 bit).</summary>
    public const int AccessTokenBytes = 20;

    /// <summary>Hold token'ının karakter uzunluğu (doğrulama için).</summary>
    public const int HoldTokenLength = 22;

    /// <summary>Erişim token'ının karakter uzunluğu.</summary>
    public const int AccessTokenLength = 27;

    /// <summary>Rezervasyon referansının karakter uzunluğu (tiresiz) — 12 × 5 bit = 60 bit.</summary>
    public const int BookingReferenceLength = PublicBookingReference.Length;

    /// <summary>Kriptografik olarak güçlü, URL güvenli token üretir.</summary>
    public static string NewUrlToken(int byteCount) => ToBase64Url(RandomNumberGenerator.GetBytes(byteCount));

    /// <summary>Yeni hold token'ı (128 bit).</summary>
    public static string NewHoldToken() => NewUrlToken(HoldTokenBytes);

    /// <summary>Yeni erişim token'ı (160 bit).</summary>
    public static string NewAccessToken() => NewUrlToken(AccessTokenBytes);

    /// <summary>
    /// Yeni rezervasyon referansı. Kural <see cref="PublicBookingReference"/>'tedir: aynı biçim
    /// admin tarafında da gösterilir, bu yüzden tek yerde durur.
    /// </summary>
    public static string NewBookingReference() => PublicBookingReference.New();

    /// <summary>Depolanan biçim (tiresiz, büyük harf) → gösterim biçimi (<c>4-4-4</c>).</summary>
    public static string FormatBookingReference(string stored) => PublicBookingReference.Format(stored);

    /// <summary>
    /// Misafirin girdiği referansı arama biçimine indirger. Geçersiz biçim <c>null</c> döner —
    /// <b>ama uç yine 202 verir</b> (varlık sızdırılmaz).
    /// </summary>
    public static string? NormalizeBookingReference(string? raw) => PublicBookingReference.Normalize(raw);

    /// <summary>Token'ın SHA-256 özeti (küçük harf hex) — veritabanına <b>yalnızca bu</b> yazılır.</summary>
    public static string Hash(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    /// <summary>
    /// E-posta hız sınırı anahtarı: <c>SHA-256(lower(trim(email)))</c>. <b>Ham e-posta hız sınırı
    /// deposunda saklanmaz.</b>
    /// </summary>
    public static string HashEmail(string email) =>
        Hash(email.Trim().ToLowerInvariant());

    /// <summary>
    /// Tuzlanmış istemci IP özeti — ham IP saklanmaz, özet başka veri kümeleriyle eşleştirilemez.
    /// <b>Tuz yapılandırılmamışsa özet hiç üretilmez</b> (<c>null</c>): tuzsuz bir SHA-256, IPv4
    /// uzayının küçüklüğü yüzünden kaba kuvvetle geri çözülebilir ve sahte bir gizlilik duygusu
    /// vermekten kötüsü yoktur.
    /// </summary>
    public static string? HashClientIp(string? clientIp, string? salt) =>
        string.IsNullOrWhiteSpace(clientIp) || string.IsNullOrWhiteSpace(salt)
            ? null
            : Hash(salt + "|" + clientIp);

    /// <summary>
    /// Sabit zamanlı karşılaştırma. Sıradan bir <c>==</c>, ilk farklı bayta kadar geçen süreyle
    /// token'ın ön ekini sızdırırdı.
    /// </summary>
    public static bool FixedTimeEquals(string left, string right) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(left),
            Encoding.UTF8.GetBytes(right));

    /// <summary>
    /// Alıcıyı maskeler (<c>juergen@example.de</c> → <c>j***@e***.de</c>). Onay yanıtı e-postayı
    /// tekrar tam olarak yazmaz. Kural <see cref="EmailMasking"/>'te tek yerdedir; gönderici
    /// implementasyonunun log'u da aynı maskeyi kullanır.
    /// </summary>
    public static string MaskEmail(string? email) => EmailMasking.Mask(email);

    /// <summary>Token biçim kontrolü: base64url alfabesi ve beklenen uzunluk.</summary>
    public static bool IsWellFormedUrlToken(string? token, int expectedLength)
    {
        if (string.IsNullOrEmpty(token) || token.Length != expectedLength)
        {
            return false;
        }

        foreach (var character in token)
        {
            var valid = char.IsAsciiLetterOrDigit(character) || character is '-' or '_';
            if (!valid)
            {
                return false;
            }
        }

        return true;
    }

    private static string ToBase64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
