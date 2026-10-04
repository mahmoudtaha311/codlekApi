using Codlek.Core.Devices;
using Codlek.Core.Enums;

namespace Codlek.Tests;

/// <summary>
/// التعرّف على الجهاز من مراسي هويته — <b>القرار، من غير قاعدة</b>.
///
/// <para>🔴 <b>ليه ده موجود أصلاً.</b> التعرّف كان بيحصل <b>على
/// الراكة وبس</b>، في قاعدتها المحلية. راكة ماشافتش اللاب ده قبل كده
/// مابتلاقيش أي مطابقة — فبتعمل جهاز جديد بكود جديد من بلوكها،
/// والسيرفر كان بياخد الجهاز ده زي ما هو من غير ما يقارن أي
/// مرساة.</para>
///
/// <para><b>واتقاس في الإنتاج:</b> نفس اللاب (LENOVO 81FK) اتفحص على
/// راكة فبقى <c>LP-00000501</c>، وبعدين على راكة تانية فبقى
/// <c>LP-00004001</c> — والاتنين مراسيهم <b>متطابقة بالحرف</b>.
/// مفيش أي اختلاف يبرّر جهازين.</para>
/// </summary>
public class DeviceIdentityTests
{
    private static AnchorProbe Probe(
        DeviceIdentifierKind kind, string raw, params Guid[] matched) =>
        new(
            kind,
            raw,
            DeviceIdentity.MatchValue(kind, raw),
            DeviceIdentity.IsStrong(kind),
            DeviceIdentity.Grade(kind, raw),
            matched);

    private static readonly Guid DeviceA = Guid.NewGuid();
    private static readonly Guid DeviceB = Guid.NewGuid();

    // =================================================================
    //  المراسي القوية
    // =================================================================

    /// <summary>
    /// 🔴 <b>وسيريال الهارد <u>مش</u> مرساة قوية — عن قصد.</b>
    ///
    /// <para>الهارد بيتبدّل، واللاب يفضل نفس الجهاز لما الهارد يتغيّر
    /// طول ما الـUUID/BIOS/Board لسه بيعرّفوه. لو عددناه قوي،
    /// <b>تبديل هارد كان هيبقى «تعارض» ويقفل الفحص</b>.</para>
    /// </summary>
    [Theory]
    [InlineData(DeviceIdentifierKind.SystemUuid, true)]
    [InlineData(DeviceIdentifierKind.BiosSerial, true)]
    [InlineData(DeviceIdentifierKind.BoardSerial, true)]
    [InlineData(DeviceIdentifierKind.DiskSerial, false)]
    [InlineData(DeviceIdentifierKind.MacAddress, false)]
    [InlineData(DeviceIdentifierKind.CompanyCode, false)]
    public void Only_three_kinds_identify_the_laptop_itself(
        DeviceIdentifierKind kind, bool strong)
    {
        Assert.Equal(strong, DeviceIdentity.IsStrong(kind));
    }

    [Fact]
    public void The_strong_set_is_exactly_three()
    {
        Assert.Equal(3, DeviceIdentity.StrongKinds.Count);
    }

    // =================================================================
    //  جودة القيمة
    // =================================================================

    [Theory]
    [InlineData("ABC12345", DeviceIdentity.GradeValid)]
    [InlineData("", DeviceIdentity.GradeEmpty)]
    [InlineData("   ", DeviceIdentity.GradeEmpty)]
    [InlineData(null, DeviceIdentity.GradeEmpty)]
    [InlineData("Default string", DeviceIdentity.GradePlaceholder)]
    [InlineData("To Be Filled By O.E.M.", DeviceIdentity.GradePlaceholder)]
    [InlineData("None", DeviceIdentity.GradePlaceholder)]
    public void The_grade_names_why_a_value_was_refused(string? raw, string expected)
    {
        Assert.Equal(expected, DeviceIdentity.Grade(DeviceIdentifierKind.BiosSerial, raw));
    }

    /// <summary>
    /// ⚠️ <b>و«قصيرة جداً» بعد الحشو — ترتيب الفحوص مهم.</b>
    /// <c>"0"</c> حشو، و<c>"AB1"</c> بتترفض في فلتر الحشو نفسه (أقل
    /// من ٤).
    /// </summary>
    [Fact]
    public void A_short_value_is_graded_short_not_placeholder()
    {
        Assert.Equal(
            DeviceIdentity.GradePlaceholder,
            DeviceIdentity.Grade(DeviceIdentifierKind.BiosSerial, "0"));

        Assert.NotEqual(
            DeviceIdentity.GradeValid,
            DeviceIdentity.Grade(DeviceIdentifierKind.BiosSerial, "AB1"));
    }

    /// <summary>
    /// 🔴 <b>و«قصيرة جداً» <u>قابلة للوصول فعلاً</u> — لأن التطبيع
    /// بيقصّر.</b>
    ///
    /// <para>فلتر الحشو بيقيس الخام (<c>"A  B"</c> = ٤ حروف، فبيعدّي)،
    /// والتدريج بيقيس <b>المطبَّع</b> — والتطبيع بيلمّ المسافات
    /// الداخلية فبيبقى <c>"A B"</c> = ٣ حروف.</para>
    ///
    /// <para>⚠️ <b>وتحوير شال فحص الطول <u>ونجا</u></b> من فحوصي
    /// الأولانية، فافتكرناه كود ميت. طلع مش ميت — الفحص ده هو اللي
    /// أثبته. والقيمة دي بتحصل فعلاً: الفني بيلزق سيريال من برنامج
    /// تاني وبييجي فيه تابات.</para>
    /// </summary>
    [Fact]
    public void A_value_that_shrinks_under_normalisation_is_too_short()
    {
        // الخام أربع حروف — فبيعدّي فلتر الحشو.
        Assert.False(Codlek.Core.Text.IdentityValues.IsPlaceholder("A  B"));

        // والمطبَّع تلاتة — فالتدريج بيرفضه.
        Assert.Equal("A B", Codlek.Core.Text.IdentityValues.Normalize("A  B"));

        Assert.Equal(
            DeviceIdentity.GradeTooShort,
            DeviceIdentity.Grade(DeviceIdentifierKind.BiosSerial, "A  B"));

        // 🔴 ومابيتطابقش عليه.
        Assert.Equal(
            "", DeviceIdentity.MatchValue(DeviceIdentifierKind.BiosSerial, "A  B"));
    }

    /// <summary>
    /// 🔴 <b>و<c>SystemUuid</c> لازم يبقى UUID فعلاً.</b> نص مش UUID
    /// في الخانة دي معناه قراية فشلت — ومطابقته بتربط لابات
    /// مختلفة.
    /// </summary>
    [Theory]
    [InlineData("4C4C4544-0051-3010-8051-B8C04F435931", DeviceIdentity.GradeValid)]
    [InlineData("NOT-A-UUID-AT-ALL", DeviceIdentity.GradeBadUuid)]
    [InlineData("ABC12345", DeviceIdentity.GradeBadUuid)]
    public void A_system_uuid_must_parse(string raw, string expected)
    {
        Assert.Equal(expected, DeviceIdentity.Grade(DeviceIdentifierKind.SystemUuid, raw));
    }

    /// <summary>
    /// ⚠️ <b>و«UUID أصفار» مابيوصلش لفحص الشكل أصلاً</b> — هو في
    /// قايمة الحشو، فبيترفض قبلها.
    /// </summary>
    [Fact]
    public void An_all_zero_uuid_is_refused_as_junk()
    {
        Assert.Equal(
            DeviceIdentity.GradePlaceholder,
            DeviceIdentity.Grade(
                DeviceIdentifierKind.SystemUuid,
                "00000000-0000-0000-0000-000000000000"));
    }

    [Fact]
    public void An_unusable_value_has_no_match_value()
    {
        Assert.Equal(
            "", DeviceIdentity.MatchValue(DeviceIdentifierKind.BiosSerial, "Default string"));

        Assert.Equal(
            "ABC12345", DeviceIdentity.MatchValue(DeviceIdentifierKind.BiosSerial, "abc12345"));
    }

    // =================================================================
    //  القرار
    // =================================================================

    [Fact]
    public void One_strong_anchor_on_one_device_resolves()
    {
        var decision = DeviceIdentity.Decide(
        [
            Probe(DeviceIdentifierKind.BiosSerial, "ABC12345", DeviceA),
        ]);

        Assert.Equal(IdentityOutcome.Resolved, decision.Outcome);
        Assert.Equal(DeviceA, decision.DeviceId);
    }

    /// <summary>
    /// ⚠️ <b>وتلات مراسي متفقة أقوى من واحدة — والسبب بيقول
    /// كام.</b>
    /// </summary>
    [Fact]
    public void Agreeing_anchors_are_counted_in_the_reason()
    {
        var decision = DeviceIdentity.Decide(
        [
            Probe(DeviceIdentifierKind.SystemUuid,
                "4C4C4544-0051-3010-8051-B8C04F435931", DeviceA),
            Probe(DeviceIdentifierKind.BiosSerial, "ABC12345", DeviceA),
            Probe(DeviceIdentifierKind.BoardSerial, "BOARD999", DeviceA),
        ]);

        Assert.Equal(IdentityOutcome.Resolved, decision.Outcome);
        Assert.Contains("3", decision.Reason);
    }

    /// <summary>
    /// 🔴 <b>ومرساة واحدة على جهازين = <u>ملتبس</u>، مش اختيار
    /// عشوائي.</b>
    ///
    /// <para>ده تكرار قايم بالفعل — ومابنعملش جهاز تالت، لأن الإنشاء
    /// بيخفي التعارض بدل ما يحله.</para>
    /// </summary>
    [Fact]
    public void One_anchor_on_two_devices_is_ambiguous()
    {
        var decision = DeviceIdentity.Decide(
        [
            Probe(DeviceIdentifierKind.BiosSerial, "ABC12345", DeviceA, DeviceB),
        ]);

        Assert.Equal(IdentityOutcome.Ambiguous, decision.Outcome);
        Assert.Null(decision.DeviceId);
    }

    /// <summary>
    /// 🔴 <b>ومرساتين قويتين على جهازين مختلفين = ملتبس
    /// كمان.</b>
    /// </summary>
    [Fact]
    public void Two_strong_anchors_on_two_devices_is_ambiguous()
    {
        var decision = DeviceIdentity.Decide(
        [
            Probe(DeviceIdentifierKind.BiosSerial, "ABC12345", DeviceA),
            Probe(DeviceIdentifierKind.BoardSerial, "BOARD999", DeviceB),
        ]);

        Assert.Equal(IdentityOutcome.Ambiguous, decision.Outcome);
        Assert.Contains("ممنوع إنشاء جهاز تالت", decision.Reason);
    }

    /// <summary>
    /// 🔴 <b>والهارد لوحده بيكفي — بس بعد ما المراسي القوية
    /// تفشل.</b>
    ///
    /// <para>دليل أضعف، بس لسه معتبر: «مفيش مرساة قوية طابقت» مش
    /// «الجهاز جديد».</para>
    /// </summary>
    [Fact]
    public void A_disk_serial_resolves_when_no_strong_anchor_matched()
    {
        var decision = DeviceIdentity.Decide(
        [
            Probe(DeviceIdentifierKind.BiosSerial, "ABC12345"),
            Probe(DeviceIdentifierKind.DiskSerial, "DISK1234", DeviceA),
        ]);

        Assert.Equal(IdentityOutcome.Resolved, decision.Outcome);
        Assert.Equal(DeviceA, decision.DeviceId);
        Assert.Contains("الهارد", decision.Reason);
    }

    /// <summary>
    /// 🔴 <b>والمرساة القوية بتكسب الهارد — ودي الحالة اللي بتخلّي
    /// تبديل هارد مايقفلش الفحص.</b>
    /// </summary>
    [Fact]
    public void A_strong_anchor_beats_a_disagreeing_disk()
    {
        var decision = DeviceIdentity.Decide(
        [
            Probe(DeviceIdentifierKind.BiosSerial, "ABC12345", DeviceA),
            Probe(DeviceIdentifierKind.DiskSerial, "DISK1234", DeviceB),
        ]);

        Assert.Equal(IdentityOutcome.Resolved, decision.Outcome);
        Assert.Equal(DeviceA, decision.DeviceId);
    }

    [Fact]
    public void Two_disks_on_two_devices_is_ambiguous()
    {
        var decision = DeviceIdentity.Decide(
        [
            Probe(DeviceIdentifierKind.DiskSerial, "DISK1234", DeviceA),
            Probe(DeviceIdentifierKind.DiskSerial, "DISK5678", DeviceB),
        ]);

        Assert.Equal(IdentityOutcome.Ambiguous, decision.Outcome);
    }

    [Fact]
    public void One_disk_on_two_devices_is_ambiguous()
    {
        var decision = DeviceIdentity.Decide(
        [
            Probe(DeviceIdentifierKind.DiskSerial, "DISK1234", DeviceA, DeviceB),
        ]);

        Assert.Equal(IdentityOutcome.Ambiguous, decision.Outcome);
    }

    /// <summary>
    /// ⚠️ <b>وهارد واحد على جهازين بنفس المعرّف = جهاز واحد.</b>
    /// اللاب بهاردين، والاتنين متسجّلين على نفس الصف.
    /// </summary>
    [Fact]
    public void Two_disks_agreeing_on_one_device_resolve()
    {
        var decision = DeviceIdentity.Decide(
        [
            Probe(DeviceIdentifierKind.DiskSerial, "DISK1234", DeviceA),
            Probe(DeviceIdentifierKind.DiskSerial, "DISK5678", DeviceA),
        ]);

        Assert.Equal(IdentityOutcome.Resolved, decision.Outcome);
        Assert.Equal(DeviceA, decision.DeviceId);
    }

    // =================================================================
    //  مفيش دليل
    // =================================================================

    /// <summary>
    /// ⚠️ <b>ومفيش دليل = <u>جهاز جديد مسموح</u>.</b> والرسالة
    /// بتفرّق بين «مراسي صالحة بس ملقتش» و«مفيش مراسي صالحة
    /// أصلاً».
    /// </summary>
    [Fact]
    public void Valid_anchors_with_no_match_allow_a_new_device()
    {
        var decision = DeviceIdentity.Decide(
        [
            Probe(DeviceIdentifierKind.BiosSerial, "ABC12345"),
        ]);

        Assert.Equal(IdentityOutcome.NoEvidence, decision.Outcome);
        Assert.Contains("مفيش جهاز موجود بيطابقها", decision.Reason);
    }

    [Fact]
    public void Junk_anchors_say_so_in_the_reason()
    {
        var decision = DeviceIdentity.Decide(
        [
            Probe(DeviceIdentifierKind.BiosSerial, "Default string"),
            Probe(DeviceIdentifierKind.BoardSerial, ""),
        ]);

        Assert.Equal(IdentityOutcome.NoEvidence, decision.Outcome);
        Assert.Contains("هوية ضعيفة", decision.Reason);
    }

    [Fact]
    public void No_anchors_at_all_is_no_evidence()
    {
        var decision = DeviceIdentity.Decide([]);

        Assert.Equal(IdentityOutcome.NoEvidence, decision.Outcome);
        Assert.Null(decision.DeviceId);
    }

    /// <summary>
    /// 🔴 <b>والحشو مابيدخلش القرار خالص.</b> «Default string» بتتكرر
    /// على <b>مئات</b> اللابات — ولو اتحسبت، كلهم بيبقوا نفس
    /// الجهاز.
    /// </summary>
    [Fact]
    public void A_junk_anchor_that_matched_is_still_ignored()
    {
        // ⚠️ المرساة دي «طابقت» جهاز في المزيّف، بس قيمتها حشو —
        //    فمفروض تتجاهل بالكامل.
        var decision = DeviceIdentity.Decide(
        [
            Probe(DeviceIdentifierKind.BiosSerial, "Default string", DeviceA),
        ]);

        Assert.Equal(IdentityOutcome.NoEvidence, decision.Outcome);
        Assert.Null(decision.DeviceId);
    }

    // =================================================================
    //  التشخيص
    // =================================================================

    /// <summary>
    /// ⚠️ <b>والأثر بيتعرض للمدير وقت الشك</b> — فلازم يقول كل
    /// مرساة قرات إيه وطابقت إيه.
    /// </summary>
    [Fact]
    public void The_trace_names_every_anchor_and_the_verdict()
    {
        var decision = DeviceIdentity.Decide(
        [
            Probe(DeviceIdentifierKind.BiosSerial, "ABC12345", DeviceA),
            Probe(DeviceIdentifierKind.BoardSerial, "Default string"),
            Probe(DeviceIdentifierKind.DiskSerial, "DISK1234", DeviceA, DeviceB),
        ]);

        var lines = decision.Trace().ToList();

        Assert.Equal(5, lines.Count);

        Assert.Contains(lines, l => l.Contains("ABC12345") && l.Contains("قوية"));
        Assert.Contains(lines, l => l.Contains("Default string") && l.Contains("وهمية"));
        Assert.Contains(lines, l => l.Contains("2 أجهزة"));
        Assert.Contains(lines, l => l.Contains("القرار:"));
        Assert.Contains(lines, l => l.Contains("السبب:"));
    }

    [Fact]
    public void The_probe_take_leaves_room_to_see_a_collision()
    {
        Assert.True(
            DeviceIdentity.ProbeTake >= 2,
            "لازم اتنين على الأقل عشان «واحد ولا أكتر» يبان.");
    }
}
