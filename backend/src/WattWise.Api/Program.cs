using WattWise.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddInfrastructure();

var app = builder.Build();

app.Run();
