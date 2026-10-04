using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Account;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Enums;
using MediatR;

namespace Codlek.Application.Account.GetAccount;

public sealed class GetAccountQueryHandler(IAccountRepository accounts, ICurrentUser me)
    : IRequestHandler<GetAccountQuery, Result<AccountResponse>>
{
    public async Task<Result<AccountResponse>> Handle(
        GetAccountQuery query, CancellationToken cancellationToken)
    {
        var user = await accounts.FindAsync(me.TenantId, me.Id, cancellationToken);

        if (user is null)
            return Result.Failure<AccountResponse>(AccountErrors.SessionNoLongerValid);

        string tenantName = await accounts.TenantNameAsync(me.TenantId, cancellationToken);

        return Result.Success(new AccountResponse(
            user.Id,
            user.UserName ?? "",
            user.DisplayName,
            user.Code,
            user.Role.ToString(),
            UserRoleText.Arabic(user.Role),
            tenantName,
            user.IsActive,
            user.SuspendedReason,
            user.MustChangePassword,
            user.CreatedAtUtc,
            user.LastLoginUtc));
    }
}
