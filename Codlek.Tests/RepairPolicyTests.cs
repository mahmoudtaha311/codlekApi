using Codlek.Core.Enums;
using Codlek.Core.Repairs;

namespace Codlek.Tests;

/// <summary>
/// سياسة أوامر الصيانة.
///
/// <para>🔴 <b>الفحص الأول هنا مش بيفحص منطق — هو قفل على قرار.</b>
/// سطر واحد في <see cref="RepairPolicy.NewOrderApproval"/> بيشغّل
/// ويقفل منع الموافقة على <b>كل الراكات في الميدان</b>. والفحص موجود
/// عشان تغييره يبقى قرار واعي مش سهو في مراجعة.</para>
/// </summary>
public class RepairPolicyTests
{
    /// <summary>
    /// 🔴 <b>الأمر الجديد بيتعمل «مستني الموافقة».</b>
    ///
    /// <para>لو الفحص ده وقع، يبقى حد غيّر السطر. والسؤال اللي لازم
    /// يتسأل: هل ده مقصود؟ لأن النتيجة إن <b>المنع بيتقفل على كل
    /// الراكات</b> — الفنيين بيبدأوا شغل من غير موافقة المحاسب
    /// خالص.</para>
    /// </summary>
    [Fact]
    public void A_new_order_starts_pending_approval() =>
        Assert.Equal(RepairApproval.Pending, RepairPolicy.NewOrderApproval);

    /// <summary>
    /// 🔴 <b>والراكة بتقرا ده من قدرات السيرفر.</b>
    ///
    /// <para>⚠️ محسوبة مش مكتوبة — عشان مايبقاش فيه مكانين للحقيقة.
    /// ولو حد كتب <c>ApprovalEnforced = true</c> بالإيد وغيّر
    /// <c>NewOrderApproval</c>، الراكة تمنع والسيرفر مايمنعش.</para>
    /// </summary>
    [Fact]
    public void The_rack_capability_is_derived_not_written() =>
        Assert.Equal(
            RepairPolicy.NewOrderApproval == RepairApproval.Pending,
            RepairPolicy.ApprovalEnforced);

    /// <summary>
    /// 🔴 <b>و<c>BrandCheck</c> مالهاش قيمة افتراضية.</b>
    ///
    /// <para>كل مكان بيسأل لازم يقول نيّته بالاسم. والافتراضي كان
    /// هيخلّي أي مسار جديد ياخد المنع في صمت — <b>ومن ضمنها مسارات
    /// قفل الأمر</b>، واللي معناها إن صيانة خلصت فعلاً مترفضش
    /// تتسجّل.</para>
    ///
    /// <para>⚠️ والفحص ده بيثبّت إن <c>Enforce</c> هي القيمة صفر
    /// (يعني الافتراضي الضمني لو حد نسى) — فلو حد زوّد قيمة قبلها،
    /// الفحص بيقع ويقول إن معنى «نسيت تحدّد» اتغيّر.</para>
    /// </summary>
    [Fact]
    public void The_brand_check_has_no_safe_implicit_default()
    {
        Assert.Equal(0, (int)RepairPolicy.BrandCheck.Enforce);
        Assert.Equal(1, (int)RepairPolicy.BrandCheck.Skip);
    }

    /// <summary>
    /// ⚠️ وقيمتين بس — زيادة تالتة محتاجة قرار.
    /// </summary>
    [Fact]
    public void There_are_exactly_two_brand_check_modes() =>
        Assert.Equal(2, Enum.GetValues<RepairPolicy.BrandCheck>().Length);
}
