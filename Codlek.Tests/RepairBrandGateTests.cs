using Codlek.Core.Repairs;
using Codlek.Core.Text;

namespace Codlek.Tests;

/// <summary>
/// قيد الماركة على أوامر الصيانة.
///
/// <para>🔴 <b>الحالات دي منقولة من <c>BrandEnforcementTests</c> في
/// المشروع القديم.</b> هناك كل حالة بتشغّل سيرفر كامل وقاعدة بيانات؛
/// هنا نفس القواعد بتتفحص في مللي ثانية — فالتغطية بتفضل كاملة بدل
/// ما تتقلّص لحالة أو اتنين.</para>
/// </summary>
public class RepairBrandGateTests
{
    private static readonly Guid Hp = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Dell = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static readonly BrandRule[] Rules =
    [
        new(Hp, "HP", ["Hewlett-Packard", "HPQ"]),
        new(Dell, "Dell", ["Dell Inc."]),
    ];

    private static BrandResult Resolve(string manufacturer) =>
        BrandToken.Resolve(Rules, manufacturer);

    // =================================================================
    //  المسك
    // =================================================================

    /// <summary>🔴 فني متقيّد بـHP مايمسكش Dell.</summary>
    [Fact]
    public void A_technician_limited_to_hp_cannot_take_a_dell() =>
        Assert.False(RepairBrandGate.CanAssign([Hp], Resolve("Dell")));

    [Fact]
    public void A_technician_limited_to_hp_can_take_an_hp() =>
        Assert.True(RepairBrandGate.CanAssign([Hp], Resolve("HP")));

    /// <summary>
    /// 🔴 <b>والاسم البديل بيوصل لنفس الماركة.</b>
    ///
    /// <para>ودي الحاجة اللي قايمة الماركات موجودة عشانها: جدول
    /// الأجهزة فيه الاسم الخام زي ما ويندوز قاله، ونفس الشركة بتيجي
    /// <c>HP</c> و<c>Hewlett-Packard</c>.</para>
    /// </summary>
    [Theory]
    [InlineData("Hewlett-Packard")]
    [InlineData("HPQ")]
    [InlineData("hewlett packard")]
    public void An_alias_resolves_to_the_same_brand(string manufacturer) =>
        Assert.True(RepairBrandGate.CanAssign([Hp], Resolve(manufacturer)));

    /// <summary>
    /// 🔴 <b>فني من غير ماركات بيمسك أي حاجة.</b>
    ///
    /// <para>القايمة الفاضية معناها «مفيش قيد»، مش «ممنوع من كل
    /// حاجة». ولو اتفهمت غلط، أول فني بيتعمل مايقدرش يمسك ولا
    /// لاب.</para>
    /// </summary>
    [Theory]
    [InlineData("HP")]
    [InlineData("Dell")]
    [InlineData("Toshiba")]
    public void A_technician_with_no_brands_takes_anything(string manufacturer) =>
        Assert.True(RepairBrandGate.CanAssign([], Resolve(manufacturer)));

    /// <summary>
    /// 🔴 <b>والماركة المش معروفة مابتتمنعش.</b>
    ///
    /// <para>القايمة مُدارة بالإيد، فلاب بماركة جديدة لسه مش مكتوبة
    /// مالوش ذنب. ولو اتمنع، كل شحنة فيها ماركة جديدة بتقف لحد ما
    /// صاحب الشغل يضيفها — وده مش اللي القيد موجود عشانه.</para>
    ///
    /// <para>⚠️ واللي بيحمي ده هو شاشة «الماركات المش معروفة»:
    /// بتوري الأسماء دي بعددها عشان تتضاف بضغطة.</para>
    /// </summary>
    [Fact]
    public void An_unknown_manufacturer_is_not_blocked() =>
        Assert.True(RepairBrandGate.CanAssign([Hp], Resolve("Toshiba")));

    [Fact]
    public void A_device_with_no_manufacturer_is_not_blocked() =>
        Assert.True(RepairBrandGate.CanAssign([Hp], Resolve("")));

    [Fact]
    public void A_technician_with_two_brands_takes_both()
    {
        Assert.True(RepairBrandGate.CanAssign([Hp, Dell], Resolve("HP")));
        Assert.True(RepairBrandGate.CanAssign([Hp, Dell], Resolve("Dell")));
    }

    // =================================================================
    //  تعدية المحاسب
    // =================================================================

    /// <summary>
    /// 🔴 <b>المحاسب بيعدّي القاعدة.</b>
    ///
    /// <para>صاحب الشغل قرّر إن المحاسب «يوافق <b>ويغيّر الفني</b>»
    /// في نفس الخطوة — فلو القيد منعه، شطر من قراره بيختفي.</para>
    ///
    /// <para>⚠️ <b>والتعدية لازم تتسجّل.</b> تعدية مابتتسجّلش معناها
    /// إن القاعدة مالهاش أي معنى — والتسجيل مسؤولية المنادي، مش
    /// الدالة دي.</para>
    /// </summary>
    [Fact]
    public void The_approver_can_assign_outside_the_brand() =>
        Assert.True(RepairBrandGate.CanAssign([Hp], Resolve("Dell"), approverOverride: true));

    /// <summary>⚠️ والتعدية مابتغيّرش حاجة لو الماركة مسموحة أصلاً.</summary>
    [Fact]
    public void The_override_changes_nothing_when_the_brand_was_already_allowed() =>
        Assert.True(RepairBrandGate.CanAssign([Hp], Resolve("HP"), approverOverride: true));

    // =================================================================
    //  القفل
    // =================================================================

    /// <summary>
    /// 🔴 <b>قفل الأمر عمره ما بيتمنع بالماركة.</b>
    ///
    /// <para>السيناريو: الفني اشتغل على اللاب، وبعدين المدير عدّل
    /// إعداداته وشال منه الماركة. ولو القفل اتمنع، الأمر بيفضل مفتوح
    /// <b>للأبد</b> — واللاب بيقعد في الورشة ومحدش يقدر يخرّجه.</para>
    /// </summary>
    [Fact]
    public void Closing_a_repair_is_never_blocked_by_brands() =>
        Assert.True(RepairBrandGate.CanComplete());
}
