using Codlek.Core.Hardware;
using Codlek.Core.Reports;

namespace Codlek.Tests;

/// <summary>
/// نص نتيجة المرحلة.
///
/// <para>🔴 <b>الملف ده موجود عشان عيب كان بيظهر على كل فحص متسلّم
/// في النظام.</b> تلات مراحل مالهاش نتيجة فحص أصلاً — المواصفات
/// (بتتقرا تلقائي)، والملاحظات (بني آدم بيكتب)، والتسليم (إجراء) —
/// والحالة بتوصلهم <c>٠</c>، وترجمة الصفر «لم يُنفّذ». فالمدير كان
/// بيقرا إن الفحص ناقص وهو كامل.</para>
/// </summary>
public class StageOutcomeTests
{
    private static StageFacts Plain =>
        new(RepairRecorded: false, HasGeneralNote: false, Handed: false);

    // =================================================================
    //  المراحل التلاتة اللي مالهاش نتيجة
    // =================================================================

    /// <summary>
    /// ⚠️ وجود الفحص نفسه هو الدليل إن المواصفات اتقرت — القراية
    /// مرفوعة معاه.
    /// </summary>
    [Fact]
    public void The_specs_stage_reads_as_captured_not_as_not_run()
    {
        Assert.Equal("اتقرت", StageOutcomeText.Arabic(StageOutcomeText.SpecsStep, 0, Plain));

        // ⚠️ ونبرتها محايدة: دي معلومة مش نجاح.
        Assert.Equal(
            StepOutcomeTone.Neutral,
            StageOutcomeText.Tone(StageOutcomeText.SpecsStep, 0, Plain));

        // 🔴 وبالترجمة العادية كانت «لم يُنفّذ».
        Assert.Equal("لم يُنفّذ", StepOutcome.Arabic(0));
    }

    /// <summary>
    /// 🔴 <b>ومرحلة الملاحظات ليها تلات ردود.</b>
    ///
    /// <para>وصيانة متسجّلة بتاخد نبرة <b>وحشة</b> رغم إن المرحلة
    /// مانجحتش ومافشلتش — دي حاجة محتاجة شغل، واللي بيقرا لازم
    /// يشوفها.</para>
    /// </summary>
    [Fact]
    public void The_notes_stage_says_which_of_the_three_cases_it_is()
    {
        var empty = Plain;
        var withNote = Plain with { HasGeneralNote = true };
        var withRepair = Plain with { RepairRecorded = true };

        Assert.Equal("مفيش ملاحظات",
            StageOutcomeText.Arabic(StageOutcomeText.NotesStep, 0, empty));

        Assert.Equal("اتسجّلت",
            StageOutcomeText.Arabic(StageOutcomeText.NotesStep, 0, withNote));

        Assert.Equal("فيه صيانة متسجّلة",
            StageOutcomeText.Arabic(StageOutcomeText.NotesStep, 0, withRepair));

        // 🔴 والصيانة المتسجّلة وحشة؛ الباقي محايد.
        Assert.Equal(StepOutcomeTone.Bad,
            StageOutcomeText.Tone(StageOutcomeText.NotesStep, 0, withRepair));

        Assert.Equal(StepOutcomeTone.Neutral,
            StageOutcomeText.Tone(StageOutcomeText.NotesStep, 0, withNote));
    }

    /// <summary>
    /// ⚠️ <b>والصيانة المتسجّلة بتغلب الملاحظة.</b> لاب فيه صيانة
    /// وملاحظة مع بعض لازم يقول «فيه صيانة» — دي الحاجة الأهم.
    /// </summary>
    [Fact]
    public void A_recorded_repair_wins_over_a_plain_note()
    {
        var both = Plain with { HasGeneralNote = true, RepairRecorded = true };

        Assert.Equal("فيه صيانة متسجّلة",
            StageOutcomeText.Arabic(StageOutcomeText.NotesStep, 0, both));
    }

    /// <summary>
    /// ⚠️ <b>وقت الانتهاء بيتكتب عند التسليم وبس</b>، فهو علامة
    /// التسليم الموجودة أصلاً. والشرط مش شكلي: الفحص ممكن يوصل من
    /// استيراد شيت من غير تسليم.
    /// </summary>
    [Fact]
    public void The_review_stage_reads_the_handover_stamp()
    {
        var handed = Plain with { Handed = true };

        Assert.Equal("تم التسليم",
            StageOutcomeText.Arabic(StageOutcomeText.ReviewStep, 0, handed));

        Assert.Equal(StepOutcomeTone.Good,
            StageOutcomeText.Tone(StageOutcomeText.ReviewStep, 0, handed));

        // ⚠️ ومن غير تسليم بترجع للترجمة العادية.
        Assert.Equal("لم يُنفّذ",
            StageOutcomeText.Arabic(StageOutcomeText.ReviewStep, 0, Plain));

        Assert.Equal(StepOutcomeTone.Neutral,
            StageOutcomeText.Tone(StageOutcomeText.ReviewStep, 0, Plain));
    }

    /// <summary>
    /// ⚠️ ومرحلة فحص عادية بحالة <c>٠</c> بتفضل «لم يُنفّذ» — الحالة
    /// الخاصة للتلاتة بس.
    /// </summary>
    [Fact]
    public void An_ordinary_stage_with_no_status_is_still_not_run()
    {
        Assert.Equal("لم يُنفّذ", StageOutcomeText.Arabic("screen", 0, Plain));

        Assert.Equal(StepOutcomeTone.Neutral, StageOutcomeText.Tone("screen", 0, Plain));
    }

    // =================================================================
    //  الحالات الحقيقية
    // =================================================================

    /// <summary>
    /// ⚠️ <b>وأي حالة حقيقية بتعدّي على الحالة الخاصة.</b> مرحلة
    /// «مواصفات» فشلت فعلاً لازم تقول «فشل» مش «اتقرت».
    /// </summary>
    [Theory]
    [InlineData(StageOutcomeText.SpecsStep)]
    [InlineData(StageOutcomeText.NotesStep)]
    [InlineData(StageOutcomeText.ReviewStep)]
    public void A_real_status_overrides_the_special_case(string stepId)
    {
        Assert.Equal("فشل", StageOutcomeText.Arabic(stepId, 2, Plain));
        Assert.Equal(StepOutcomeTone.Bad, StageOutcomeText.Tone(stepId, 2, Plain));
    }

    /// <summary>
    /// 🔴 <b>«مش موجود» و«تعذّر التنفيذ» مش فشل ومش نجاح.</b>
    ///
    /// <para>الأولانية معلومة مواصفات (اللاب مالوش الحاجة دي)،
    /// والتانية مشكلة في الفحص نفسه. ولو اتلوّنوا أحمر، الشاشة بتقول
    /// إن اللاب باظ وهو سليم.</para>
    /// </summary>
    [Theory]
    [InlineData(1, StepOutcomeTone.Good)]
    [InlineData(2, StepOutcomeTone.Bad)]
    [InlineData(3, StepOutcomeTone.Neutral)]
    [InlineData(4, StepOutcomeTone.Unclear)]
    [InlineData(5, StepOutcomeTone.Unclear)]
    [InlineData(6, StepOutcomeTone.Neutral)]
    public void The_tone_matrix_is_frozen(int status, string tone) =>
        Assert.Equal(tone, StageOutcomeText.Tone("screen", status, Plain));

    /// <summary>
    /// ⚠️ ورقم من نسخة أحدث بياخد «محايد» مش «وحش» — المجهول بيفضل
    /// ظاهر، مابيتحكمش عليه.
    /// </summary>
    [Fact]
    public void An_unknown_status_is_neutral_and_still_named()
    {
        Assert.Equal(StepOutcomeTone.Neutral, StageOutcomeText.Tone("screen", 99, Plain));
        Assert.Equal("لم يُنفّذ", StageOutcomeText.Arabic("screen", 99, Plain));
    }
}
