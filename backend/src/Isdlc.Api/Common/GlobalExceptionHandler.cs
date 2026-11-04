using Isdlc.Application.Common.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

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
            _ => (StatusCodes.Status500InternalServerError, "Beklenmeyen bir hata oluştu",
                "Lütfen daha sonra tekrar deneyin. Sorun sürerse sistem yöneticisine bildirin."),
        };

        if (status == StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception for {Method} {Path}",
                httpContext.Request.Method, httpContext.Request.Path);
        }

        httpContext.Response.StatusCode = status;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = detail,
            },
        });
    }
}
