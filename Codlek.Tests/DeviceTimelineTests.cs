using Codlek.Core.Devices;
using Codlek.Core.Enums;

namespace Codlek.Tests;

/// <summary>
/// ترتيب خط زمن اللاب وتصفيحه.
///
/// <para>🔴 <b>وكل حاجة في الملف ده بتحمي عيب بيعيش شهور قبل ما حد
/// يشوفه:</b> ترتيب غلط في <b>التعادلات وبس</b>، أو حدث بيظهر في
/// صفحتين، أو حدث بيختفي خالص. التاريخ بيبان معقول في الحالتين —
/// واللي بيبص مش بيعرف إن فيه حاجة ناقصة.</para>
/// </summary>
public class DeviceTimelineTests
{
    // =================================================================
    //  الأولوية
    // =================================================================

    /// <summary>
    /// 🔴 <b>كل أولوية لازم تكون رقم واحد (٠—٩).</b>
    ///
    /// <para>مفتاح الترتيب بيلزق الرقم ده في نص بعرض <b>مش</b> ثابت،
    /// والمقارنة بعد كده نصية بترتيب البايت. فأولوية «١٠» بتقع
    /// <b>قبل</b> أولوية «٩» لأن <c>'1' &lt; '9'</c>.</para>
    /// </summary>
    [Fact]
    public void Every_rank_is_a_single_digit()
    {
        foreach (var type in Enum.GetValues<DeviceTimelineEventType>())
        {
            int rank = DeviceTimelineOrder.Rank(type);

            Assert.InRange(rank, 0, 9);
            Assert.Single(rank.ToString());
        }

        // ⚠️ والمجهول كمان — نوع من نسخة أحدث بياخد الافتراضي.
        Assert.InRange(DeviceTimelineOrder.Rank((DeviceTimelineEventType)999), 0, 9);
    }

    /// <summary>
    /// 🔴 <b>كل نوع ليه أولوية صريحة — مش الافتراضي.</b>
    ///
    /// <para>الافتراضي (٩) معناه «آخر أي مجموعة متعادلة»، فنوع
    /// بيستعمله بيقع آخر التعادلات من غير سبب. والفحص بالانعكاس:
    /// نوع جديد بينضاف بكرة بيوقّعه لوحده.</para>
    /// </summary>
    [Fact]
    public void Every_event_type_has_an_explicit_rank()
    {
        var onDefault = Enum.GetValues<DeviceTimelineEventType>()
            .Where(t => DeviceTimelineOrder.Rank(t) == 9)
            .ToList();

        Assert.Empty(onDefault);
    }

    /// <summary>
    /// ⚠️ <b>والترتيب ده سببه السببية مش الذوق:</b> اللاب يتكتشف،
    /// يتفحص، الفحص يفتح أمر، الأمر يبدأ ويقفل، اللاب يتنقل، وفي
    /// الآخر حد يكتب ملاحظة عن اللي حصل.
    /// </summary>
    [Fact]
    public void The_ranks_follow_causality()
    {
        int[] order =
        [
            DeviceTimelineOrder.Rank(DeviceTimelineEventType.DeviceDiscovered),
            DeviceTimelineOrder.Rank(DeviceTimelineEventType.TestPerformed),
            DeviceTimelineOrder.Rank(DeviceTimelineEventType.RepairOpened),
            DeviceTimelineOrder.Rank(DeviceTimelineEventType.RepairStarted),
            DeviceTimelineOrder.Rank(DeviceTimelineEventType.RepairCompleted),
            DeviceTimelineOrder.Rank(DeviceTimelineEventType.RepairUnableToRepair),
            DeviceTimelineOrder.Rank(DeviceTimelineEventType.RepairCancelled),
            DeviceTimelineOrder.Rank(DeviceTimelineEventType.DeviceMoved),
            DeviceTimelineOrder.Rank(DeviceTimelineEventType.NoteAdded),
        ];

        Assert.Equal(order.OrderBy(r => r), order);
    }

    // =================================================================
    //  مفتاح الترتيب
    // =================================================================

    /// <summary>
    /// 🔴 <b>المقارنة النصية لازم تساوي المقارنة الزمنية.</b>
    ///
    /// <para>وده سبب <c>D19</c> على التيكات: من غير عرض ثابت،
    /// <c>"9999" &lt; "10000"</c> نصياً والترتيب بيتقلب.</para>
    /// </summary>
    [Fact]
    public void The_sort_key_compares_like_the_timestamp()
    {
        var early = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var late = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);

        string a = DeviceTimelineOrder.SortKey(
            early, DeviceTimelineEventType.TestPerformed, "x");

        string b = DeviceTimelineOrder.SortKey(
            late, DeviceTimelineEventType.TestPerformed, "x");

        Assert.True(string.CompareOrdinal(a, b) < 0);
    }

    /// <summary>
    /// ⚠️ <b>والتيكات بعرض ثابت ١٩ خانة</b> — حتى للتواريخ الصغيرة.
    /// </summary>
    [Fact]
    public void The_ticks_segment_is_always_nineteen_characters()
    {
        string key = DeviceTimelineOrder.SortKey(
            new DateTime(1, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            DeviceTimelineEventType.NoteAdded,
            "x");

        Assert.Equal(19, key.Split('|')[0].Length);
    }

    /// <summary>
    /// 🔴 <b>وعند تساوي التوقيت، النوع هو اللي بيفك التعادل.</b>
    /// </summary>
    [Fact]
    public void At_the_same_instant_the_type_breaks_the_tie()
    {
        var at = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc);

        string discovered = DeviceTimelineOrder.SortKey(
            at, DeviceTimelineEventType.DeviceDiscovered, "x");

        string note = DeviceTimelineOrder.SortKey(
            at, DeviceTimelineEventType.NoteAdded, "x");

        // الملاحظة أولويتها أعلى رقماً، فبترتّب بعد الاكتشاف صاعداً
        // — ويعني فوقه في العرض النازل.
        Assert.True(string.CompareOrdinal(discovered, note) < 0);
    }

    /// <summary>
    /// 🔴 <b>والمستوى التالت هو اللي بيمنع الحدث يتكرر أو يختفي.</b>
    ///
    /// <para>من غيره، صفّين بنفس التوقيت ونفس النوع مفتاحهم واحد —
    /// وده بالظبط اللي كسر تصفيح الفحوص قبل كده.</para>
    /// </summary>
    [Fact]
    public void Two_rows_at_the_same_instant_and_type_still_differ()
    {
        var at = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc);

        string a = DeviceTimelineOrder.SortKey(
            at, DeviceTimelineEventType.TestPerformed, Guid.NewGuid().ToString("N"));

        string b = DeviceTimelineOrder.SortKey(
            at, DeviceTimelineEventType.TestPerformed, Guid.NewGuid().ToString("N"));

        Assert.NotEqual(a, b);
    }

    // =================================================================
    //  التصفيح
    // =================================================================

    /// <summary>
    /// ⚠️ <b>صفحة واحدة على الأقل دايماً.</b> اللاب اللي لسه
    /// ماتفحصش عنده حدث واحد (الاكتشاف)، وصفر صفحات كانت بتخلّي
    /// الواجهة تقول «صفحة ٠ من ٠».
    /// </summary>
    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(25, 1)]
    [InlineData(26, 2)]
    [InlineData(50, 2)]
    [InlineData(51, 3)]
    public void Pages_are_at_least_one(int total, int expected)
    {
        Assert.Equal(expected, DeviceTimelineOrder.Pages(total));
    }

    /// <summary>
    /// 🔴 <b>الطلب بيترد لآخر صفحة موجودة.</b> رابط محفوظ على صفحة
    /// ٧ للاب بقى عنده صفحتين لازم يعرض حاجة — مش شاشة فاضية.
    /// </summary>
    [Theory]
    [InlineData(null, 30, 1)]
    [InlineData(0, 30, 1)]
    [InlineData(-5, 30, 1)]
    [InlineData(1, 30, 1)]
    [InlineData(2, 30, 2)]
    [InlineData(7, 30, 2)]
    [InlineData(99, 10, 1)]
    public void A_page_past_the_end_falls_back_to_the_last_one(
        int? requested, int total, int expected)
    {
        Assert.Equal(expected, DeviceTimelineOrder.ClampPage(requested, total));
    }

    /// <summary>
    /// ⚠️ <b>والسقف تاني:</b> الطريقة بتجيب «الصفحة × ٢٥» من
    /// <b>كل</b> مصدر، فصفحة ٥٠٠ كانت هتسحب ١٢٥٠٠ صف من كل جدول.
    /// </summary>
    [Fact]
    public void The_page_is_capped_even_when_there_is_enough_data()
    {
        int huge = DeviceTimelineOrder.PageSize * 10_000;

        Assert.Equal(DeviceTimelineOrder.MaxPage,
            DeviceTimelineOrder.ClampPage(9_999, huge));
    }

    [Theory]
    [InlineData(1, 25, 0)]
    [InlineData(2, 50, 25)]
    [InlineData(3, 75, 50)]
    public void Need_and_skip_follow_the_page(int page, int need, int skip)
    {
        Assert.Equal(need, DeviceTimelineOrder.Need(page));
        Assert.Equal(skip, DeviceTimelineOrder.Skip(page));
    }

    /// <summary>
    /// 🔴 <b>حاجز: <c>ClampPage</c> بتاخد عدد <u>الأحداث</u> مش عدد
    /// الصفحات.</b>
    ///
    /// <para>تمرير عدد صفحات هنا بيقصّ على الرقم الغلط ويخلّي آخر
    /// الصفحات مش موصولة — وده بيبان في آخر صفحة وبس.</para>
    /// </summary>
    [Fact]
    public void ClampPage_takes_an_item_count_not_a_page_count()
    {
        // ٦٠ حدث = ٣ صفحات، فالصفحة ٣ مسموحة.
        Assert.Equal(3, DeviceTimelineOrder.ClampPage(3, 60));

        // ولو كانت بتاخد عدد صفحات، «٣» كانت هتتفسّر صفحة واحدة.
        Assert.NotEqual(1, DeviceTimelineOrder.ClampPage(3, 60));
    }

    // =================================================================
    //  النصوص
    // =================================================================

    /// <summary>
    /// 🔴 <b>الملخّص بيقول اللي متسجّل وبس.</b> القطعة الفاضية
    /// مابتتكتبش، فالسطر بيطول ويقصر على حسب الموجود — مش قالب فيه
    /// خانات فاضية.
    /// </summary>
    [Fact]
    public void An_empty_part_is_left_out_of_the_summary()
    {
        string none = DeviceTimelineText.Discovery("", DeviceIdentityConfidence.None, "");

        // درجة الثقة بتتكتب دايماً، والباقي لأ.
        Assert.Equal("ثقة مفيش", none);

        string all = DeviceTimelineText.Discovery(
            "LP-00018425", DeviceIdentityConfidence.A, "serial+uuid");

        Assert.Equal("الكود LP-00018425 · ثقة أ · أساس الهوية: serial+uuid", all);
    }

    /// <summary>
    /// ⚠️ <b>ودرجة الثقة بحرف عربي مش لاتيني</b> — السطر كله عربي،
    /// و«A» جوّاه بتبان حرف ضايع.
    /// </summary>
    [Theory]
    [InlineData(DeviceIdentityConfidence.A, "أ")]
    [InlineData(DeviceIdentityConfidence.B, "ب")]
    [InlineData(DeviceIdentityConfidence.C, "ج")]
    [InlineData(DeviceIdentityConfidence.None, "مفيش")]
    public void The_confidence_letter_is_arabic(
        DeviceIdentityConfidence confidence, string expected)
    {
        Assert.Equal(expected, DeviceTimelineText.ConfidenceLetter(confidence));
    }

    /// <summary>
    /// 🔴 <b>قصّ العرض بيحط علامة قص — و<c>TextClip.To</c> لأ.</b>
    ///
    /// <para>الأخيرة بتفصّل على طول العمود قبل الحفظ، وزيادة «…» على
    /// قيمة متخزّنة بتخلّي القيمة نفسها مغلوطة. والفحص ده بيثبّت إن
    /// الاتنين <b>مش</b> بيتساووا.</para>
    /// </summary>
    [Fact]
    public void The_display_clip_adds_an_ellipsis_and_the_column_clip_does_not()
    {
        string longText = new('م', 200);

        string display = DeviceTimelineText.Clip(longText, 160);
        string column = Core.Text.TextClip.To(longText, 160);

        Assert.EndsWith("…", display, StringComparison.Ordinal);
        Assert.DoesNotContain("…", column);
        Assert.NotEqual(display, column);

        Assert.Equal(161, display.Length);
        Assert.Equal(160, column.Length);
    }

    /// <summary>
    /// ⚠️ <b>والقصّ بيشيل المسافات الأول.</b> الملاحظات جاية من
    /// الميدان وبتبدأ بسطر فاضي كتير.
    /// </summary>
    [Fact]
    public void The_display_clip_trims_first()
    {
        Assert.Equal("كلام", DeviceTimelineText.Clip("  كلام  ", 160));
        Assert.Equal("", DeviceTimelineText.Clip(null, 160));
        Assert.Equal("", DeviceTimelineText.Clip("   ", 160));
    }

    /// <summary>
    /// 🔴 <b>الحركة بتكتب اللي اختلف وبس.</b>
    ///
    /// <para>تسليم الحيازة لنفس الفني بيتخزّن وفيه
    /// <c>From == To</c>، وطباعة «من فلان لفلان» في الحالة دي بتقول
    /// إن حاجة اتغيّرت وهي ما اتغيّرتش.</para>
    /// </summary>
    [Fact]
    public void A_movement_that_changed_nothing_says_nothing()
    {
        var at = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc);

        string summary = DeviceTimelineText.Movement(
            fromStage: DeviceOperationalStage.Tested,
            toStage: DeviceOperationalStage.Tested,
            fromLocation: "المخزن",
            toLocation: "المخزن",
            locationChanged: false,
            fromHolder: "محمود",
            toHolder: "محمود",
            holderChanged: false,
            reason: "",
            occurredAtUtc: at,
            recordedAtUtc: at);

        Assert.Equal("", summary);
    }

    [Fact]
    public void A_movement_reports_each_side_that_changed()
    {
        var at = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc);

        string summary = DeviceTimelineText.Movement(
            fromStage: DeviceOperationalStage.Tested,
            toStage: DeviceOperationalStage.WithSales,
            fromLocation: "المخزن",
            toLocation: "المبيعات",
            locationChanged: true,
            fromHolder: "محمود",
            toHolder: "كريم",
            holderChanged: true,
            reason: "طلب العميل",
            occurredAtUtc: at,
            recordedAtUtc: at);

        Assert.Contains("المرحلة:", summary);
        Assert.Contains("المكان: المخزن ← المبيعات", summary);
        Assert.Contains("الحائز: محمود ← كريم", summary);
        Assert.Contains("طلب العميل", summary);
    }

    /// <summary>
    /// 🔴 <b>وصول متأخر بيتقال — بعتبة دقيقة.</b>
    ///
    /// <para>راكة اشتغلت أوفلاين بترفع حركة عمرها أيام، والسطر لازم
    /// يقول إنها وصلت متأخرة بدل ما يخبّي الفرق. والعتبة دقيقة مش
    /// صفر: الفرق الطبيعي أجزاء من الثانية، وطباعته على كل سطر
    /// بتحوّل معلومة مفيدة لضوضاء.</para>
    /// </summary>
    [Fact]
    public void A_late_arrival_is_stated_only_past_a_minute()
    {
        var at = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc);

        string instant = Movement(at, at.AddSeconds(5));
        string late = Movement(at, at.AddDays(3));

        Assert.DoesNotContain("وصل السيرفر", instant);
        Assert.Contains("وصل السيرفر", late);
    }

    /// <summary>
    /// ⚠️ وعتبة الدقيقة بالظبط — <c>&gt;=</c> مش <c>&gt;</c>.
    /// </summary>
    [Fact]
    public void Exactly_one_minute_counts_as_late()
    {
        var at = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc);

        Assert.Contains("وصل السيرفر", Movement(at, at.AddMinutes(1)));
        Assert.DoesNotContain("وصل السيرفر", Movement(at, at.AddSeconds(59)));
    }

    private static string Movement(DateTime occurred, DateTime recorded) =>
        DeviceTimelineText.Movement(
            DeviceOperationalStage.Tested, DeviceOperationalStage.Tested,
            "", "", false, "", "", false, "",
            occurred, recorded);

    /// <summary>
    /// ⚠️ <b>ومدة الفحص بالدقايق مقطوعة مش مقرّبة</b> — زي القديم.
    /// فحص ٩٠ ثانية بيتقال عنه «دقيقة».
    /// </summary>
    [Theory]
    [InlineData(90_000, "1 دقيقة")]
    [InlineData(119_000, "1 دقيقة")]
    [InlineData(120_000, "2 دقيقة")]
    public void The_test_duration_is_truncated_to_minutes(long ms, string expected)
    {
        Assert.Contains(expected, DeviceTimelineText.Test(null, ms));
    }

    /// <summary>⚠️ ومدة صفر مابتتكتبش خالص.</summary>
    [Fact]
    public void A_zero_duration_is_left_out()
    {
        Assert.Equal("", DeviceTimelineText.Test(null, 0));
    }

    // =================================================================
    //  العناوين
    // =================================================================

    /// <summary>
    /// 🔴 <b>كل نوع ليه عنوان — ما عدا الحركة.</b>
    ///
    /// <para>الحركة عنوانها بيجي من <c>DeviceMovementTitle</c> على
    /// حسب نوعها، فعنوان ثابت ليها هنا كان بيخفي الفرق بين «اتنقل»
    /// و«اتسلّم».</para>
    /// </summary>
    [Fact]
    public void Every_type_but_the_movement_has_a_title()
    {
        var missing = Enum.GetValues<DeviceTimelineEventType>()
            .Where(t => t != DeviceTimelineEventType.DeviceMoved)
            .Where(t => DeviceTimelineText.Title(t).Length == 0)
            .ToList();

        Assert.Empty(missing);
        Assert.Equal("", DeviceTimelineText.Title(DeviceTimelineEventType.DeviceMoved));
    }

    /// <summary>
    /// 🔴 <b>«تمت الصيانة» و«تعذّر الإصلاح» عنوانين مختلفين.</b>
    ///
    /// <para>الاتنين بيقفلوا الأمر في نفس العمود، بس واحد معناه
    /// اللاب بقى جاهز والتاني معناه لسه فيه عطل — ودمجهم بيخلّي خط
    /// الزمن يقول «خلصت» على شغل ما خلصش.</para>
    /// </summary>
    [Fact]
    public void Completed_and_unable_do_not_share_a_title()
    {
        Assert.NotEqual(
            DeviceTimelineText.Title(DeviceTimelineEventType.RepairCompleted),
            DeviceTimelineText.Title(DeviceTimelineEventType.RepairUnableToRepair));
    }
}
