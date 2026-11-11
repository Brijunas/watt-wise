using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using NodaTime;

using WattWise.Application.Email;
using WattWise.Infrastructure.Email;
using WattWise.Infrastructure.IntegrationTests.Accounts;
using WattWise.Infrastructure.Persistence;

namespace WattWise.Infrastructure.IntegrationTests.Email;

public class EmailRequestsTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    private static CancellationToken Token => AccountsTestSupport.Token;

    [Fact]
    public async Task A_request_for_an_account_is_saved_pending_and_due_now()
    {
        await using ServiceProvider provider = fixture.BuildAccountServices(EmailLinks.NewClock());
        Guid accountId = await AccountsTestSupport.SaveAccountAsync(provider, Token);

        Guid id = await AddAsync(provider, requests => requests.Add(EmailRequestKind.EmailConfirmation, accountId));

        EmailRequest row = await ReadAsync(provider, id);
        Instant now = Instant.FromUtc(2026, 10, 26, 12, 0);
        Assert.Equal(
            (EmailRequestKind.EmailConfirmation, accountId, null, EmailRequestStatus.Pending, 0, now, now),
            (row.Kind, row.AccountId, row.Email, row.Status, row.AttemptCount, row.NextAttemptAt, row.CreatedAt));
    }

    [Fact]
    public async Task A_request_for_an_address_keeps_the_address_and_has_no_account()
    {
        await using ServiceProvider provider = fixture.BuildAccountServices(EmailLinks.NewClock());
        string email = AccountsTestSupport.UniqueEmail();

        Guid id = await AddAsync(provider, requests => requests.Add(EmailRequestKind.PasswordReset, email));

        EmailRequest row = await ReadAsync(provider, id);
        Assert.Equal((EmailRequestKind.PasswordReset, (Guid?)null, email), (row.Kind, row.AccountId, row.Email));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_request_for_a_blank_address_is_rejected(string? email)
    {
        await using ServiceProvider provider = fixture.BuildAccountServices(EmailLinks.NewClock());
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        IEmailRequests requests = scope.ServiceProvider.GetRequiredService<IEmailRequests>();

        Assert.ThrowsAny<ArgumentException>(() => requests.Add(EmailRequestKind.EmailConfirmation, email!));
    }

    private static async Task<Guid> AddAsync(ServiceProvider provider, Action<IEmailRequests> add)
    {
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        add(scope.ServiceProvider.GetRequiredService<IEmailRequests>());
        await db.SaveChangesAsync(Token);
        return db.ChangeTracker.Entries<EmailRequest>().Single().Entity.Id;
    }

    private static async Task<EmailRequest> ReadAsync(ServiceProvider provider, Guid id)
    {
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.EmailRequests.AsNoTracking().SingleAsync(request => request.Id == id, Token);
    }
}
