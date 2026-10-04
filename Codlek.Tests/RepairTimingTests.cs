using Codlek.Core.Repairs;

namespace Codlek.Tests;

/// <summary>
/// أرقام الوقت على أمر الصيانة — <b>تلات أسئلة مختلفة</b>.
///
/// <para>🔴 <b>والمشروع القديم فيه نسختين من <c>DurationMs</c>:</b>
/// واحدة في <c>RepairService</c> بتغذّي صفحة التفاصيل، وواحدة خاصة في
/// <c>ApiV1</c> بتغذّي القايمة. نفس المعادلة بمدخلين مختلفين —
/// واتجمّعوا في واحدة هنا عشان مايفترقوش.</para>
/// </summary>
public class RepairTimingTests
{
    private static readonly DateTime Now = new(2026, 10, 4, 12, 0, 0, DateTimeKind.Utc);

    // =================================================================
    //  المدة — للأوامر المقفولة
    // =================================================================

    [Fact]
    public void A_duration_needs_both_ends()
    {
        Assert.Null(RepairTiming.DurationMs(null, Now));
        Assert.Null(RepairTiming.DurationMs(Now, null));
        Assert.Null(RepairTiming.DurationMs(null, null));
    }

    [Fact]
    public void A_ninety_minute_repair_is_five_point_four_million_milliseconds() =>
        Assert.Equal(5_400_000, RepairTiming.DurationMs(Now, Now.AddMinutes(90)));

    /// <summary>
    /// ⚠️ <b>السالب بيتقصّ على صفر.</b>
    ///
    /// <para>ساعة الراكة ممكن تكون قدّام ساعة السيرفر، فوقت القفل
    /// بييجي قبل وقت البدء. والمدة السالبة بتبان في الشاشة كرقم
    /// مجنون.</para>
    /// </summary>
    [Fact]
    public void A_negative_duration_clamps_to_zero() =>
        Assert.Equal(0, RepairTiming.DurationMs(Now, Now.AddHours(-3)));

    // =================================================================
    //  عمر الفتح — للأوامر المفتوحة
    // =================================================================

    /// <summary>
    /// 🔴 <b>الأمر المقفول بيرجّع <c>null</c> مش المدة.</b>
    ///
    /// <para>خلطهم بيخلّي أمر قفل من شهر يطلع «عمره ٧٢٠ ساعة» ويغرق
    /// اللي لسه واقف في الطابور.</para>
    /// </summary>
    [Fact]
    public void A_closed_order_has_no_open_age()
    {
        Assert.Null(RepairTiming.OpenAgeHours(Now.AddDays(-5), Now, Now));

        // ⚠️ وحتى لو وقت القفل قبل وقت الفتح (ساعة راكة غلط).
        Assert.Null(RepairTiming.OpenAgeHours(Now, Now.AddDays(-5), Now));
    }

    [Fact]
    public void An_open_order_ages_from_when_it_was_opened() =>
        Assert.Equal(
            48, RepairTiming.OpenAgeHours(Now.AddHours(-48), null, Now)!.Value, precision: 3);

    [Fact]
    public void A_future_opened_date_clamps_to_zero() =>
        Assert.Equal(0, RepairTiming.OpenAgeHours(Now.AddHours(5), null, Now));

    // =================================================================
    //  عمر الصيانة — تحت إيد الفني
    // =================================================================

    /// <summary>
    /// 🔴 <b>مفيش بداية = <c>null</c>، مش صفر.</b>
    ///
    /// <para>الصفر كان هيخلّي الأمر اللي محدش استلمه يطلع في أول
    /// «الأقدم الأول» كأنه أطول واحد.</para>
    /// </summary>
    [Fact]
    public void An_unstarted_order_has_no_repair_age_not_zero() =>
        Assert.Null(RepairTiming.RepairAgeHours(null, null, Now));

    /// <summary>
    /// ⚠️ والمقفول بيتفحص <b>قبل</b> المبدوء — أمر اتقفل وعمره ما
    /// بدأ بيرجّع <c>null</c>.
    /// </summary>
    [Fact]
    public void A_closed_never_started_order_returns_null() =>
        Assert.Null(RepairTiming.RepairAgeHours(null, Now, Now));

    [Fact]
    public void A_started_open_order_ages_from_the_start() =>
        Assert.Equal(
            3, RepairTiming.RepairAgeHours(Now.AddHours(-3), null, Now)!.Value, precision: 3);

    /// <summary>
    /// 🔴 <b>المثال اللي الوثيقة القديمة بتضربه.</b>
    ///
    /// <para>أمر اتفتح من أسبوعين وبدأ من ساعة: <c>OpenAge = 336</c>
    /// و<c>RepairAge = 1</c>. ورقم واحد منهم لوحده بيدّي صورة غلط في
    /// الاتجاهين — «بقاله أسبوعين» بتقول إهمال، و«بقاله ساعة» بتقول
    /// إن كل حاجة تمام.</para>
    /// </summary>
    [Fact]
    public void The_worked_example_from_the_legacy_doc()
    {
        var opened = Now.AddDays(-14);
        var started = Now.AddHours(-1);

        Assert.Equal(336, RepairTiming.OpenAgeHours(opened, null, Now)!.Value, precision: 3);
        Assert.Equal(1, RepairTiming.RepairAgeHours(started, null, Now)!.Value, precision: 3);
    }
}
