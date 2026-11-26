namespace Isdlc.Application.Common.Exceptions;

/// <summary>
/// Field errors a service finds after the DTO's attributes have passed (e.g. the company's own fields, which the DTO cannot
/// know). Returned as 400 in the same shape as the automatic validation errors: { title, errors: { "customValues.x": [...] } }.
/// Keys are JSON field paths; messages are shown to the user, so write them in Turkish.
/// </summary>
public class FieldValidationException(IReadOnlyDictionary<string, string[]> errors)
    : Exception("Girilen bilgiler geçersiz.")
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
}
