namespace Isdlc.Application.Common.Models;

public static class PagingExtensions
{
    /// <summary>
    /// Returns one page of the query. Sort the query first, otherwise pages are not stable:
    /// <c>query.OrderBy(x => x.Id).ApplyPaging(pageRequest)</c>.
    /// </summary>
    public static IQueryable<T> ApplyPaging<T>(this IQueryable<T> query, PageRequest pageRequest) =>
        query
            .Skip((pageRequest.Page - 1) * pageRequest.PageSize)
            .Take(pageRequest.PageSize);
}
