using Isdlc.Application.Search.Dtos;

namespace Isdlc.Application.Search;

/// <summary>
/// Genel arama (Faz 5 · F7): projects, improvement proposals and the records inside projects, from one search box.
/// Contract: docs/search-api.md.
/// </summary>
public interface ISearchService
{
    /// <summary>Up to 5 matches of each kind; the text is trimmed and must be 2–100 characters.</summary>
    Task<SearchResultDto> SearchAsync(string? text, CancellationToken cancellationToken);
}
