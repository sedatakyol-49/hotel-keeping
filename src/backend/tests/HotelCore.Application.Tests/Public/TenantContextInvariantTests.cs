using AwesomeAssertions;
using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Security;
using HotelCore.Application.Tests.Support;

namespace HotelCore.Application.Tests.Public;

/// <summary>
/// <see cref="ITenantContext"/> sözleşme testleri.
///
/// <para><b>Değişmez:</b> <c>Source == PublicChannel ⇒ HotelId != null &amp;&amp;
/// !CanAccessAllHotels</c>. Bu iki koşuldan biri bozulursa public bir istek ya hiçbir şey görür
/// ya <b>her şeyi</b> görür; ikisi de kabul edilemez, bu yüzden kural bir yorum değil bir
/// testtir (architecture-public-booking.md §4.2).</para>
/// </summary>
public sealed class TenantContextInvariantTests
{
    /// <summary>Yansıma ile ulaşılan iç tip: kural test edilecek olan implementasyondadır.</summary>
    private static ITenantContext Create(ICurrentUser currentUser, PublicTenantScope scope)
    {
        var type = typeof(PublicTenantScope).Assembly
            .GetType("HotelCore.Application.Common.Security.TenantContext", throwOnError: true)!;

        return (ITenantContext)Activator.CreateInstance(type, currentUser, scope)!;
    }

    [Fact]
    public void An_unscoped_anonymous_request_sees_nothing()
    {
        var context = Create(new TestCurrentUser { IsAuthenticated = false, HotelId = null }, new PublicTenantScope());

        context.Source.Should().Be(TenantScopeSource.None);
        context.HotelId.Should().BeNull();
        context.CanAccessAllHotels.Should().BeFalse();
    }

    [Fact]
    public void An_authenticated_request_keeps_the_existing_behaviour()
    {
        var hotelId = Guid.NewGuid();
        var currentUser = new TestCurrentUser { HotelId = hotelId, IsAuthenticated = true };

        var context = Create(currentUser, new PublicTenantScope());

        context.Source.Should().Be(TenantScopeSource.Authenticated);
        context.HotelId.Should().Be(hotelId);
        context.CanAccessAllHotels.Should().BeFalse();
    }

    [Fact]
    public void A_head_office_user_keeps_the_consolidated_bypass()
    {
        var currentUser = new TestCurrentUser
        {
            HotelId = null,
            CanAccessAllHotels = true,
            IsAuthenticated = true
        };

        var context = Create(currentUser, new PublicTenantScope());

        context.Source.Should().Be(TenantScopeSource.Authenticated);
        context.CanAccessAllHotels.Should().BeTrue("Head Office konsolide gorunumu DEGISMEZ");
    }

    [Fact]
    public void A_public_request_with_a_resolved_hotel_satisfies_the_invariant()
    {
        var hotelId = Guid.NewGuid();
        var scope = new PublicTenantScope();
        scope.Activate(hotelId, "berlin-mitte", "de");

        var context = Create(new TestCurrentUser { IsAuthenticated = false }, scope);

        context.Source.Should().Be(TenantScopeSource.PublicChannel);
        context.HotelId.Should().Be(hotelId);
        context.CanAccessAllHotels.Should().BeFalse();
    }

    [Fact]
    public void A_public_request_never_falls_back_to_the_authenticated_identity()
    {
        // Admin token tasiyan bir public istek: slug cozulemedi (otel yok).
        var adminHotelId = Guid.NewGuid();
        var currentUser = new TestCurrentUser
        {
            HotelId = adminHotelId,
            CanAccessAllHotels = true,
            IsAuthenticated = true
        };

        var scope = new PublicTenantScope();
        scope.MarkPublicRequest();

        var context = Create(currentUser, scope);

        // "Admin token + gecersiz slug = baska otelin verisi" yolu HIC acilmaz.
        context.HotelId.Should().BeNull();
        context.CanAccessAllHotels.Should().BeFalse();
        context.Source.Should().Be(TenantScopeSource.None);
    }

    [Fact]
    public void A_public_request_with_an_admin_token_is_still_scoped_by_the_path()
    {
        var pathHotelId = Guid.NewGuid();
        var currentUser = new TestCurrentUser
        {
            HotelId = Guid.NewGuid(),
            CanAccessAllHotels = true,
            IsAuthenticated = true
        };

        var scope = new PublicTenantScope();
        scope.Activate(pathHotelId, "berlin-mitte", "de");

        var context = Create(currentUser, scope);

        context.HotelId.Should().Be(pathHotelId, "otorite YOLDADIR, token'da degil");
        context.CanAccessAllHotels.Should().BeFalse();
        context.Source.Should().Be(TenantScopeSource.PublicChannel);
    }

    [Fact]
    public void Entering_a_hotel_scope_narrows_and_then_restores()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        var scope = new PublicTenantScope();
        scope.Activate(first, "a", "de");

        var context = Create(new TestCurrentUser { IsAuthenticated = false }, scope);

        using (scope.Enter(second))
        {
            context.HotelId.Should().Be(second);
            context.Source.Should().Be(TenantScopeSource.PublicChannel);
            context.CanAccessAllHotels.Should().BeFalse("daraltma HICBIR ZAMAN bypass acmaz");
        }

        context.HotelId.Should().Be(first, "Dispose onceki kapsami AYNEN geri koyar");
    }

    [Fact]
    public void The_scope_cannot_be_activated_twice_in_one_request()
    {
        var scope = new PublicTenantScope();
        scope.Activate(Guid.NewGuid(), "a", "de");

        var act = () => scope.Activate(Guid.NewGuid(), "b", "de");

        act.Should().Throw<InvalidOperationException>();
    }
}
