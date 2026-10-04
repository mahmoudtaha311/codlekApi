using System.Security.Claims;
using Codlek.Core.Enums;
using Microsoft.AspNetCore.Authorization;

namespace Codlek.Api.Authorization;

/// <summary>
/// سياسات الصلاحيات — <b>منقولة من المشروع القديم بالحرف</b>.
///
/// <para>🔴 <b>كل سياسة عضوية صريحة في قايمة، مش مقارنة بترتيب
/// الأدوار.</b> أرقام <see cref="UserRole"/> مالهاش ترتيب
/// (<c>Accountant = 4</c> أكبر من <c>Owner = 2</c>)، فأي
/// <c>&gt;=</c> هيدّي صلاحيات مالهاش معنى.</para>
/// </summary>
public static class Policies
{
    public const string ManagerOrAbove = "ManagerOrAbove";
    public const string OwnerOnly = "OwnerOnly";
    public const string RepairsViewer = "RepairsViewer";
    public const string RepairApprover = "RepairApprover";

    /// <summary>تسليم اللابات — مدير الدور والمالك بس.</summary>
    public const string HandoverAllowed = "HandoverAllowed";

    public static AuthorizationOptions AddCodlekPolicies(this AuthorizationOptions options)
    {
        options.AddPolicy(ManagerOrAbove, p =>
            p.RequireClaim(ClaimTypes.Role,
                nameof(UserRole.Manager), nameof(UserRole.Owner),
                nameof(UserRole.FloorManager)));

        options.AddPolicy(OwnerOnly, p =>
            p.RequireClaim(ClaimTypes.Role, nameof(UserRole.Owner)));

        /*
          🔴 **المحاسب بيشوف الصيانة — ومش بيشوف حاجة تانية.**

          ⚠️ القايمة دي **نفس أعضاء ManagerOrAbove زيادة المحاسب**. أي
          نقص هنا بيخسّر المديرين وصولهم لصفحة الصيانة من غير ما حد
          ياخد باله.
        */
        options.AddPolicy(RepairsViewer, p =>
            p.RequireClaim(ClaimTypes.Role,
                nameof(UserRole.Manager), nameof(UserRole.Owner),
                nameof(UserRole.FloorManager), nameof(UserRole.Accountant)));

        // 🔴 **والموافقة للمحاسب والمالك بس — مش للمدير.**
        options.AddPolicy(RepairApprover, p =>
            p.RequireClaim(ClaimTypes.Role,
                nameof(UserRole.Accountant), nameof(UserRole.Owner)));

        // 🔴 **والتسليم لمدير الدور والمالك بس — مش لمدير المخزن.**
        options.AddPolicy(HandoverAllowed, p =>
            p.RequireClaim(ClaimTypes.Role,
                nameof(UserRole.FloorManager), nameof(UserRole.Owner)));

        return options;
    }
}
