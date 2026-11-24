using System.ComponentModel.DataAnnotations;
using Isdlc.Api.Common;
using Isdlc.Application.Search;
using Isdlc.Application.Search.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace Isdlc.Api.Controllers;

/// <summary>/api/v1/search — the search box in the top bar (Faz 5 · F7). Contract: docs/search-api.md</summary>
public class SearchController(ISearchService searchService) : ApiControllerBase
{
    /// <summary>?q=portal: up to 5 projects, proposals, open issues, minutes, risks and requirements each.</summary>
    [HttpGet]
    [ProducesResponseType<SearchResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<SearchResultDto> Search(
        [FromQuery, Required(ErrorMessage = "En az 2 karakter girin."),
         MinLength(SearchService.MinLength, ErrorMessage = "En az 2 karakter girin."),
         MaxLength(SearchService.MaxLength, ErrorMessage = "En fazla 100 karakter olabilir.")]
        string q,
        CancellationToken cancellationToken)
    {
        return await searchService.SearchAsync(q, cancellationToken);
    }
}
