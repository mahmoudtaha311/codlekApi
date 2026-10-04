using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Users;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Auth;
using MediatR;

namespace Codlek.Application.Features.Users.SuspendUser;

/// <summary>
/// إيقاف حساب.
///
/// <para>🔴 <b>السبب إجباري، والمستخدم بيشوفه في شاشة الدخول.</b> من
/// غير السبب بيقف قدام رسالة مقفولة مش عارف يكلّم مين — فبيروح يجرّب
/// باسورد زميله، وده بالظبط اللي الإيقاف بيمنعه. <b>فالسبب مش توثيق،
/// هو جزء من الميزة نفسها.</b></para>
///
/// <para>⚠️ و«مين أوقفه وإمتى» بيتسجّلوا معاه: «موقوف» من غير اسم ولا
/// وقت بتخلّي فك الإيقاف قرار محدش مسؤول عنه.</para>
/// </summary>
public sealed class SuspendUserCommandHandler(
    IUserAccountRepository users,
    ILoginSessions sessions,
    IAuditTrail audit,
    IUnitOfWork unitOfWork,
    ICurrentUser me)
    : IRequestHandler<SuspendUserCommand, Result<UserAccountResult>>
{
    public async Task<Result<UserAccountResult>> Handle(
        SuspendUserCommand command, CancellationToken cancellationToken)
    {
        var target = await users.FindAsync(me.TenantId, command.Id, cancellationToken);

        if (target is null) return Result.Failure<UserAccountResult>(UserErrors.NotFound);

        if (!UserManagementRules.CanManage(me.Role, me.Id, target.Id, target.Role))
            return Result.Failure<UserAccountResult>(
                UserErrors.Forbidden(UserManagementRules.DenialReason(me.Id, target.Id)));

        string reason = command.Reason.Trim();

        if (reason.Length < UserManagementRules.MinSuspendReasonLength)
            return Result.Failure<UserAccountResult>(UserErrors.SuspendReasonRequired);

        /*
          ⚠️ **الخانة دي نص حر ومفيش عليها أي حد في الواجهة**، والمدير
          بيلزق فيها رسالة واتساب أو محضر. ٤٠٠ حرف عربي ≈ ٦٠ كلمة،
          يعني مش بعيد خالص — ومن غير الفحص ده الحساب مابيتوقفش
          والمدير بيشوف خطأ عام مش فاهم منه حاجة.
        */
        if (reason.Length > UserManagementRules.MaxSuspendReasonLength)
            return Result.Failure<UserAccountResult>(UserErrors.SuspendReasonTooLong);

        target.IsActive = false;
        target.SuspendedReason = reason;
        target.SuspendedByName = me.DisplayName;
        target.SuspendedAtUtc = DateTime.UtcNow;

        /*
          🔴 **الإيقاف لازم يقطع الجلسة المفتوحة، مش يمنع دخول جديد
          وبس.**

          السيناريو اللي الميزة موجودة عشانه هو لابتوب اتسرق أو موظف
          اتفصل — والاتنين التاب عندهم مفتوح. من غير الزيادة دي هو
          بيفضل يعمل حسابات فنيين ويصدّر المخزن كله بعد الإيقاف
          بساعات.
        */
        target.CredentialVersion++;

        await sessions.EndAllAsync(target.Id, $"إيقاف الحساب — {reason}", cancellationToken);

        audit.Record(
            AuditActions.WebUserSuspended, "User", target.Id, target.Code,
            $"اتوقف حساب «{target.DisplayName}» — {reason}");

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(
            new UserAccountResult(target.Id, target.DisplayName, target.Code));
    }
}
