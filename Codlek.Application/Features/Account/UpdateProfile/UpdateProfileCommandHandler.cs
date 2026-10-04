using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Account;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Application.Interfaces;
using Codlek.Core.Enums;
using MediatR;

namespace Codlek.Application.Features.Account.UpdateProfile;

public sealed class UpdateProfileCommandHandler(
    IAccountRepository accounts,
    IUnitOfWork unitOfWork,
    ICurrentUser me)
    : IRequestHandler<UpdateProfileCommand, Result<AccountResponse>>
{
    public async Task<Result<AccountResponse>> Handle(
        UpdateProfileCommand command, CancellationToken cancellationToken)
    {
        var user = await accounts.FindAsync(me.TenantId, me.Id, cancellationToken);

        if (user is null)
            return Result.Failure<AccountResponse>(AccountErrors.SessionNoLongerValid);

        user.DisplayName = command.DisplayName.Trim();

        /*
          ⚠️ **ولا سطر تاني هنا.** الدور والشركة والكود والصلاحية
          والنسخة — مفيش ولا واحد فيهم بيتلمس. ده مش سهو، ده شكل
          الحماية: اللي مش مكتوب مايتغيّرش.
        */
        await unitOfWork.SaveChangesAsync(cancellationToken);

        /*
          ⚠️ **والاسم جوّه التوكن بيفضل القديم لحد التجديد الجايّ.**

          النظام القديم كان بيعيد إصدار الكوكي على طول، فالشريط العلوي
          بيتحدّث فوراً. التوكن ورقة موقّعة — ماينفعش يتعدّل.

          والرد ده بيحلّ المشكلة: الواجهة بتاخد الاسم الجديد من هنا
          وتعرضه. ولو اعتمدت على الادعاء اللي في التوكن، الاسم كان
          هيفضل قديم لـ١٥ دقيقة — <b>ودي حالة بتبان للمستخدم كأن
          الحفظ مانجحش</b>.
        */
        string tenantName = await accounts.TenantNameAsync(me.TenantId, cancellationToken);

        return Result.Success(new AccountResponse(
            user.Id, user.UserName ?? "", user.DisplayName, user.Code,
            user.Role.ToString(), UserRoleText.Arabic(user.Role), tenantName,
            user.IsActive, user.SuspendedReason, user.MustChangePassword,
            user.CreatedAtUtc, user.LastLoginUtc));
    }
}
