using Codlek.Core.Enums;
using Codlek.Core.Repairs;

namespace Codlek.Tests;

/// <summary>
/// انتقالات حالة أمر الصيانة.
///
/// <para>🔴 <b>الجدول ده بيمنع نوعين من العطل:</b> أمر مقفول بيرجع
/// يفتح (فالعدّ والتاريخ بيبوظوا)، وأمر شغّال مايقدرش يتقفل (فاللاب
/// بيقعد في الورشة للأبد).</para>
/// </summary>
public class RepairStatusRuleTests
{
    // =================================================================
    //  «حد فتح اللاب فعلاً؟»
    // =================================================================

    /// <summary>
    /// 🔴 <b>الإلغاء مش شغل اتعمل — وده أهم فحص هنا.</b>
    ///
    /// <para><c>Cancelled = 5</c> رقمها أكبر من <c>InProgress = 2</c>،
    /// فشرط زي <c>status &gt;= InProgress</c> كان بيحسب الإلغاء شغل
    /// اتعمل. وإلغاء أمر مستني موافقة حاجة طبيعية تماماً — مالهاش أي
    /// علاقة بإن حد فك لاب من غير إذن.</para>
    /// </summary>
    [Theory]
    [InlineData(RepairStatus.New, false)]
    [InlineData(RepairStatus.WaitingForRepair, false)]
    [InlineData(RepairStatus.InProgress, true)]
    [InlineData(RepairStatus.Completed, true)]
    [InlineData(RepairStatus.UnableToRepair, true)]
    [InlineData(RepairStatus.Cancelled, false)]
    public void Bench_work_is_explicit_membership_not_a_number_comparison(
        RepairStatus status, bool isBenchWork) =>
        Assert.Equal(isBenchWork, RepairStatusRules.IsBenchWork(status));

    /// <summary>
    /// 🔴 <b>الفحص اللي بيمسك المقارنة بالأرقام.</b>
    ///
    /// <para>لو حد كتب <c>status &gt;= InProgress</c>، الإلغاء كان
    /// هيعدّي لأن رقمه أكبر. الفحص ده بيثبّت إن <b>رقم أكبر مش
    /// معناه شغل أكتر</b>.</para>
    /// </summary>
    [Fact]
    public void A_higher_enum_number_does_not_mean_more_work_was_done()
    {
        Assert.True((int)RepairStatus.Cancelled > (int)RepairStatus.InProgress);

        Assert.True(RepairStatusRules.IsBenchWork(RepairStatus.InProgress));
        Assert.False(RepairStatusRules.IsBenchWork(RepairStatus.Cancelled));
    }

    // =================================================================
    //  الانتقالات
    // =================================================================

    [Theory]
    [InlineData(RepairStatus.New, RepairStatus.WaitingForRepair)]
    [InlineData(RepairStatus.New, RepairStatus.InProgress)]
    [InlineData(RepairStatus.New, RepairStatus.Cancelled)]
    [InlineData(RepairStatus.WaitingForRepair, RepairStatus.InProgress)]
    [InlineData(RepairStatus.WaitingForRepair, RepairStatus.Cancelled)]
    [InlineData(RepairStatus.InProgress, RepairStatus.Completed)]
    [InlineData(RepairStatus.InProgress, RepairStatus.UnableToRepair)]
    public void The_allowed_transitions_are_allowed(RepairStatus from, RepairStatus to) =>
        Assert.True(RepairStatusRules.CanMove(from, to));

    /// <summary>
    /// 🔴 <b>أمر اتقفل مايرجعش يفتح.</b>
    ///
    /// <para>الفتح من جديد أمر <b>جديد</b> — وده اللي بيخلّي العدّ
    /// والتاريخ مفهومين. لو الأمر رجع يفتح، «كام أمر اتعمل الشهر ده»
    /// بيبقى سؤال مالوش جواب.</para>
    /// </summary>
    [Theory]
    [InlineData(RepairStatus.Completed, RepairStatus.InProgress)]
    [InlineData(RepairStatus.Completed, RepairStatus.New)]
    [InlineData(RepairStatus.Completed, RepairStatus.WaitingForRepair)]
    [InlineData(RepairStatus.UnableToRepair, RepairStatus.InProgress)]
    [InlineData(RepairStatus.UnableToRepair, RepairStatus.Completed)]
    [InlineData(RepairStatus.Cancelled, RepairStatus.New)]
    [InlineData(RepairStatus.Cancelled, RepairStatus.InProgress)]
    public void A_finished_order_never_reopens(RepairStatus from, RepairStatus to) =>
        Assert.False(RepairStatusRules.CanMove(from, to));

    /// <summary>
    /// ⚠️ <b>والأمر الجديد مايتقفلش على طول.</b>
    ///
    /// <para>لازم يعدّي على <c>InProgress</c> — يعني حد مسكه فعلاً.
    /// «تمت الصيانة» على أمر محدش فتحه معناها سجل كاذب.</para>
    /// </summary>
    [Theory]
    [InlineData(RepairStatus.New, RepairStatus.Completed)]
    [InlineData(RepairStatus.New, RepairStatus.UnableToRepair)]
    [InlineData(RepairStatus.WaitingForRepair, RepairStatus.Completed)]
    [InlineData(RepairStatus.WaitingForRepair, RepairStatus.UnableToRepair)]
    public void An_order_cannot_be_closed_before_anyone_started_it(
        RepairStatus from, RepairStatus to) =>
        Assert.False(RepairStatusRules.CanMove(from, to));

    /// <summary>
    /// ⚠️ <b>والانتقال لنفس الحالة بيعدّي دايماً.</b>
    ///
    /// <para>إعادة الإسناد لفني تاني بتعيد كتابة نفس الحالة — ولو
    /// اتمنعت، المدير مايقدرش يغيّر الفني على أمر شغّال.</para>
    /// </summary>
    [Fact]
    public void Staying_in_the_same_state_is_always_allowed()
    {
        foreach (var status in Enum.GetValues<RepairStatus>())
            Assert.True(RepairStatusRules.CanMove(status, status));
    }

    /// <summary>
    /// ⚠️ وإلغاء أمر شغّال <b>ممنوع</b> — اللي بيتعمل هو «تعذر
    /// الإصلاح».
    ///
    /// <para>الفرق بيبان في التقارير: الملغي يعني محدش لمسه، و«تعذر»
    /// يعني حد اشتغل ومعرفش.</para>
    /// </summary>
    [Fact]
    public void An_order_in_progress_cannot_be_cancelled() =>
        Assert.False(RepairStatusRules.CanMove(RepairStatus.InProgress, RepairStatus.Cancelled));

    // =================================================================
    //  النصوص — عقد عرض
    // =================================================================

    /// <summary>
    /// 🔴 النصوص دي بتتعرض للمستخدم، وبتتبنى منها رسايل.
    /// </summary>
    [Theory]
    [InlineData(RepairStatus.New, "جديدة")]
    [InlineData(RepairStatus.WaitingForRepair, "بانتظار الصيانة")]
    [InlineData(RepairStatus.InProgress, "قيد الصيانة")]
    [InlineData(RepairStatus.Completed, "تمت الصيانة")]
    [InlineData(RepairStatus.UnableToRepair, "تعذر الإصلاح")]
    [InlineData(RepairStatus.Cancelled, "ملغاة")]
    public void The_status_text_is_frozen(RepairStatus status, string expected) =>
        Assert.Equal(expected, RepairStatusRules.Text(status));

    [Theory]
    [InlineData(RepairApproval.Pending, "مستني الموافقة")]
    [InlineData(RepairApproval.Approved, "اتوافق عليه")]
    [InlineData(RepairApproval.Rejected, "اترفض")]
    public void The_approval_text_is_frozen(RepairApproval approval, string expected) =>
        Assert.Equal(expected, RepairStatusRules.ApprovalText(approval));

    /// <summary>⚠️ وكل حالة ليها نص — مفيش واحدة بترجع «غير معروف».</summary>
    [Fact]
    public void Every_status_has_real_text()
    {
        foreach (var status in Enum.GetValues<RepairStatus>())
            Assert.NotEqual("غير معروف", RepairStatusRules.Text(status));

        foreach (var approval in Enum.GetValues<RepairApproval>())
            Assert.NotEqual("غير معروف", RepairStatusRules.ApprovalText(approval));
    }
}
