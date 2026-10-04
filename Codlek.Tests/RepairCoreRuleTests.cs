using Codlek.Core.Devices;
using Codlek.Core.Enums;
using Codlek.Core.Paging;
using Codlek.Core.Repairs;
using Codlek.Core.Text;

namespace Codlek.Tests;

/// <summary>
/// القواعد النقية الباقية في قطاع الصيانة.
///
/// <para>⚠️ كل واحدة منهم كانت مكرّرة في المشروع القديم في مكانين أو
/// تلاتة — والفحوص دي هي اللي بتمنعهم يفترقوا تاني.</para>
/// </summary>
public class RepairCoreRuleTests
{
    // =================================================================
    //  كود الأمر
    // =================================================================

    [Theory]
    [InlineData(1, "RP-00000001")]
    [InlineData(42, "RP-00000042")]
    [InlineData(99_999_999, "RP-99999999")]
    public void The_public_code_is_eight_digits(int number, string expected) =>
        Assert.Equal(expected, RepairCode.Format(number));

    /// <summary>
    /// ⚠️ <b>الرقم اللي بعد ٩٩٩٩٩٩٩٩ بيطلع ٩ خانات.</b>
    ///
    /// <para>الفحص ده بيثبّت السلوك مش بيوافق عليه: العمود طوله ٢٠،
    /// فالسقف ده قرار واعي. و«RP-» + ٩ خانات = ١٢ حرف، لسه جوّه
    /// العمود.</para>
    /// </summary>
    [Fact]
    public void Past_eight_digits_the_code_grows_rather_than_wrapping()
    {
        string code = RepairCode.Format(100_000_000);

        Assert.Equal("RP-100000000", code);
        Assert.True(code.Length <= TextClip.Lengths.PublicCode);
    }

    /// <summary>
    /// 🔴 <b>اسم العدّاد مثبّت.</b>
    ///
    /// <para>تغييره بيرجّع الترقيم لواحد — و<b>بيكرّر أكواد موجودة
    /// مطبوعة على ورق</b>.</para>
    /// </summary>
    [Fact]
    public void The_counter_name_is_frozen()
    {
        Assert.Equal("repair", RepairCode.CounterName);
        Assert.Equal("RP-", RepairCode.Prefix);
    }

    // =================================================================
    //  حالة الفتح
    // =================================================================

    /// <summary>
    /// 🔴 <b>الإسناد هو اللي بيعمل المسؤولية.</b>
    ///
    /// <para><c>New</c> = معروض على كل فني مطابق، محدش ماسكه.
    /// <c>WaitingForRepair</c> = فني بالاسم ماسكه ولسه مابدأش. والفرق
    /// ده هو الحاجة الوحيدة اللي بتدّي «مين المسؤول دلوقتي» جواب.</para>
    /// </summary>
    [Fact]
    public void Assignment_is_what_creates_accountability()
    {
        Assert.Equal(RepairStatus.New, RepairOpening.StatusAtOpen(assigned: false));
        Assert.Equal(RepairStatus.WaitingForRepair, RepairOpening.StatusAtOpen(assigned: true));
    }

    /// <summary>⚠️ وفتح من غير إسناد ثم إسناد انتقال شرعي.</summary>
    [Fact]
    public void Opening_unassigned_then_assigning_is_a_legal_move() =>
        Assert.True(RepairStatusRules.CanMove(
            RepairOpening.StatusAtOpen(false), RepairOpening.StatusAtOpen(true)));

    /// <summary>⚠️ وأمر مسنود من أول لحظة لسه ينفع يتلغي قبل ما يبدأ.</summary>
    [Fact]
    public void A_pre_assigned_order_can_still_be_cancelled() =>
        Assert.True(RepairStatusRules.CanMove(
            RepairOpening.StatusAtOpen(true), RepairStatus.Cancelled));

    // =================================================================
    //  القفل
    // =================================================================

    [Theory]
    [InlineData(RepairStatus.Completed, true)]
    [InlineData(RepairStatus.UnableToRepair, true)]
    [InlineData(RepairStatus.Cancelled, true)]
    [InlineData(RepairStatus.New, false)]
    [InlineData(RepairStatus.WaitingForRepair, false)]
    [InlineData(RepairStatus.InProgress, false)]
    public void Closure_is_explicit_membership(RepairStatus status, bool closed) =>
        Assert.Equal(closed, RepairClosure.IsClosed(status));

    [Fact]
    public void Open_is_exactly_the_opposite_of_closed()
    {
        foreach (var status in Enum.GetValues<RepairStatus>())
            Assert.Equal(RepairClosure.IsClosed(status), !RepairClosure.IsOpen(status));
    }

    /// <summary>
    /// 🔴 <b>الفحص ده بيوثّق <i>ليه</i> العضوية صريحة.</b>
    ///
    /// <para><c>Cancelled</c> رقمها أكبر من <c>InProgress</c>، فأي
    /// <c>&gt;=</c> هنا بيحسب غلط.</para>
    /// </summary>
    [Fact]
    public void Closure_cannot_be_an_ordinal_comparison()
    {
        Assert.True((int)RepairStatus.Cancelled > (int)RepairStatus.InProgress);

        Assert.True(RepairClosure.IsClosed(RepairStatus.Cancelled));
        Assert.False(RepairClosure.IsClosed(RepairStatus.InProgress));
    }

    // =================================================================
    //  حاجز الموافقة
    // =================================================================

    /// <summary>
    /// 🔴 <b>الميزة كلها بتتقفل من ثابت واحد.</b>
    /// </summary>
    [Fact]
    public void Turning_the_feature_off_opens_the_gate() =>
        Assert.Equal(
            RepairApprovalGate.StartBlock.None,
            RepairApprovalGate.CanStart(false, RepairApproval.Pending));

    [Theory]
    [InlineData(RepairApproval.Approved, RepairApprovalGate.StartBlock.None)]
    [InlineData(RepairApproval.Rejected, RepairApprovalGate.StartBlock.Rejected)]
    [InlineData(RepairApproval.Pending, RepairApprovalGate.StartBlock.AwaitingApproval)]
    public void The_gate_names_which_refusal_applies(
        RepairApproval approval, RepairApprovalGate.StartBlock expected) =>
        Assert.Equal(expected, RepairApprovalGate.CanStart(true, approval));

    /// <summary>
    /// 🔴 <b>قيمة مش معروفة = «مستني موافقة»، مش «موافَق عليه».</b>
    ///
    /// <para>⚠️ <b>والراكة بتفشل مفتوح والسيرفر بيفشل مقفول — وده
    /// مقصود.</b> الراكة أوفلاين والشغل اتعمل خلاص على البنش، فرفضه
    /// بيمسح الدليل. السيرفر لسه قبل الشغل، فالمنع مالوش تكلفة.</para>
    /// </summary>
    [Fact]
    public void An_unknown_approval_value_is_never_read_as_approved() =>
        Assert.Equal(
            RepairApprovalGate.StartBlock.AwaitingApproval,
            RepairApprovalGate.CanStart(true, (RepairApproval)99));

    /// <summary>
    /// 🔴 <b>الحاجز مسلّح دلوقتي فعلاً.</b>
    ///
    /// <para>الفحص ده بيربط الحاجز بالسياسة: لو حد غيّر
    /// <c>NewOrderApproval</c>، الفحص ده بيقع بدل ما الحاجز يتفكّ في
    /// صمت.</para>
    /// </summary>
    [Fact]
    public void The_gate_is_actually_armed_today() =>
        Assert.NotEqual(
            RepairApprovalGate.StartBlock.None,
            RepairApprovalGate.CanStart(
                RepairPolicy.ApprovalEnforced, RepairApproval.Pending));

    // =================================================================
    //  القص
    // =================================================================

    [Theory]
    [InlineData(null, 10, "")]
    [InlineData("", 10, "")]
    [InlineData("abc", 3, "abc")]
    [InlineData("abcd", 3, "abc")]
    public void Clipping_keeps_the_boundary_inclusive(
        string? value, int max, string expected) =>
        Assert.Equal(expected, TextClip.To(value, max));

    /// <summary>
    /// ⚠️ <b>القص بدل الرفض مقصود.</b>
    ///
    /// <para>الرفض في طابور الرفع من الراكة <b>نهائي</b>، فسبب عطل
    /// عربي طويل كان بيمسح شغل بنش حقيقي.</para>
    /// </summary>
    [Fact]
    public void Five_hundred_arabic_characters_clip_to_four_hundred() =>
        Assert.Equal(
            TextClip.Lengths.Reason,
            TextClip.To(new string('ا', 500), TextClip.Lengths.Reason).Length);

    // =================================================================
    //  اسم الجهاز
    // =================================================================

    /// <summary>
    /// 🔴 الاسم التجاري بيسبق الموديل الخام — <b>الفني بيعرف اللاب
    /// بـ«EliteBook» مش بـ«20L5»</b>.
    /// </summary>
    [Fact]
    public void The_commercial_name_wins() =>
        Assert.Equal(
            "HP EliteBook 840 G5",
            DeviceNaming.Display("HP", "EliteBook 840 G5", "HP EliteBook 840 G5 Notebook PC"));

    /// <summary>
    /// ⚠️ <b>والمصنّع بيتكرر لو كان جوّه الموديل الخام.</b>
    ///
    /// <para>ده سلوك حقيقي شغّال، والفحص بيثبّته مش بيوافق عليه:
    /// «تصليحه» بيغيّر اللي الداش بورد بتعرضه.</para>
    /// </summary>
    [Fact]
    public void The_double_manufacturer_artefact_is_pinned() =>
        Assert.Equal("HP HP ProBook", DeviceNaming.Display("HP", null, "HP ProBook"));

    [Theory]
    [InlineData(null, null, "X230", "X230")]
    [InlineData("Lenovo", "   ", "20AL", "Lenovo 20AL")]
    [InlineData(null, null, null, "")]
    public void The_name_never_has_stray_spaces(
        string? manufacturer, string? commercial, string? raw, string expected) =>
        Assert.Equal(expected, DeviceNaming.Display(manufacturer, commercial, raw));

    // =================================================================
    //  التصفيح
    // =================================================================

    /// <summary>
    /// ⚠️ <b>التصفيح بيقصّ مابيرفضش.</b>
    ///
    /// <para><c>?page=0</c> لازم يرجّع أول صفحة مش <c>400</c> — القديم
    /// كده والداش بورد معتمدة عليه. والقص هو اللي بيمنع <c>Skip</c>
    /// بقيمة سالبة.</para>
    /// </summary>
    [Theory]
    [InlineData(null, null, 1, 40)]
    [InlineData(0, 0, 1, 40)]
    [InlineData(-3, -5, 1, 40)]
    [InlineData(2, 1_000_000, 2, 200)]
    [InlineData(1, 200, 1, 200)]
    public void Paging_clamps_instead_of_rejecting(
        int? page, int? size, int expectedPage, int expectedSize)
    {
        var (p, s) = Paging.Clamp(page, size);

        Assert.Equal(expectedPage, p);
        Assert.Equal(expectedSize, s);
    }

    [Theory]
    [InlineData(0, 40, 0)]
    [InlineData(41, 40, 2)]
    [InlineData(40, 40, 1)]
    public void Total_pages_rounds_up(int items, int size, int expected) =>
        Assert.Equal(expected, Paging.TotalPages(items, size));

    /// <summary>⚠️ ومفيش قسمة على صفر.</summary>
    [Fact]
    public void Total_pages_never_divides_by_zero() =>
        Assert.Equal(5, Paging.TotalPages(5, 0));
}
