namespace HotelCore.Domain.Common;

/// <summary>
/// Otel (tenant) kapsamındaki entity'ler. <c>AppDbContext</c> bu arayüzü uygulayan her tipe
/// global query filter ile <c>HotelId</c> koşulunu otomatik ekler (bkz. architecture.md §3).
/// </summary>
public interface ITenantEntity
{
    Guid HotelId { get; set; }
}
