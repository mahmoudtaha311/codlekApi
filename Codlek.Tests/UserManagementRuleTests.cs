using Codlek.Core.Auth;
using Codlek.Core.Enums;

namespace Codlek.Tests;

/// <summary>
/// مين يقدر يتحكم في مين.
///
/// <para>🔴 <b>كل فحص هنا بيمنع حالة حقيقية، مش بيغطّي سطر.</b> الخلل
/// في القواعد دي مش بيبان كعطل — بيبان كمدير مخزن ترقّى نفسه لمالك،
/// أو كمالك قافل على نفسه الباب.</para>
/// </summary>
public class UserManagementRuleTests
{
    private static readonly Guid Me = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Someone = Guid.Parse("22222222-2222-2222-2222-222222222222");

    // =================================================================
    //  التحكم في حساب موجود
    // =================================================================

    /// <summary>
    /// 🔴 <b>المالك مايتحكمش في نفسه من الشاشة دي.</b>
    ///
    /// <para>من غير الشرط ده، المالك يقدر يوقف نفسه — والشركة تفضل من
    /// غير ولا حساب مالك يقدر يفكّ الإيقاف. <b>باب مقفول من جوّه
    /// مالوش مفتاح.</b></para>
    /// </summary>
    [Theory]
    [InlineData(UserRole.Owner)]
    [InlineData(UserRole.Manager)]
    [InlineData(UserRole.FloorManager)]
    [InlineData(UserRole.Accountant)]
    [InlineData(UserRole.Technician)]
    public void Nobody_can_manage_their_own_account(UserRole role) =>
        Assert.False(UserManagementRules.CanManage(role, Me, Me, role));

    [Fact]
    public void The_owner_can_manage_anyone_else()
    {
        foreach (var target in Enum.GetValues<UserRole>())
            Assert.True(UserManagementRules.CanManage(UserRole.Owner, Me, Someone, target));
    }

    /// <summary>
    /// 🔴 <b>مدير المخزن بيتحكم في الفنيين وبس.</b>
    ///
    /// <para>من غير القيد ده، أي مدير مخزن يقدر يوقف المالك.</para>
    /// </summary>
    [Theory]
    [InlineData(UserRole.Technician, true)]
    [InlineData(UserRole.Manager, false)]
    [InlineData(UserRole.Owner, false)]
    [InlineData(UserRole.FloorManager, false)]
    [InlineData(UserRole.Accountant, false)]
    public void The_warehouse_manager_only_manages_technicians(UserRole target, bool allowed) =>
        Assert.Equal(
            allowed,
            UserManagementRules.CanManage(UserRole.Manager, Me, Someone, target));

    /// <summary>
    /// ⚠️ <b>مدير الدور بيشوف القايمة ومابيعملش حاجة.</b>
    ///
    /// <para>هو داخل في سياسة <c>ManagerOrAbove</c> فبيوصل للنقط —
    /// والقفل جوّه كل نقطة. ده سلوك الصفحة القديمة بالظبط، وتغييره
    /// بيدّيه صلاحية إدارة حسابات محدش طلبها.</para>
    /// </summary>
    [Fact]
    public void The_floor_manager_can_see_but_not_act()
    {
        foreach (var target in Enum.GetValues<UserRole>())
            Assert.False(
                UserManagementRules.CanManage(UserRole.FloorManager, Me, Someone, target));
    }

    [Fact]
    public void A_technician_and_an_accountant_manage_nobody()
    {
        foreach (var role in new[] { UserRole.Technician, UserRole.Accountant })
            foreach (var target in Enum.GetValues<UserRole>())
                Assert.False(UserManagementRules.CanManage(role, Me, Someone, target));
    }

    // =================================================================
    //  إنشاء حساب
    // =================================================================

    [Fact]
    public void The_owner_can_create_any_role()
    {
        foreach (var wanted in Enum.GetValues<UserRole>())
            Assert.True(UserManagementRules.CanCreate(UserRole.Owner, wanted));
    }

    /// <summary>
    /// 🔴 <b>مدير المخزن بيضيف فنيين وبس — وده يمنع الترقية
    /// الذاتية.</b>
    ///
    /// <para>من غير القيد ده، أي مدير مخزن يعمل لنفسه حساب مالك تاني
    /// ويترقّى — وساعتها كل قاعدة تانية بتبقى شكلية.</para>
    /// </summary>
    [Theory]
    [InlineData(UserRole.Technician, true)]
    [InlineData(UserRole.Manager, false)]
    [InlineData(UserRole.Owner, false)]
    [InlineData(UserRole.FloorManager, false)]
    [InlineData(UserRole.Accountant, false)]
    public void The_warehouse_manager_can_only_create_technicians(
        UserRole wanted, bool allowed) =>
        Assert.Equal(allowed, UserManagementRules.CanCreate(UserRole.Manager, wanted));

    [Fact]
    public void Nobody_else_can_create_accounts()
    {
        foreach (var role in new[]
                 { UserRole.FloorManager, UserRole.Accountant, UserRole.Technician })
            foreach (var wanted in Enum.GetValues<UserRole>())
                Assert.False(UserManagementRules.CanCreate(role, wanted));
    }

    // =================================================================
    //  الرسالة
    // =================================================================

    /// <summary>
    /// ⚠️ <b>الرسالتين مختلفتين عن قصد.</b>
    ///
    /// <para>الفرق بين «ده حسابك إنت» و«ده أعلى من صلاحيتك» هو اللي
    /// بيخلّي المستخدم يعرف يروح لمين.</para>
    /// </summary>
    [Fact]
    public void The_denial_message_tells_the_user_what_to_do_next()
    {
        Assert.Contains("صفحة الحساب", UserManagementRules.DenialReason(Me, Me));
        Assert.Contains("المدير العام", UserManagementRules.DenialReason(Me, Someone));
    }

    // =================================================================
    //  الأطوال
    // =================================================================

    /// <summary>
    /// 🔴 <b>الأطوال دي = أطوال أعمدة القاعدة بالحرف.</b>
    ///
    /// <para>EF مابيشغّلش تحقق <c>DataAnnotations</c> وقت الحفظ —
    /// بيبعت القيمة زي ما هي وSQL Server بيرمي «String or binary data
    /// would be truncated». والاستثناء ده مش متمسك، فبيوصل للواجهة
    /// <b>٥٠٠</b> بدل رسالة، والإجراء مابيحصلش أصلاً.</para>
    /// </summary>
    [Fact]
    public void The_length_limits_match_the_database_columns()
    {
        Assert.Equal(60, UserManagementRules.MaxUsernameLength);
        Assert.Equal(120, UserManagementRules.MaxDisplayNameLength);
        Assert.Equal(400, UserManagementRules.MaxSuspendReasonLength);
    }

    [Fact]
    public void The_minimums_are_the_old_projects_minimums()
    {
        Assert.Equal(3, UserManagementRules.MinUsernameLength);
        Assert.Equal(2, UserManagementRules.MinDisplayNameLength);
        Assert.Equal(3, UserManagementRules.MinSuspendReasonLength);
    }
}
