using Mediator;

using Microsoft.AspNetCore.Diagnostics.HealthChecks;

using Scalar.AspNetCore;

using WattWise.Api.Cors;
using WattWise.Api.Endpoints;
using WattWise.Api.ErrorHandling;
using WattWise.Api.Health;
using WattWise.Api.Observability;
using WattWise.Api.OpenApi;
using WattWise.Application;
using WattWise.Application.Behaviors;
using WattWise.Infrastructure;
using WattWise.Infrastructure.Observability;
using WattWise.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);
builder.AddObservability("wattwise-api");
builder.Services.AddInfrastructure();
builder.Services.AddApplication();
// Keep the behavior list identical to PipelineHost in Application.Tests (the generator needs a literal list at each site).
builder.Services.AddMediator((MediatorOptions options) =>
{
    options.Assemblies = [typeof(ApplicationAssembly)];
    options.PipelineBehaviors = [typeof(LoggingBehavior<,>), typeof(ValidationBehavior<,>)];
    options.ServiceLifetime = ServiceLifetime.Scoped;
});
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = ProblemDetailsCustomization.Apply);
builder.Services.AddApiCors();
builder.Services.AddApiOpenApi();
builder.Services.AddEndpointModules();
builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>("database");

var app = builder.Build();

// Outermost, so the request line records the final status after the exception handler and status code pages.
app.UseApiRequestLogging();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors();
app.MapHealthChecks("/health", new HealthCheckOptions { ResponseWriter = HealthResponseWriter.WriteAsync });

// The OpenAPI document and Scalar UI are only served outside Production.
if (!app.Environment.IsProduction())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapEndpointModules();

app.Run();
