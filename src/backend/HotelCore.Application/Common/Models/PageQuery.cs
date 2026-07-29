namespace HotelCore.Application.Common.Models;

/// <summary>
/// Ortak sayfalama parametreleri (<c>?page=1&amp;pageSize=20</c>). Sınırlar tek yerde tutulur ki
/// hiçbir endpoint yanlışlıkla tüm tabloyu döndürmesin.
/// </summary>
public sealed record PageQuery
{
    public const int DefaultPageSize = 20;

    public const int MaxPageSize = 200;

    private readonly int _page = 1;
    private readonly int _pageSize = DefaultPageSize;

    public int Page
    {
        get => _page;
        init => _page = value < 1 ? 1 : value;
    }

    public int PageSize
    {
        get => _pageSize;
        init => _pageSize = value switch
        {
            < 1 => DefaultPageSize,
            > MaxPageSize => MaxPageSize,
            _ => value
        };
    }

    /// <summary>EF Core <c>Skip()</c> için atlanacak öğe sayısı.</summary>
    public int Skip => (Page - 1) * PageSize;
}
