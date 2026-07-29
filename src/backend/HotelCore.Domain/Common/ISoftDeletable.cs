namespace HotelCore.Domain.Common;

/// <summary>
/// Yumuşak silme. Kayıt fiziksel olarak silinmez; GoBD saklama süresi (10 yıl) gereği
/// faturalar için zorunludur. Global query filter <c>!IsDeleted</c> koşulunu ekler.
/// </summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; set; }

    DateTimeOffset? DeletedAt { get; set; }
}
