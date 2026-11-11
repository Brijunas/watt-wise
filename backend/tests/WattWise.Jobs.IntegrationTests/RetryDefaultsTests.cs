using Microsoft.Extensions.DependencyInjection;

using WattWise.Application.Email;

namespace WattWise.Jobs.IntegrationTests;

[Collection(JobsHostCollection.Name)]
public class RetryDefaultsTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    [Theory]
    [InlineData(EmailRequestKind.PasswordReset, 4, "0,1,5,15")]
    [InlineData(EmailRequestKind.EmailConfirmation, 6, "0,1,5,15,60,240")]
    [InlineData(EmailRequestKind.AccountAlreadyExists, 3, "0,5,30")]
    public async Task The_shipped_appsettings_give_each_kind_its_attempts_and_delays_in_minutes(EmailRequestKind kind, int attempts, string delayMinutes)
    {
        await using TestHostFactory<Program> factory = new(JobsTestSettings.For(fixture));
        using HttpClient client = factory.CreateClient();

        EmailRetrySchedule schedule = factory.Services.GetRequiredService<IEmailRetrySettings>().For(kind);

        Assert.Equal((attempts, delayMinutes), (schedule.Attempts, string.Join(",", schedule.Delays.Select(delay => delay.TotalMinutes))));
    }
}
