namespace HotelCore.Application.Common.Models;

/// <summary>
/// Sayfalı liste yanıtı. Şekil api-contracts.md "Sayfalama" bölümüyle BİREBİR aynıdır:
/// <c>{ items, page, pageSize, totalCount }</c>. Frontend bu alan adlarına bağlıdır —
/// yeni alan eklemeden önce sözleşme güncellenmelidir.
/// </summary>
/// <typeparam name="T">Öğe tipi (DTO).</typeparam>
public sealed record PagedResult<T>
{
    public PagedResult()
    {
    }

    public PagedResult(IReadOnlyList<T> items, int page, int pageSize, int totalCount)
    {
        Items = items;
        Page = page;
        PageSize = pageSize;
        TotalCount = totalCount;
    }

    /// <summary>Geçerli sayfadaki öğeler.</summary>
    public IReadOnlyList<T> Items { get; init; } = [];

    /// <summary>1 tabanlı sayfa numarası.</summary>
    public int Page { get; init; } = 1;

    /// <summary>Sayfa başına öğe sayısı.</summary>
    public int PageSize { get; init; }

    /// <summary>Filtreye uyan toplam öğe sayısı (sayfalama öncesi).</summary>
    public int TotalCount { get; init; }
}
