using System.Security.Claims;
using System.Text.Json;
using Codlek.Api.Controllers;
using Codlek.Application.Contracts.Auth;
using Codlek.Core.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Codlek.Tests;

/// <summary>
/// <c>GET /api/v1/auth/me</c> — <b>شكل القديم بالحرف</b>.
///
/// <para>🔴 <b>واتلقط بتشغيل فحوص القديم على الجديد.</b> النسخة
/// السابقة كانت بترجّع ستة حقول من غير <c>username</c> ولا
/// <c>isOwner</c> ولا <c>isRepairApprover</c> — واللوحة بتفتح شاشاتها
/// بالأعلام دي. يعني بعد التحويل المالك كان هيلاقي شاشة المراجعة مقفولة
/// والمحاسب مش شايف زرار الموافقة، من غير ولا خطأ.</para>
/// </summary>
public class AuthMeTests
{
    private static MeResponse Me(UserRole role, bool mustChange = false)
    {
        var claims = new List<Claim> { new(ClaimTypes.Name, "owner.a") };

        if (mustChange) claims.Add(new Claim("must", "1"));

        var controller = new AuthController(sender: null!)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")),
                },
            },
        };

        var result = Assert.IsType<OkObjectResult>(controller.Me(new FakeCurrentUser(role)));

        return Assert.IsType<MeResponse>(result.Value);
    }

    /// <summary>
    /// 🔴 <b>الأسماء والترتيب زي القديم</b> — والزيادة الوحيدة في
    /// الآخر.
    /// </summary>
    [Fact]
    public void The_shape_is_the_legacy_one_plus_one_trailing_field()
    {
        var json = JsonSerializer.SerializeToElement(
            Me(UserRole.Owner), new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Equal(
            ["userId", "tenantId", "username", "displayName", "code", "role", "roleText",
             "isManagerOrAbove", "isOwner", "isRepairApprover", "mustChangePassword"],
            json.EnumerateObject().Select(p => p.Name));
    }

    [Fact]
    public void The_identity_comes_from_the_signed_in_user()
    {
        var me = Me(UserRole.Manager);

        Assert.Equal("owner.a", me.Username);
        Assert.Equal("كريم", me.DisplayName);
        Assert.Equal("U001", me.Code);
        Assert.Equal("Manager", me.Role);
        Assert.Equal(UserRoleText.Arabic(UserRole.Manager), me.RoleText);
        Assert.NotEqual(Guid.Empty, me.UserId);
        Assert.NotEqual(Guid.Empty, me.TenantId);
    }

    /// <summary>
    /// 🔴 <b>الأعلام نفس قواعد الحواجز بالحرف.</b> مدير الدور جوّه
    /// «مدير أو فوق»؛ المحاسب موافق ومش مدير؛ المالك الاتنين.
    /// </summary>
    [Theory]
    [InlineData(UserRole.Owner, true, true, true)]
    [InlineData(UserRole.Manager, true, false, false)]
    [InlineData(UserRole.FloorManager, true, false, false)]
    [InlineData(UserRole.Accountant, false, false, true)]
    [InlineData(UserRole.Technician, false, false, false)]
    public void The_flags_follow_the_role(
        UserRole role, bool managerOrAbove, bool owner, bool approver)
    {
        var me = Me(role);

        Assert.Equal(managerOrAbove, me.IsManagerOrAbove);
        Assert.Equal(owner, me.IsOwner);
        Assert.Equal(approver, me.IsRepairApprover);
    }

    [Fact]
    public void A_forced_password_change_is_reported()
    {
        Assert.True(Me(UserRole.Manager, mustChange: true).MustChangePassword);
        Assert.False(Me(UserRole.Manager).MustChangePassword);
    }
}
