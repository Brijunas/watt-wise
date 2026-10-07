using Hangfire;

using WattWise.Application;
using WattWise.Infrastructure;
using WattWise.Infrastructure.Observability;
using WattWise.Jobs.Observability;
using WattWise.Jobs.Recurring;
using WattWise.Jobs.Storage;

var builder = WebApplication.CreateBuilder(args);
builder.AddObservability("wattwise-jobs");
builder.Services.AddInfrastructure();
builder.Services.AddApplication();
builder.Services.AddHangfireStorage();
builder.Services.AddHangfireServer((BackgroundJobServerOptions options) => options.WorkerCount = 5);
builder.Services.AddTransient<NoOpJob>();

var app = builder.Build();

app.UseJobsRequestLogging();
app.MapHangfireDashboard("/hangfire");
RecurringJobs.Register(app.Services.GetRequiredService<IRecurringJobManager>());

app.Run();
