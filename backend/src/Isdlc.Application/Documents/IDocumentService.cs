using Isdlc.Application.Documents.Dtos;

namespace Isdlc.Application.Documents;

/// <summary>
/// Document tracking: the status of each tracked document in a project. Contract: docs/requirements-documents-api.md
/// The lists are short and fixed (one row per document type), so they are not paged.
/// </summary>
public interface IDocumentService
{
    /// <summary>Every tracked document with its state in the project, in stage order. 404 if the project is missing.</summary>
    Task<IReadOnlyList<ProjectDocumentDto>> ListAsync(int projectId, CancellationToken cancellationToken);

    /// <summary>
    /// Saves the document's status, note and link; creates the record the first time.
    /// 404 project or document type missing, 409 project not active.
    /// </summary>
    Task<ProjectDocumentDto> UpdateAsync(
        int projectId, int documentTypeId, UpdateProjectDocumentDto request, CancellationToken cancellationToken);

    /// <summary>The Document Library: all tracked documents in stage order.</summary>
    Task<IReadOnlyList<DocumentTypeDto>> ListTypesAsync(CancellationToken cancellationToken);
}
