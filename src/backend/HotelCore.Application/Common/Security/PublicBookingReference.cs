using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace HotelCore.Application.Common.Security;

/// <summary>
/// Misafire gösterilen rezervasyon referansının biçimi: <b>Crockford Base32</b>, 12 karakter
/// (60 bit), tiresiz saklanır, <c>4-4-4</c> gruplu gösterilir (<c>K7QM-3XPD-9RTV</c>).
///
/// <para><b>Neden Crockford:</b> alfabesi <c>I</c>, <c>L</c>, <c>O</c>, <c>U</c> harflerini
/// içermez → <c>1/I</c> ve <c>0/O</c> karışmaz, telefonda hatasız dikte edilir, kazara küfür
/// üretmez ve büyük/küçük harf duyarsızdır.</para>
///
/// <para><b>Neden <c>RES-2026-00042</c> kullanılamaz:</b> sıralı ve tahmin edilebilirdir;
/// sorgulama anahtarı yapılırsa saldırgan tüm rezervasyonları sırayla dener.</para>
///
/// <para><b>Neden <c>Common/Security</c>'de:</b> referans hem misafir kanalında üretilir hem
/// <b>admin</b> tarafında gösterilir (<c>GET /reservations/{id}</c> → <c>publicReference</c>).
/// Biçimlendirmeyi public slice'ın içinde bırakmak, admin okuma yolunu public feature ad alanına
/// bağımlı yapardı; kural bu yüzden ortak ve tarafsız bir yerde durur.</para>
/// </summary>
public static class PublicBookingReference
{
    /// <summary>Referansın karakter uzunluğu (tiresiz).</summary>
    public const int Length = 12;

    /// <summary>Crockford Base32 alfabesi (<c>I</c>, <c>L</c>, <c>O</c>, <c>U</c> yoktur).</summary>
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    /// <summary>Kriptografik olarak güçlü yeni referans (60 bit), tiresiz.</summary>
    public static string New()
    {
        Span<byte> buffer = stackalloc byte[8];
        RandomNumberGenerator.Fill(buffer);

        // İlk 60 bit kullanılır; kalan 4 bit atılır (12 × 5 = 60).
        var value = BitConverter.ToUInt64(buffer) >> 4;

        Span<char> chars = stackalloc char[Length];
        for (var index = Length - 1; index >= 0; index--)
        {
            chars[index] = Alphabet[(int)(value & 0x1F)];
            value >>= 5;
        }

        return new string(chars);
    }

    /// <summary>Depolanan biçim (tiresiz) → gösterim biçimi (<c>4-4-4</c>).</summary>
    public static string Format(string stored)
    {
        ArgumentNullException.ThrowIfNull(stored);

        return stored.Length != Length
            ? stored
            : string.Create(CultureInfo.InvariantCulture, $"{stored[..4]}-{stored[4..8]}-{stored[8..]}");
    }

    /// <summary>
    /// Misafirin girdiği referansı arama biçimine indirger: büyük harf, <c>-</c> ve boşluk atılır,
    /// Crockford eşlemesi (<c>I/L → 1</c>, <c>O → 0</c>) uygulanır. Geçersiz biçimde <c>null</c>.
    /// </summary>
    public static string? Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var builder = new StringBuilder(Length);

        foreach (var character in raw)
        {
            if (character is '-' or ' ' or '\t')
            {
                continue;
            }

            var upper = char.ToUpperInvariant(character);
            var mapped = upper switch
            {
                'I' or 'L' => '1',
                'O' => '0',
                _ => upper
            };

            if (!Alphabet.Contains(mapped, StringComparison.Ordinal))
            {
                return null;
            }

            builder.Append(mapped);

            if (builder.Length > Length)
            {
                return null;
            }
        }

        return builder.Length == Length ? builder.ToString() : null;
    }
}
