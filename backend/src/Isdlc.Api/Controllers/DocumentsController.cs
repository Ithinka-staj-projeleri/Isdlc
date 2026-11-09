using Isdlc.Api.Common;
using Isdlc.Application.Documents;
using Isdlc.Application.Documents.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace Isdlc.Api.Controllers;

/// <summary>
/// The project's documents: /api/v1/projects/{projectId}/documents.
/// It is a sub-resource of projects, so the routes are written in full ("/" ignores the base route).
/// </summary>
public class DocumentsController(IDocumentService documentService) : ApiControllerBase
{
    private const string Route = "/api/v1/projects/{projectId:int}/documents";

    /// <summary>Every tracked document with its state in the project, in stage order. Not paged: one row per document.</summary>
    [HttpGet(Route)]
    [ProducesResponseType<IReadOnlyList<ProjectDocumentDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IReadOnlyList<ProjectDocumentDto>> List(int projectId, CancellationToken cancellationToken)
    {
        return await documentService.ListAsync(projectId, cancellationToken);
    }

    /// <summary>Saves the document's status, note and link (creates the record the first time).</summary>
    [HttpPut(Route + "/{documentTypeId:int}")]
    [ProducesResponseType<ProjectDocumentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ProjectDocumentDto> Update(
        int projectId, int documentTypeId, UpdateProjectDocumentDto request, CancellationToken cancellationToken)
    {
        return await documentService.UpdateAsync(projectId, documentTypeId, request, cancellationToken);
    }
}
