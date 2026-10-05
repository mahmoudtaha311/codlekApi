using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Users;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Auth;
using MediatR;

namespace Codlek.Application.Features.Users.ActivateUser;

/// <summary>
/// رجوع الحساب للخدمة.
///
/// <para>⚠️ <b>بيفضّي بيانات الإيقاف كلها.</b> لو
/// <c>SuspendedReason</c> فضل مكتوب بعد التفعيل، شاشة الدخول (اللي
/// بتقرا العمود ده) بتفضل تعرض سبب إيقاف قديم لحساب شغّال — والمستخدم
/// بيدخل وهو شايف رسالة بتقوله إنه موقوف.</para>
/// </summary>
public sealed class ActivateUserCommandHandler(
    IUserAccountRepository users,
    IAuditTrail audit,
    IUnitOfWork unitOfWork,
    ICurrentUser me,
    IAccountStanding standing)
    : IRequestHandler<ActivateUserCommand, Result<UserAccountResult>>
{
    public async Task<Result<UserAccountResult>> Handle(
        ActivateUserCommand command, CancellationToken cancellationToken)
    {
        var target = await users.FindAsync(me.TenantId, command.Id, cancellationToken);

        if (target is null) return Result.Failure<UserAccountResult>(UserErrors.NotFound);

        if (!UserManagementRules.CanManage(me.Role, me.Id, target.Id, target.Role))
            return Result.Failure<UserAccountResult>(
                UserErrors.Forbidden(UserManagementRules.DenialReason(me.Id, target.Id)));

        target.IsActive = true;
        target.SuspendedReason = "";
        target.SuspendedByName = "";
        target.SuspendedAtUtc = null;

        /*
          ⚠️ **ومفيش زيادة في `CredentialVersion` هنا — زي القديم.**

          التفعيل بيفتح الباب، مابيقفلش حاجة. وزيادة النسخة كانت
          هتطرد الحساب من أجهزته — وهو أصلاً مطرود من وقت الإيقاف،
          فمفيش حاجة تتقفل.
        */
        audit.Record(
            AuditActions.WebUserActivated, "User", target.Id, target.Code,
            $"رجع حساب «{target.DisplayName}» للخدمة");

        await unitOfWork.SaveChangesAsync(cancellationToken);

        // 🔴 بعد الحفظ — شوف IAccountStanding.Forget.
        standing.Forget(target.Id);

        return Result.Success(
            new UserAccountResult(target.Id, target.DisplayName, target.Code));
    }
}
