using Mediator;

using WattWise.Api.Endpoints;
using WattWise.Api.ErrorHandling;
using WattWise.Application;
using WattWise.Application.Behaviors;
using WattWise.Infrastructure;
using WattWise.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);
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
builder.Services.AddEndpointModules();
builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.MapHealthChecks("/health");
app.MapEndpointModules();

app.Run();
