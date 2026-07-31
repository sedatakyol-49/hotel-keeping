using AwesomeAssertions;
using HotelCore.Application.Common.Security;

namespace HotelCore.Application.Tests.Public;

/// <summary>
/// Rezervasyon referansının biçim ve normalize kuralları (api-contracts-public-booking.md §7.4).
///
/// <para><b>Neden Crockford Base32:</b> alfabesi <c>I</c>, <c>L</c>, <c>O</c>, <c>U</c> içermez;
/// misafir telefonda referansı dikte ederken <c>0/O</c> ve <c>1/I</c> karışmaz. Normalize kuralı
/// bu karışmayı <b>affeder</b> (<c>O → 0</c>, <c>I/L → 1</c>) — aksi hâlde destek hattı doğru
/// referansı reddederdi.</para>
/// </summary>
public sealed class PublicBookingReferenceTests
{
    [Fact]
    public void A_new_reference_uses_only_the_crockford_alphabet()
    {
        for (var attempt = 0; attempt < 200; attempt++)
        {
            var reference = PublicBookingReference.New();

            reference.Should().HaveLength(12);
            reference.Should().MatchRegex("^[0-9A-HJKMNP-TV-Z]{12}$");
            reference.Should().NotContainAny("I", "L", "O", "U");
        }
    }

    [Fact]
    public void References_are_grouped_for_display_but_stored_without_hyphens()
    {
        var formatted = PublicBookingReference.Format("K7QM3XPD9RTV");

        formatted.Should().Be("K7QM-3XPD-9RTV");
        PublicBookingReference.Normalize(formatted).Should().Be("K7QM3XPD9RTV");
    }

    [Theory]
    [InlineData("k7qm-3xpd-9rtv", "K7QM3XPD9RTV")]
    [InlineData("K7QM 3XPD 9RTV", "K7QM3XPD9RTV")]
    [InlineData("K7QM3XPD9RTV", "K7QM3XPD9RTV")]
    public void Normalisation_is_case_and_separator_insensitive(string input, string expected) =>
        PublicBookingReference.Normalize(input).Should().Be(expected);

    [Theory]
    // O -> 0 ve I/L -> 1: misafirin okuma hatasi affedilir.
    [InlineData("O7QM3XPD9RTV", "07QM3XPD9RTV")]
    [InlineData("I7QM3XPD9RTV", "17QM3XPD9RTV")]
    [InlineData("L7QM3XPD9RTV", "17QM3XPD9RTV")]
    public void Confusable_characters_are_mapped(string input, string expected) =>
        PublicBookingReference.Normalize(input).Should().Be(expected);

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("too-short")]
    [InlineData("K7QM3XPD9RTVEXTRA")]
    [InlineData("K7QM3XPD9RT!")]
    public void An_invalid_reference_normalises_to_null(string? input) =>
        PublicBookingReference.Normalize(input).Should().BeNull();
}
