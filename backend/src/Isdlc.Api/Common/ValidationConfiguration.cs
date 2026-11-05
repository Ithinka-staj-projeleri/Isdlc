using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;

namespace Isdlc.Api.Common;

/// <summary>
/// Makes automatic 400 validation responses follow AGENTS.md:
/// Turkish title and messages, and error keys that match the JSON field names (camelCase).
/// </summary>
public static class ValidationConfiguration
{
    public const string Title = "Girilen bilgiler geçersiz.";

    public static void Configure(MvcOptions options)
    {
        // Error keys use the JSON name: "expectedStepId", not "ExpectedStepId".
        options.ModelMetadataDetailsProviders.Add(new SystemTextJsonValidationMetadataProvider());

        // Turkish default messages for validation attributes that have no ErrorMessage of their own.
        options.ModelMetadataDetailsProviders.Add(new TurkishValidationMessages());

        // Messages for values that cannot be read at all (wrong type, missing body, ...).
        var messages = options.ModelBindingMessageProvider;
        messages.SetValueIsInvalidAccessor(_ => "Geçersiz değer.");
        messages.SetAttemptedValueIsInvalidAccessor((_, _) => "Geçersiz değer.");
        messages.SetNonPropertyAttemptedValueIsInvalidAccessor(_ => "Geçersiz değer.");
        messages.SetUnknownValueIsInvalidAccessor(_ => "Geçersiz değer.");
        messages.SetNonPropertyUnknownValueIsInvalidAccessor(() => "Geçersiz değer.");
        messages.SetValueMustBeANumberAccessor(_ => "Bir sayı girilmelidir.");
        messages.SetNonPropertyValueMustBeANumberAccessor(() => "Bir sayı girilmelidir.");
        messages.SetValueMustNotBeNullAccessor(_ => "Bu alan boş bırakılamaz.");
        messages.SetMissingBindRequiredValueAccessor(_ => "Bu alan zorunludur.");
        messages.SetMissingKeyOrValueAccessor(() => "Bu alan zorunludur.");
        messages.SetMissingRequestBodyRequiredValueAccessor(() => "İstek gövdesi boş olamaz.");
    }

    public static void ConfigureApiBehavior(ApiBehaviorOptions options)
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var factory = context.HttpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>();
            var problem = factory.CreateValidationProblemDetails(
                context.HttpContext,
                CleanErrors(context),
                statusCode: StatusCodes.Status400BadRequest,
                title: Title,
                detail: "Lütfen işaretli alanları düzeltin.");

            return new BadRequestObjectResult(problem)
            {
                ContentTypes = { "application/problem+json" },
            };
        };
    }

    /// <summary>
    /// Rewrites the raw model state into what the client can use:
    /// "$.expectedStepId" becomes "expectedStepId", empty messages get a Turkish text,
    /// and the extra "dto is required" error that follows unreadable JSON is dropped.
    /// </summary>
    private static ModelStateDictionary CleanErrors(ActionContext context)
    {
        var bodyParameterNames = context.ActionDescriptor.Parameters
            .Where(p => p.BindingInfo?.BindingSource == BindingSource.Body)
            .Select(p => p.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var errors = context.ModelState
            .Where(entry => entry.Value is { Errors.Count: > 0 })
            .ToList();

        // When the body could not be read, ASP.NET reports the real problem ("$.field")
        // and also the body parameter itself as missing. Keep only the real problem.
        var hasFieldErrors = errors.Any(entry => !bodyParameterNames.Contains(entry.Key));

        var cleaned = new ModelStateDictionary();
        foreach (var (key, entry) in errors)
        {
            if (hasFieldErrors && bodyParameterNames.Contains(key))
            {
                continue;
            }

            // "$" is the whole body (broken JSON); "$.field" is one field that could not be read.
            var cleanKey = key == "$" ? string.Empty : key.StartsWith("$.") ? key[2..] : key;
            var fallbackMessage = key == "$" ? "İstek gövdesi okunamadı." : "Geçersiz değer.";
            foreach (var error in entry!.Errors)
            {
                cleaned.AddModelError(cleanKey,
                    string.IsNullOrEmpty(error.ErrorMessage) ? fallbackMessage : error.ErrorMessage);
            }
        }

        return cleaned;
    }

    /// <summary>
    /// Fills in a Turkish message only where the developer did not write one.
    /// </summary>
    private sealed class TurkishValidationMessages : IValidationMetadataProvider
    {
        public void CreateValidationMetadata(ValidationMetadataProviderContext context)
        {
            foreach (var attribute in context.ValidationMetadata.ValidatorMetadata.OfType<ValidationAttribute>())
            {
                if (HasOwnMessage(attribute))
                {
                    continue;
                }

                // {1} and {2} are the attribute's own limits (max/min length, range).
                attribute.ErrorMessage = attribute switch
                {
                    RequiredAttribute => "Bu alan zorunludur.",
                    StringLengthAttribute { MinimumLength: > 0 } => "{2} ile {1} karakter arasında olmalıdır.",
                    StringLengthAttribute => "En fazla {1} karakter olabilir.",
                    MaxLengthAttribute => "En fazla {1} karakter olabilir.",
                    MinLengthAttribute => "En az {1} karakter olmalıdır.",
                    RangeAttribute => "{1} ile {2} arasında olmalıdır.",
                    EmailAddressAttribute => "Geçerli bir e-posta adresi girilmelidir.",
                    _ => "Geçersiz değer.",
                };
            }
        }

        /// <summary>
        /// Some attributes (EmailAddress, Phone, Url) report their built-in English text as ErrorMessage,
        /// so compare with a fresh instance to tell it apart from a message the developer wrote.
        /// </summary>
        private static bool HasOwnMessage(ValidationAttribute attribute)
        {
            if (attribute.ErrorMessageResourceName is not null)
            {
                return true;
            }
            if (attribute.ErrorMessage is null)
            {
                return false;
            }

            var type = attribute.GetType();
            var builtIn = type.GetConstructor(Type.EmptyTypes) is null
                ? null
                : ((ValidationAttribute)Activator.CreateInstance(type)!).ErrorMessage;
            return attribute.ErrorMessage != builtIn;
        }
    }
}
