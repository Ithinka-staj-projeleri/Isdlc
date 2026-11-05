namespace Isdlc.Application.Common.Models;

/// <summary>
/// Shared paging query for list endpoints: ?page=1&amp;pageSize=20.
/// Out-of-range values are clamped instead of failing the request.
/// Holds only what the client sends: every public property becomes a query parameter in Swagger.
/// Apply it to a query with <see cref="PagingExtensions.ApplyPaging{T}"/>.
/// </summary>
public record PageRequest
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    private readonly int _page = 1;
    private readonly int _pageSize = DefaultPageSize;

    public int Page
    {
        get => _page;
        init => _page = Math.Max(1, value);
    }

    public int PageSize
    {
        get => _pageSize;
        init => _pageSize = Math.Clamp(value, 1, MaxPageSize);
    }
}
