using Isdlc.Api.Common;
using Isdlc.Application.Documents;
using Isdlc.Application.Documents.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace Isdlc.Api.Controllers;

/// <summary>The Document Library: /api/v1/document-types</summary>
public class DocumentTypesController(IDocumentService documentService) : ApiControllerBase
{
    /// <summary>All tracked documents in stage order. Not paged: a short, fixed list.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<DocumentTypeDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<DocumentTypeDto>> List(CancellationToken cancellationToken)
    {
        return await documentService.ListTypesAsync(cancellationToken);
    }
}
