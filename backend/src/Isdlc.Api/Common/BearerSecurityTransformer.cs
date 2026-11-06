using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Isdlc.Api.Common;

/// <summary>Adds the "Authorize" button to Swagger: paste the JWT from POST /api/v1/auth/login.</summary>
public class BearerSecurityTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(
        OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "POST /api/v1/auth/login ile alınan token'ı yapıştırın.",
        };

        document.Security ??= [];
        document.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", document)] = [],
        });
        document.SetReferenceHostDocument();

        return Task.CompletedTask;
    }
}