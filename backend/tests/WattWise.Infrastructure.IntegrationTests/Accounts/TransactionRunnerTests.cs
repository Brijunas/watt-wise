using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

using WattWise.Application;
using WattWise.Application.Results;
using WattWise.Domain.Accounts;
using WattWise.Infrastructure.Persistence;

namespace WattWise.Infrastructure.IntegrationTests.Accounts;

public class TransactionRunnerTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    private static CancellationToken Token => AccountsTestSupport.Token;

    private static async Task<long> AccountRowsAsync(ServiceProvider provider, Guid accountId) =>
        await AccountsTestSupport.CountAsync(provider, $"""SELECT count(*) AS "Value" FROM app.accounts WHERE id = {accountId}""", Token);

    /// <summary>Stages a new account and saves it; the work every test hands the runner.</summary>
    private static async Task SaveNewAccountAsync(AppDbContext db, Account account, CancellationToken token)
    {
        db.Accounts.Add(account);
        await db.SaveChangesAsync(token);
    }

    [Fact]
    public async Task A_successful_result_commits_what_the_work_saved_and_is_returned_as_it_is()
    {
        await using ServiceProvider provider = fixture.BuildAccountServices();
        Account account = AccountsTestSupport.NewAccount();
        Result<int> result;
        await using (AsyncServiceScope scope = provider.CreateAsyncScope())
        {
            AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            result = await scope.ServiceProvider.GetRequiredService<ITransactionRunner>().ExecuteAsync<int>(
                async token =>
                {
                    await SaveNewAccountAsync(db, account, token);
                    return 42;
                },
                Token);
        }

        Assert.Equal((42, 1L), (result.Value, await AccountRowsAsync(provider, account.Id)));
    }

    [Fact]
    public async Task A_failed_result_rolls_back_what_the_work_saved_and_is_returned_as_it_is()
    {
        await using ServiceProvider provider = fixture.BuildAccountServices();
        Account account = AccountsTestSupport.NewAccount();
        Result<int> result;
        await using (AsyncServiceScope scope = provider.CreateAsyncScope())
        {
            AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            result = await scope.ServiceProvider.GetRequiredService<ITransactionRunner>().ExecuteAsync<int>(
                async token =>
                {
                    await SaveNewAccountAsync(db, account, token);
                    return Error.BadRequest("Test.Failed", "Failed on purpose.");
                },
                Token);
        }

        Assert.Equal(("Test.Failed", 0L), (result.Error.Code, await AccountRowsAsync(provider, account.Id)));
    }

    [Fact]
    public async Task An_exception_rolls_back_what_the_work_saved_and_reaches_the_caller()
    {
        await using ServiceProvider provider = fixture.BuildAccountServices();
        Account account = AccountsTestSupport.NewAccount();
        Exception? thrown;
        await using (AsyncServiceScope scope = provider.CreateAsyncScope())
        {
            AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            thrown = await Record.ExceptionAsync(() => scope.ServiceProvider.GetRequiredService<ITransactionRunner>().ExecuteAsync<int>(
                async token =>
                {
                    await SaveNewAccountAsync(db, account, token);
                    throw new InvalidOperationException("Work failed.");
                },
                Token));
        }

        Assert.Equal(("Work failed.", 0L), (thrown?.Message, await AccountRowsAsync(provider, account.Id)));
    }

    [Fact]
    public async Task After_a_failed_result_the_scopes_change_tracker_is_empty()
    {
        await using ServiceProvider provider = fixture.BuildAccountServices();
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await scope.ServiceProvider.GetRequiredService<ITransactionRunner>().ExecuteAsync<int>(
            async token =>
            {
                await SaveNewAccountAsync(db, AccountsTestSupport.NewAccount(), token);
                return Error.BadRequest("Test.Failed", "Failed on purpose.");
            },
            Token);

        Assert.Empty(db.ChangeTracker.Entries());
    }

    [Fact]
    public async Task After_an_exception_the_scopes_change_tracker_is_empty()
    {
        await using ServiceProvider provider = fixture.BuildAccountServices();
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        _ = await Record.ExceptionAsync(() => scope.ServiceProvider.GetRequiredService<ITransactionRunner>().ExecuteAsync<int>(
            async token =>
            {
                await SaveNewAccountAsync(db, AccountsTestSupport.NewAccount(), token);
                throw new InvalidOperationException("Work failed.");
            },
            Token));

        Assert.Empty(db.ChangeTracker.Entries());
    }

    [Fact]
    public async Task Changes_staged_before_the_runner_starts_are_dropped_and_not_saved_with_the_work()
    {
        await using ServiceProvider provider = fixture.BuildAccountServices();
        Account staged = AccountsTestSupport.NewAccount();
        await using (AsyncServiceScope scope = provider.CreateAsyncScope())
        {
            AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Accounts.Add(staged);

            await scope.ServiceProvider.GetRequiredService<ITransactionRunner>().ExecuteAsync<int>(
                async token =>
                {
                    await SaveNewAccountAsync(db, AccountsTestSupport.NewAccount(), token);
                    return 1;
                },
                Token);
        }

        Assert.Equal(0L, await AccountRowsAsync(provider, staged.Id));
    }

    [Fact]
    public async Task Inside_an_open_transaction_a_failed_result_clears_the_change_tracker()
    {
        await using ServiceProvider provider = fixture.BuildAccountServices();
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await using IDbContextTransaction outer = await db.Database.BeginTransactionAsync(Token);

        await scope.ServiceProvider.GetRequiredService<ITransactionRunner>().ExecuteAsync<int>(
            async token =>
            {
                await SaveNewAccountAsync(db, AccountsTestSupport.NewAccount(), token);
                return Error.BadRequest("Test.Failed", "Failed on purpose.");
            },
            Token);

        Assert.Empty(db.ChangeTracker.Entries());
    }

    [Fact]
    public async Task Inside_an_open_transaction_a_successful_result_keeps_the_change_tracker()
    {
        await using ServiceProvider provider = fixture.BuildAccountServices();
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await using IDbContextTransaction outer = await db.Database.BeginTransactionAsync(Token);

        await scope.ServiceProvider.GetRequiredService<ITransactionRunner>().ExecuteAsync<int>(
            async token =>
            {
                await SaveNewAccountAsync(db, AccountsTestSupport.NewAccount(), token);
                return 1;
            },
            Token);

        Assert.Single(db.ChangeTracker.Entries());
    }

    [Fact]
    public async Task Inside_a_transaction_that_is_already_open_the_runner_neither_commits_nor_rolls_back_but_leaves_it_to_the_owner()
    {
        await using ServiceProvider provider = fixture.BuildAccountServices();
        Account account = AccountsTestSupport.NewAccount();
        await using (AsyncServiceScope scope = provider.CreateAsyncScope())
        {
            AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await using IDbContextTransaction outer = await db.Database.BeginTransactionAsync(Token);

            await scope.ServiceProvider.GetRequiredService<ITransactionRunner>().ExecuteAsync<int>(
                async token =>
                {
                    await SaveNewAccountAsync(db, account, token);
                    return 1;
                },
                Token);

            await outer.RollbackAsync(Token);
        }

        Assert.Equal(0L, await AccountRowsAsync(provider, account.Id));
    }
}
