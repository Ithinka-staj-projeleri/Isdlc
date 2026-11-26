using Isdlc.Application.Common.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Isdlc.Api.Common;

/// <summary>
/// Turns every unhandled exception into a ProblemDetails response.
/// Known exceptions keep their message; anything else becomes a generic 500.
/// </summary>
public class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title, detail) = exception switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, "Kayıt bulunamadı", exception.Message),
            BusinessRuleException => (StatusCodes.Status400BadRequest, "İşlem yapılamadı", exception.Message),
            FieldValidationException => (StatusCodes.Status400BadRequest, ValidationConfiguration.Title, exception.Message),
            ForbiddenException => (StatusCodes.Status403Forbidden, "Yetkiniz yok", exception.Message),
            ConflictException => (StatusCodes.Status409Conflict, "Kayıt güncel değil", exception.Message),
            ServiceUnavailableException => (StatusCodes.Status503ServiceUnavailable, "Servis kullanılamıyor", exception.Message),
            // RowVersion mismatch: someone else saved the same record after it was read.
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "Kayıt güncel değil",
                "Kayıt bu arada başka biri tarafından değiştirildi. Sayfayı yenileyin."),
            _ => (StatusCodes.Status500InternalServerError, "Beklenmeyen bir hata oluştu",
                "Lütfen daha sonra tekrar deneyin. Sorun sürerse sistem yöneticisine bildirin."),
        };

        if (status == StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception for {Method} {Path}",
                httpContext.Request.Method, httpContext.Request.Path);
        }

        httpContext.Response.StatusCode = status;

        // Field errors found by a service look exactly like the automatic validation errors (ValidationConfiguration).
        var problem = exception is FieldValidationException fieldErrors
            ? new ValidationProblemDetails(fieldErrors.Errors.ToDictionary(e => e.Key, e => e.Value))
            {
                Status = status,
                Title = ValidationConfiguration.Title,
                Detail = "Lütfen işaretli alanları düzeltin.",
            }
            : new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = detail,
            };

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problem,
        });
    }
}
