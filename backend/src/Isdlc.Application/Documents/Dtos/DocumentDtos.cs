using System.ComponentModel.DataAnnotations;
using Isdlc.Application.Common.Models;
using Isdlc.Domain.Documents;

namespace Isdlc.Application.Documents.Dtos;

/// <summary>A tracked document, as shown inside a project's document row.</summary>
public record DocumentTypeSummaryDto(int Id, string Code, string Name, int StageOrder, string StageShortTitle);

/// <summary>One row of the Document Library (GET /api/v1/document-types).</summary>
public record DocumentTypeDto(
    int Id, string Code, string Name, int StageOrder, string StageShortTitle, string? Description);

/// <summary>
/// A document in a project. A document never saved on the project is NotStarted with no note, link or user.
/// UpdatedBy / UpdatedAt: who saved it last and when.
/// </summary>
public record ProjectDocumentDto(
    DocumentTypeSummaryDto DocumentType,
    DocumentStatus Status,
    string? Note,
    string? Link,
    UserSummaryDto? UpdatedBy,
    DateTime? UpdatedAt);

/// <summary>PUT /api/v1/projects/{projectId}/documents/{documentTypeId}</summary>
public record UpdateProjectDocumentDto : IValidatableObject
{
    [Required(ErrorMessage = "Durum zorunludur.")]
    public DocumentStatus? Status { get; init; }

    [MaxLength(1000, ErrorMessage = "Not en fazla 1000 karakter olabilir.")]
    public string? Note { get; init; }

    /// <summary>Where the document is kept; files are not uploaded.</summary>
    [MaxLength(1000, ErrorMessage = "Bağlantı en fazla 1000 karakter olabilir.")]
    public string? Link { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!string.IsNullOrWhiteSpace(Link) && !IsWebAddress(Link.Trim()))
        {
            yield return new ValidationResult(
                "Bağlantı http:// ya da https:// ile başlayan bir adres olmalıdır.", [nameof(Link)]);
        }
    }

    private static bool IsWebAddress(string text) =>
        Uri.TryCreate(text, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
