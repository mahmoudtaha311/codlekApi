using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Users;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Auth;
using Codlek.Core.Entities.Auth;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace Codlek.Application.Features.Users.ResetUserPassword;

/// <summary>
/// المدير بيعيّن باسورد جديد لحساب تاني.
///
/// <para>🔴 <b>بتحطّ <c>MustChangePassword</c>.</b> اللي بيعمل إعادة
/// التعيين بيعرف الباسورد — هو اللي كتبه وبيسلّمه بإيده. من غير
/// الإجبار، الباسورد ده بيفضل شغّال شهور وحساب المستخدم عملياً مشترك
/// بينه وبين مديره.</para>
/// </summary>
public sealed class ResetUserPasswordCommandHandler(
    IUserAccountRepository users,
    UserManager<ApplicationUser> identity,
    ILoginSessions sessions,
    IAuditTrail audit,
    IUnitOfWork unitOfWork,
    ICurrentUser me)
    : IRequestHandler<ResetUserPasswordCommand, Result<UserAccountResult>>
{
    public async Task<Result<UserAccountResult>> Handle(
        ResetUserPasswordCommand command, CancellationToken cancellationToken)
    {
        var target = await users.FindAsync(me.TenantId, command.Id, cancellationToken);

        if (target is null) return Result.Failure<UserAccountResult>(UserErrors.NotFound);

        if (!UserManagementRules.CanManage(me.Role, me.Id, target.Id, target.Role))
            return Result.Failure<UserAccountResult>(
                UserErrors.Forbidden(UserManagementRules.DenialReason(me.Id, target.Id)));

        /*
          ⚠️ **المدير مايعرفش باسورد الحساب التاني**، فـ
          `ChangePasswordAsync` (اللي بتطلب القديم) مش مناسبة.
          `RemovePassword` + `AddPassword` بتعمل نفس الحاجة وبتعدّي
          على نفس قواعد القوة ونفس البصمة المسجَّلة.
        */
        await identity.RemovePasswordAsync(target);
        var added = await identity.AddPasswordAsync(target, command.Password);

        if (!added.Succeeded)
        {
            string reason = string.Join(" ", added.Errors.Select(e => e.Description));
            return Result.Failure<UserAccountResult>(UserErrors.PasswordRejected(reason));
        }

        target.MustChangePassword = true;

        /*
          🔴 **الزيادة دي هي اللي بتقفل الجلسة المفتوحة.**

          من غيرها شاشة الدخول بترفض الباسورد القديم — فاللي عمل إعادة
          التعيين يشوف إنها اشتغلت — والتاب المفتوح عند صاحب الحساب
          يفضل ماشي بسلطة الباسورد القديم، وإجبار تغيير الباسورد
          مايوصلوش أصلاً.
        */
        target.CredentialVersion++;

        await identity.UpdateAsync(target);

        // ⚠️ وكل جلساته في الجدول بتتقفل كمان — الصف الملغي بيقول
        // «اتقفلت ليه وإمتى»، والنسخة لوحدها مابتسجّلش حاجة.
        await sessions.EndAllAsync(
            target.Id, "إعادة تعيين كلمة المرور", cancellationToken);

        audit.Record(
            AuditActions.WebUserPasswordReset, "User", target.Id, target.Code,
            $"اتغيّرت كلمة مرور «{target.DisplayName}»");

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(
            new UserAccountResult(target.Id, target.DisplayName, target.Code));
    }
}
