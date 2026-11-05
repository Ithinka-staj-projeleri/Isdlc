using System.Text.Json.Serialization;
using Isdlc.Api.Common;
using Isdlc.Application;
using Isdlc.Infrastructure;
using Isdlc.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Errors: every error response uses the ProblemDetails format.
builder.Services.AddProblemDetails(options =>
    options.CustomizeProblemDetails = context =>
        context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier);
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// Routes: /api/v1/{controller in kebab-case}. JSON: enums as text.
// Validation errors (400): Turkish messages, camelCase field names (see ValidationConfiguration).
builder.Services
    .AddControllers(options =>
    {
        options.Conventions.Add(new RouteTokenTransformerConvention(new KebabCaseParameterTransformer()));
        ValidationConfiguration.Configure(options);
    })
    .ConfigureApiBehaviorOptions(ValidationConfiguration.ConfigureApiBehavior)
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        // Unreadable JSON gets our Turkish message instead of the serializer's English one.
        options.AllowInputFormatterExceptionMessages = false;
    });

// The OpenAPI document reads these options, so Swagger also shows enums as text.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi(options =>
    options.AddOperationTransformer<CamelCaseQueryParametersTransformer>());

var app = builder.Build();

await app.Services.InitializeDatabaseAsync(app.Environment.IsDevelopment());

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    // OpenAPI document: /openapi/v1.json, test page: /swagger
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "Isdlc API v1");
        options.DocumentTitle = "Isdlc API";
    });
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
