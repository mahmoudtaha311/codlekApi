using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Users;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Auth;
using Codlek.Core.Enums;
using MediatR;

namespace Codlek.Application.Features.Users.GetUsers;

public sealed class GetUsersQueryHandler(IUserAccountRepository users, ICurrentUser me)
    : IRequestHandler<GetUsersQuery, Result<IReadOnlyList<UserAccountItem>>>
{
    public async Task<Result<IReadOnlyList<UserAccountItem>>> Handle(
        GetUsersQuery query, CancellationToken cancellationToken)
    {
        var rows = await users.ListAsync(me.TenantId, cancellationToken);

        /*
          🔴 **`canManage` بيتحسب على السيرفر لكل صف.**

          الواجهة بتقفل الأزرار بيه. لو الواجهة حسبته لوحدها، القاعدة
          بتبقى متكتوبة في مكانين بلغتين، وأول تعديل في واحدة منهم
          بيخلّي الصفحة تعرض زرار بيرجّع 403 — أو أسوأ، تخفي زرار
          المستخدم من حقه يضغطه.

          ⚠️ **وده تسهيل مش فرض.** كل نقطة بتتأكد تاني بنفسها. اللي
          بيبعت طلب بإيده مش بيمرّ على الواجهة خالص.

          ⚠️ والحساب بيتعمل في الذاكرة لأن `CanManage` دالة C# مش
          قابلة للترجمة لـSQL. عدد مستخدمي الشركة عشرات مش آلاف.
        */
        var items = rows
            .Select(u => new UserAccountItem(
                Id: u.Id,
                Username: u.UserName ?? "",
                DisplayName: u.DisplayName,
                Code: u.Code,
                Role: u.Role.ToString(),
                RoleText: UserRoleText.Arabic(u.Role),
                IsActive: u.IsActive,
                SuspendedReason: u.SuspendedReason,
                SuspendedByName: u.SuspendedByName,
                SuspendedAtUtc: u.SuspendedAtUtc,
                MustChangePassword: u.MustChangePassword,
                CreatedAtUtc: u.CreatedAtUtc,
                LastLoginUtc: u.LastLoginUtc,
                CanManage: UserManagementRules.CanManage(me.Role, me.Id, u.Id, u.Role)))
            .ToList();

        return Result.Success<IReadOnlyList<UserAccountItem>>(items);
    }
}
