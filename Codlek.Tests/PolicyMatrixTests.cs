using System.Security.Claims;
using Codlek.Api.Authorization;
using Codlek.Core.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Codlek.Tests;

/// <summary>
/// مصفوفة الصلاحيات — <b>منقولة من فحوص المشروع القديم</b>.
///
/// <para>🔴 <b>الفحوص دي بتقيس السياسات نفسها، مش النقط.</b> هناك
/// <c>AccountantAccessTests</c> بيشغّل سيرفر كامل ويسجّل دخول بأربع
/// حسابات ويضرب ٢٠ مسار. هنا نفس المصفوفة بتتفحص على كائن
/// <c>IAuthorizationService</c> — فأي تضييق غلط بيبان قبل ما أي
/// كنترولر يتكتب.</para>
///
/// <para>⚠️ <b>والسبب إن ده مهم:</b> سياسة <c>RepairApprover</c>
/// اتعملت عشان الموافقة تبقى عند حد واحد. ولو بقى أعضاؤها نفس
/// <c>RepairsViewer</c>، الدور الجديد يبقى مالوش أي معنى — وصاحب
/// الشغل طلبه بالظبط عشان كده.</para>
/// </summary>
public class PolicyMatrixTests
{
    private static readonly IAuthorizationService Authorization = Build();

    private static IAuthorizationService Build() =>
        new ServiceCollection()
            .AddLogging()
            .AddAuthorizationCore(o => o.AddCodlekPolicies())
            .BuildServiceProvider()
            .GetRequiredService<IAuthorizationService>();

    /// <summary>
    /// مستخدم بالدور ده — <b>بنفس الادعاء اللي الإصدار بيكتبه</b>.
    ///
    /// <para>⚠️ <c>ClaimTypes.Role</c> واسم الـenum بالحرف. أي اختلاف
    /// هنا بيخلّي الفحص يقيس حاجة تانية خالص.</para>
    /// </summary>
    private static ClaimsPrincipal As(UserRole role) =>
        new(new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, role.ToString())], "test"));

    private static async Task<bool> Allowed(UserRole role, string policy) =>
        (await Authorization.AuthorizeAsync(As(role), null, policy)).Succeeded;

    // =================================================================
    //  ManagerOrAbove — الحاجز العام
    // =================================================================

    [Theory]
    [InlineData(UserRole.Owner, true)]
    [InlineData(UserRole.Manager, true)]
    [InlineData(UserRole.FloorManager, true)]
    [InlineData(UserRole.Accountant, false)]
    [InlineData(UserRole.Technician, false)]
    public async Task ManagerOrAbove_membership_is_frozen(UserRole role, bool allowed) =>
        Assert.Equal(allowed, await Allowed(role, Policies.ManagerOrAbove));

    // =================================================================
    //  OwnerOnly
    // =================================================================

    [Theory]
    [InlineData(UserRole.Owner, true)]
    [InlineData(UserRole.Manager, false)]
    [InlineData(UserRole.FloorManager, false)]
    [InlineData(UserRole.Accountant, false)]
    [InlineData(UserRole.Technician, false)]
    public async Task OwnerOnly_membership_is_frozen(UserRole role, bool allowed) =>
        Assert.Equal(allowed, await Allowed(role, Policies.OwnerOnly));

    // =================================================================
    //  RepairsViewer — القراية
    // =================================================================

    /// <summary>
    /// 🔴 <b>المحاسب بيشوف الصيانة — ومش بيشوف حاجة تانية.</b>
    ///
    /// <para>⚠️ والقايمة دي <b>نفس أعضاء <c>ManagerOrAbove</c> زيادة
    /// المحاسب</b>. أي نقص هنا بيخسّر المديرين وصولهم لصفحة الصيانة
    /// من غير ما حد ياخد باله — والفحص اللي بيقيس المحاسب بس
    /// مابيقولش حاجة عن ده.</para>
    /// </summary>
    [Theory]
    [InlineData(UserRole.Owner, true)]
    [InlineData(UserRole.Manager, true)]
    [InlineData(UserRole.FloorManager, true)]
    [InlineData(UserRole.Accountant, true)]
    [InlineData(UserRole.Technician, false)]
    public async Task RepairsViewer_membership_is_frozen(UserRole role, bool allowed) =>
        Assert.Equal(allowed, await Allowed(role, Policies.RepairsViewer));

    /// <summary>
    /// 🔴 <b>كل عضو في <c>ManagerOrAbove</c> لازم يبقى في
    /// <c>RepairsViewer</c>.</b>
    ///
    /// <para>ده الفحص اللي بيمنع «المديرين خسروا صفحة الصيانة» —
    /// وهو بيشتغل حتى لو حد زوّد دور جديد.</para>
    /// </summary>
    [Fact]
    public async Task Everyone_who_manages_can_also_read_repairs()
    {
        foreach (var role in Enum.GetValues<UserRole>())
            if (await Allowed(role, Policies.ManagerOrAbove))
                Assert.True(
                    await Allowed(role, Policies.RepairsViewer),
                    $"{role} في ManagerOrAbove ومش في RepairsViewer");
    }

    // =================================================================
    //  RepairApprover — القرار
    // =================================================================

    /// <summary>
    /// 🔴 <b>الموافقة للمحاسب والمالك بس — مش للمدير.</b>
    ///
    /// <para>والمالك موجود لأن شركة محاسبها موقوف بتفضل أوامرها
    /// معلّقة للأبد ومفيش حد يقدر يفكّها.</para>
    /// </summary>
    [Theory]
    [InlineData(UserRole.Accountant, true)]
    [InlineData(UserRole.Owner, true)]
    [InlineData(UserRole.Manager, false)]
    [InlineData(UserRole.FloorManager, false)]
    [InlineData(UserRole.Technician, false)]
    public async Task RepairApprover_membership_is_frozen(UserRole role, bool allowed) =>
        Assert.Equal(allowed, await Allowed(role, Policies.RepairApprover));

    /// <summary>
    /// 🔴 <b>السياستين مش نفس الأعضاء — ولازم يفضلوا مختلفين.</b>
    ///
    /// <para>لو بقوا متطابقين، الدور الجديد يبقى مالوش أي معنى.
    /// والمدير بالتحديد لازم يشوف الطابور ومايقررش فيه.</para>
    /// </summary>
    [Fact]
    public async Task The_manager_reads_the_queue_but_cannot_decide()
    {
        Assert.True(await Allowed(UserRole.Manager, Policies.RepairsViewer));
        Assert.False(await Allowed(UserRole.Manager, Policies.RepairApprover));
    }

    // =================================================================
    //  RepairAssigner — الإسناد
    // =================================================================

    /// <summary>
    /// 🔴 <b>الإسناد للمديرين والمحاسب.</b> قرار المالك (٥ أكتوبر):
    /// المحاسب بيوافق وبيرفض <b>وبيسند لفنيين الصيانة</b>. القديم كان
    /// الإسناد فيه للمديرين بس — ده فرق مقصود ومكتوب في <c>CUTOVER.md</c>.
    ///
    /// <para>⚠️ والفني لأ: الإسناد قرار على شغل غيره.</para>
    /// </summary>
    [Theory]
    [InlineData(UserRole.Owner, true)]
    [InlineData(UserRole.Manager, true)]
    [InlineData(UserRole.FloorManager, true)]
    [InlineData(UserRole.Accountant, true)]
    [InlineData(UserRole.Technician, false)]
    public async Task RepairAssigner_membership_is_frozen(UserRole role, bool allowed) =>
        Assert.Equal(allowed, await Allowed(role, Policies.RepairAssigner));

    /// <summary>
    /// ⚠️ <b>المحاسب بيسند — بس مابيفتحش ولا بيلغي.</b> الإسناد بس هو
    /// اللي اتفتح له، والإلغاء فضل على <c>ManagerOrAbove</c>.
    /// </summary>
    [Fact]
    public async Task The_accountant_assigns_but_does_not_cancel()
    {
        Assert.True(await Allowed(UserRole.Accountant, Policies.RepairAssigner));
        Assert.False(await Allowed(UserRole.Accountant, Policies.ManagerOrAbove));
    }

    // =================================================================
    //  HandoverAllowed
    // =================================================================

    /// <summary>
    /// 🔴 <b>التسليم لمدير الدور والمالك بس — مش لمدير المخزن.</b>
    ///
    /// <para>الطلب كان صريح: الميزة دي هي <b>الزيادة</b> اللي مدير
    /// الدور عنده.</para>
    /// </summary>
    [Theory]
    [InlineData(UserRole.FloorManager, true)]
    [InlineData(UserRole.Owner, true)]
    [InlineData(UserRole.Manager, false)]
    [InlineData(UserRole.Accountant, false)]
    [InlineData(UserRole.Technician, false)]
    public async Task HandoverAllowed_membership_is_frozen(UserRole role, bool allowed) =>
        Assert.Equal(allowed, await Allowed(role, Policies.HandoverAllowed));

    // =================================================================
    //  ملاحظة على التركيب
    // =================================================================

    /// <summary>
    /// 🔴 <b>الفحص ده بيوثّق فخ في ASP.NET، مش بيقيس كود عندنا.</b>
    ///
    /// <para>سياسة على الكنترولر وسياسة على النقطة <b>بيتجمعوا
    /// (AND)</b> — مش بيتبدلوا. فلو <c>RepairsController</c> اتحطّ
    /// عليه <c>ManagerOrAbove</c> للسهولة، المحاسب ياخد <c>403</c>
    /// على طابور الصيانة اللي هو أصلاً موجود عشانه — ومفيش سياسة على
    /// النقطة تقدر تفكّ ده.</para>
    ///
    /// <para>⚠️ <b>فالكنترولر لازم يبقى <c>[Authorize]</c> بس</b>،
    /// وكل نقطة شايلة سياستها. والفحص هنا بيثبّت إن مفيش دور واحد
    /// بيعدّي الاتنين — يعني الجمع هيقفل فعلاً.</para>
    /// </summary>
    [Fact]
    public async Task Combining_manager_and_approver_locks_out_everyone_who_matters()
    {
        foreach (var role in Enum.GetValues<UserRole>())
        {
            bool manages = await Allowed(role, Policies.ManagerOrAbove);
            bool approves = await Allowed(role, Policies.RepairApprover);

            // المالك هو الوحيد اللي في الاتنين — وده مش كفاية:
            // المحاسب لازم يقرّر، وهو مش في ManagerOrAbove.
            if (role == UserRole.Accountant) Assert.False(manages && approves);
        }

        Assert.False(await Allowed(UserRole.Accountant, Policies.ManagerOrAbove));
        Assert.True(await Allowed(UserRole.Accountant, Policies.RepairApprover));
    }
}
