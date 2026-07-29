namespace HotelCore.Domain.Common;

/// <summary>
/// Denetim alanları. Değerler <c>AppDbContext.SaveChangesAsync</c> içinde
/// <c>ICurrentUser</c> + <c>IDateTimeProvider</c> kullanılarak doldurulur; el ile set edilmez.
/// </summary>
public interface IAuditableEntity
{
    DateTimeOffset CreatedAt { get; set; }

    Guid? CreatedByUserId { get; set; }

    DateTimeOffset? ModifiedAt { get; set; }

    Guid? ModifiedByUserId { get; set; }
}
