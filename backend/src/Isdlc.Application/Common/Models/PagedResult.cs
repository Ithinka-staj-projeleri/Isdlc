namespace Isdlc.Application.Common.Models;

/// <summary>
/// Shared response shape for every list endpoint: { "items": [...], "totalCount": 42 }.
/// </summary>
public record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount);
