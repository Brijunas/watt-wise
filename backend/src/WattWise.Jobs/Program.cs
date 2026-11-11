using Hangfire;

using Mediator;

using WattWise.Application;
using WattWise.Application.Behaviors;
using WattWise.Application.Email;
using WattWise.Infrastructure;
using WattWise.Infrastructure.Email;
using WattWise.Infrastructure.Observability;
using WattWise.Jobs.Observability;
using WattWise.Jobs.Recurring;
using WattWise.Jobs.Storage;

var builder = WebApplication.CreateBuilder(args);
builder.AddObservability("wattwise-jobs");
builder.Services.AddInfrastructure();
// The Jobs host is the only one that sends email, so it stops at start without these settings.
builder.Services.AddOptions<SmtpSettings>().ValidateOnStart();
builder.Services.AddOptions<LinkSettings>().ValidateOnStart();
foreach (EmailRequestKind kind in Enum.GetValues<EmailRequestKind>())
{
    builder.Services.AddOptions<EmailRetrySettings>(kind.ToString()).ValidateOnStart();
}

builder.Services.AddApplication();
// Keep the behavior list identical to PipelineHost in Application.Tests (the generator needs a literal list at each site).
builder.Services.AddMediator((MediatorOptions options) =>
{
    options.Assemblies = [typeof(ApplicationAssembly)];
    options.PipelineBehaviors = [typeof(LoggingBehavior<,>), typeof(ValidationBehavior<,>)];
    options.ServiceLifetime = ServiceLifetime.Scoped;
});
builder.Services.AddHangfireStorage();
// The recurring-job scheduler polls on SchedulePollingInterval (default 15 s) and would miss the 5-second send job.
builder.Services.AddHangfireServer((BackgroundJobServerOptions options) =>
{
    options.WorkerCount = 5;
    options.SchedulePollingInterval = TimeSpan.FromSeconds(5);
});
builder.Services.AddTransient<NoOpJob>();
builder.Services.AddTransient<SendEmailsJob>();
builder.Services.AddTransient<PurgeEmailRequestsJob>();
builder.Services.AddTransient<PurgeSessionsJob>();

var app = builder.Build();

app.UseJobsRequestLogging();
app.MapHangfireDashboard("/hangfire");
RecurringJobs.Register(app.Services.GetRequiredService<IRecurringJobManager>());

app.Run();
