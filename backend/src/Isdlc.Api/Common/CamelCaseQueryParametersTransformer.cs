using System.Text.Json;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Isdlc.Api.Common;

/// <summary>
/// Shows query parameters in camelCase in the OpenAPI document (?page=1&amp;pageSize=20),
/// like the JSON fields. Binding ignores case, so this only fixes what Swagger shows.
/// </summary>
public class CamelCaseQueryParametersTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        foreach (var parameter in operation.Parameters?.OfType<OpenApiParameter>() ?? [])
        {
            if (parameter.In == ParameterLocation.Query && parameter.Name is not null)
            {
                parameter.Name = JsonNamingPolicy.CamelCase.ConvertName(parameter.Name);
            }
        }
        return Task.CompletedTask;
    }
}
