using Mediator;

using WattWise.Application.Results;

namespace WattWise.Application.Accounts;

public sealed class GetMeQueryHandler(IAccountsQueries queries) : IQueryHandler<GetMeQuery, Result<Me>>
{
    public async ValueTask<Result<Me>> Handle(GetMeQuery query, CancellationToken cancellationToken)
    {
        Me? me = await queries.GetMeAsync(query.AccountId, cancellationToken);
        if (me is null)
        {
            return AccountErrors.AccountNotFound();
        }

        return me;
    }
}
