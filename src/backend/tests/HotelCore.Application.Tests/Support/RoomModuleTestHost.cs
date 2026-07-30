using HotelCore.Application.Common.Localization;
using HotelCore.Domain.Entities;
using HotelCore.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HotelCore.Application.Tests.Support;

/// <summary>
/// Oda yonetimi handler testleri icin kendi kendine yeten mini host.
/// <para>
/// Ortak altyapi (SQLite <c>:memory:</c>, gercek <c>AppDbContext</c>, <c>AddApplication()</c>
/// boru hatti) <see cref="ApplicationTestHost"/> icindedir; burada yalnizca
/// oda modulunun verisi ve yardimcilari bulunur.
/// </para>
/// </summary>
internal sealed class RoomModuleTestHost : ApplicationTestHost
{
    private RoomModuleTestHost()
    {
    }

    /// <summary>A oteli — testlerin varsayilan aktif oteli.</summary>
    public Guid HotelId { get; private set; }

    /// <summary>B oteli — tenant izolasyonu ve "baska otele ait kayit" senaryolari icin.</summary>
    public Guid OtherHotelId { get; private set; }

    /// <summary>A otelindeki hazir oda tipi (<c>DBL</c>).</summary>
    public Guid RoomTypeId { get; private set; }

    /// <summary>B otelindeki hazir oda tipi (kod <c>TWN</c>; A otelinde bu kod yoktur).</summary>
    public Guid OtherHotelRoomTypeId { get; private set; }

    /// <summary>Iki otelin bagli oldugu Head Office.</summary>
    public Guid HeadOfficeId { get; private set; }

    public static async Task<RoomModuleTestHost> CreateAsync()
    {
        var host = new RoomModuleTestHost();

        try
        {
            await host.InitialiseAsync();

            return host;
        }
        catch
        {
            await host.DisposeAsync();
            throw;
        }
    }

    /// <summary>Oda ekler. <paramref name="isDeleted"/> ile "zaten silinmis" satir kurulabilir.</summary>
    public async Task<Room> AddRoomAsync(
        Guid hotelId,
        Guid roomTypeId,
        string number,
        int floor = 1,
        HousekeepingStatus status = HousekeepingStatus.Clean,
        bool isOutOfOrder = false,
        string? note = null,
        bool isDeleted = false)
    {
        var room = new Room
        {
            HotelId = hotelId,
            RoomTypeId = roomTypeId,
            Number = number,
            Floor = floor,
            HousekeepingStatus = status,
            IsOutOfOrder = isOutOfOrder,
            Note = note,
            IsDeleted = isDeleted,
            DeletedAt = isDeleted ? Clock.UtcNow : null
        };

        Database.Rooms.Add(room);
        await SaveAndDetachAsync();

        return room;
    }

    public async Task<RoomType> AddRoomTypeAsync(
        Guid hotelId,
        string code,
        string name,
        decimal basePrice = 100m,
        int capacity = 2,
        string? amenities = null)
    {
        var roomType = new RoomType
        {
            HotelId = hotelId,
            Code = code,
            Name = name,
            BasePrice = basePrice,
            Capacity = capacity,
            Amenities = amenities
        };

        Database.RoomTypes.Add(roomType);
        await SaveAndDetachAsync();

        return roomType;
    }

    /// <summary>Odaya rezervasyon (ve misafir) ekler — silme kuralini denemek icin.</summary>
    public async Task<Reservation> AddReservationAsync(
        Guid hotelId,
        Guid roomId,
        DateOnly checkIn,
        DateOnly checkOut,
        ReservationStatus status)
    {
        var guest = new Guest
        {
            HotelId = hotelId,
            FirstName = "Test",
            LastName = "Guest"
        };

        var reservation = new Reservation
        {
            HotelId = hotelId,
            RoomId = roomId,
            GuestId = guest.Id,
            ReservationNumber = $"R-{Guid.NewGuid().ToString("N")[..8]}",
            CheckIn = checkIn,
            CheckOut = checkOut,
            Status = status
        };

        Database.Guests.Add(guest);
        Database.Reservations.Add(reservation);
        await SaveAndDetachAsync();

        return reservation;
    }

    /// <summary>
    /// Rezervasyona <b>yururlukteki belge</b> niteliginde bir fatura baglar: numarali,
    /// <c>IssuedAt</c> damgali, iptal edilmemis ve storno degil.
    /// <para>
    /// "Faturalanmis" tanimi budur (<c>InvoiceEffectiveness</c>); oda silme kurali buna bakar:
    /// faturalanmamis rezervasyonu olan oda silinemez (GoBD / AO §147 — kayit erisilebilir
    /// kalmalidir). Taslak fatura yeterli DEGILDIR, cunku taslak henuz belge degildir.
    /// </para>
    /// </summary>
    public async Task<Invoice> AddIssuedInvoiceAsync(Reservation reservation)
    {
        ArgumentNullException.ThrowIfNull(reservation);

        var invoice = new Invoice
        {
            HotelId = reservation.HotelId,
            GuestId = reservation.GuestId,
            ReservationId = reservation.Id,
            Currency = "EUR"
        };

        // GoBD guard'i yalnizca Modified/Deleted'i engeller; Added bir Finalized fatura yazilabilir.
        invoice.MarkFinalized($"T-{Guid.NewGuid().ToString("N")[..8]}", Clock.UtcNow);

        Database.Invoices.Add(invoice);
        await SaveAndDetachAsync();

        return invoice;
    }

    /// <summary>Oda tipi icin dinamik icerik cevirisi ekler (architecture.md §4.6).</summary>
    public async Task AddTranslationAsync(Guid roomTypeId, string culture, string field, string text)
    {
        Database.Translations.Add(new Translation
        {
            EntityType = TranslationEntityTypes.RoomType,
            EntityId = roomTypeId,
            Culture = culture,
            Field = field,
            Text = text
        });

        await SaveAndDetachAsync();
    }

    /// <summary>Global query filter'i atlayarak ham oda satirini okur (silinmisler dahil).</summary>
    public Task<Room?> FindRoomIncludingDeletedAsync(Guid id) =>
        Database.Rooms.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(room => room.Id == id);

    /// <summary>Global query filter'i atlayarak ham oda tipi satirini okur.</summary>
    public Task<RoomType?> FindRoomTypeIncludingDeletedAsync(Guid id) =>
        Database.RoomTypes.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(type => type.Id == id);

    protected override async Task SeedAsync()
    {
        var headOffice = new HeadOffice { BrandName = "HotelCore Test", DefaultCulture = "de" };
        var hotelA = NewHotel(headOffice.Id, "Test Hotel A");
        var hotelB = NewHotel(headOffice.Id, "Test Hotel B");

        Database.HeadOffices.Add(headOffice);
        Database.Hotels.Add(hotelA);
        Database.Hotels.Add(hotelB);
        await SaveAndDetachAsync();

        HeadOfficeId = headOffice.Id;
        HotelId = hotelA.Id;
        OtherHotelId = hotelB.Id;

        RoomTypeId = (await AddRoomTypeAsync(HotelId, "DBL", "Doppelzimmer", basePrice: 120m)).Id;
        OtherHotelRoomTypeId = (await AddRoomTypeAsync(OtherHotelId, "TWN", "Zweibettzimmer B")).Id;

        // Varsayilan baglam: A otelinin kullanicisi (Head Office bypass'i kapali).
        CurrentUser.HotelId = HotelId;
        CurrentUser.HeadOfficeId = HeadOfficeId;
    }

    private static Hotel NewHotel(Guid headOfficeId, string name) => new()
    {
        HeadOfficeId = headOfficeId,
        Name = name,
        City = "Berlin",
        Country = Country.DE,
        Currency = "EUR",
        DefaultCulture = "de",
        TaxProfile = new TaxProfile { VatRate = 19m, ReducedVatRate = 7m }
    };
}
