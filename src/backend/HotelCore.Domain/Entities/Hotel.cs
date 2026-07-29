using HotelCore.Domain.Common;
using HotelCore.Domain.Enums;

namespace HotelCore.Domain.Entities;

/// <summary>
/// Fiziksel otel / şube — multi-tenant modelinde <b>tenant kökü</b>.
/// Kendisi <see cref="ITenantEntity"/> DEĞİLDİR; erişim <see cref="UserHotelAccess"/> ile yönetilir.
/// </summary>
public sealed class Hotel : EntityBase, IAuditableEntity, ISoftDeletable
{
    public Guid HeadOfficeId { get; set; }

    public HeadOffice HeadOffice { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    public Country Country { get; set; } = Country.DE;

    public string City { get; set; } = string.Empty;

    public string? AddressLine { get; set; }

    public string? PostalCode { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }

    /// <summary>Vergi numarası (DE: Steuernummer / USt-IdNr.) — fatura üstbilgisinde basılır.</summary>
    public string? TaxNumber { get; set; }

    public string DefaultCulture { get; set; } = "de";

    /// <summary>ISO 4217 para birimi kodu (EUR, TRY, CHF ...).</summary>
    public string Currency { get; set; } = "EUR";

    /// <summary>Owned type — bkz. <see cref="TaxProfile"/>.</summary>
    public TaxProfile TaxProfile { get; set; } = new();

    public ICollection<Department> Departments { get; } = [];

    public ICollection<RoomType> RoomTypes { get; } = [];

    public ICollection<Room> Rooms { get; } = [];

    public DateTimeOffset CreatedAt { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public DateTimeOffset? ModifiedAt { get; set; }

    public Guid? ModifiedByUserId { get; set; }

    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }
}
