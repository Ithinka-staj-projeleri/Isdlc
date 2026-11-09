using System.ComponentModel.DataAnnotations;
using Isdlc.Application.Documents.Dtos;
using Isdlc.Domain.Documents;

namespace Isdlc.Application.Tests.Documents;

/// <summary>The link rule on the request body; no database needed.</summary>
public class UpdateProjectDocumentDtoTests
{
    private static List<ValidationResult> Validate(string? link)
    {
        var dto = new UpdateProjectDocumentDto { Status = DocumentStatus.InProgress, Link = link };
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(dto, new ValidationContext(dto), results, validateAllProperties: true);
        return results;
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("https://sirket.sharepoint.com/sites/isdlc/pkd.docx")]
    [InlineData("http://intranet/dokuman")]
    public void Validate_EmptyOrWebAddress_IsValid(string? link)
    {
        Assert.Empty(Validate(link));
    }

    [Theory]
    [InlineData("sirket.sharepoint.com/pkd")]
    [InlineData("C:\\Dokumanlar\\pkd.docx")]
    [InlineData("ftp://sunucu/pkd")]
    [InlineData("javascript:alert(1)")]
    public void Validate_NotAWebAddress_ReturnsErrorOnLink(string link)
    {
        var error = Assert.Single(Validate(link));

        Assert.Equal([nameof(UpdateProjectDocumentDto.Link)], error.MemberNames);
    }
}
