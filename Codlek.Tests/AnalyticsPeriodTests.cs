using Codlek.Core.Analytics;
using Codlek.Core.Time;

namespace Codlek.Tests;

/// <summary>
/// فترة التحليلات — <b>الحدود بأيام القاهرة</b>.
///
/// <para>🔴 جهاز بيتفحص ١٢:٣٠ بالليل بتوقيت القاهرة لازم يتحسب على
/// <b>يومه</b>، مش على اللي فات. ودي مش تفصيلة: شغل الوردية
/// المتأخرة كان بيروح لفني في يوم غلط.</para>
/// </summary>
public class AnalyticsPeriodTests
{
    /// <summary>
    /// 🔴 <b>الحد الأعلى مفتوح — والنهاية من اليوم اللي بعده.</b>
    ///
    /// <para>المقارنة في الاستعلامات <c>&lt; ToUtc</c>، فلو أخدنا
    /// بداية آخر يوم كان اليوم الأخير كله هيختفي من الفترة.</para>
    /// </summary>
    [Fact]
    public void The_upper_bound_is_the_start_of_the_day_after_the_last_one()
    {
        var period = AnalyticsPeriod.Resolve("today", null, null);

        Assert.Equal(CairoDay.Today, period.FirstCairoDay);
        Assert.Equal(CairoDay.Today, period.LastCairoDay);

        Assert.Equal(CairoDay.StartOf(CairoDay.Today), period.FromUtc);
        Assert.Equal(CairoDay.StartOf(CairoDay.Today.AddDays(1)), period.ToUtc);

        // ⚠️ والمدى بيغطّي آخر لحظة في اليوم.
        Assert.True(period.ToUtc > period.FromUtc.AddHours(23));
    }

    /// <summary>
    /// ⚠️ يوم واحد = تجميع بالساعة. وده اللي بيخلّي «الشغل بيتعمل
    /// امتى» سؤال له إجابة بدل عمود واحد عملاق.
    /// </summary>
    [Fact]
    public void A_single_day_is_hourly_and_anything_longer_is_daily()
    {
        Assert.True(AnalyticsPeriod.Resolve("today", null, null).Hourly);
        Assert.True(AnalyticsPeriod.SingleDay(CairoDay.Today).Hourly);

        Assert.False(AnalyticsPeriod.Resolve("7", null, null).Hourly);
        Assert.False(AnalyticsPeriod.Resolve("30", null, null).Hourly);
    }

    [Theory]
    [InlineData("today", 1)]
    [InlineData("7", 7)]
    [InlineData("30", 30)]
    [InlineData("90", 90)]
    public void The_fixed_keys_cover_the_right_number_of_days(string key, int days) =>
        Assert.Equal(days, AnalyticsPeriod.Resolve(key, null, null).Days);

    /// <summary>
    /// 🔴 <b>أي قيمة مش مفهومة بترجع لـ«آخر ٣٠ يوم» بدل ما
    /// ترمي.</b>
    ///
    /// <para>ده مدخل من المتصفح، والفترة مش قرار أمني — والوقوف هنا
    /// بيحوّل رابط قديم محفوظ لصفحة خطأ من غير أي مكسب.</para>
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("كلام")]
    [InlineData("999")]
    [InlineData("yesterday")]
    public void An_unknown_key_falls_back_to_thirty_days(string? key)
    {
        var period = AnalyticsPeriod.Resolve(key, null, null);

        Assert.Equal("30", period.Key);
        Assert.Equal(30, period.Days);
    }

    [Fact]
    public void The_month_key_starts_on_the_first_of_the_cairo_month()
    {
        var period = AnalyticsPeriod.Resolve("month", null, null);

        Assert.Equal("month", period.Key);
        Assert.Equal(1, period.FirstCairoDay.Day);
        Assert.Equal(CairoDay.Today.Month, period.FirstCairoDay.Month);
        Assert.Equal(CairoDay.Today, period.LastCairoDay);
    }

    // =================================================================
    //  الفترة المخصّصة
    // =================================================================

    /// <summary>
    /// ⚠️ من غير تاريخين مفهومين، «مخصّص» مالوش معنى — بنرجع
    /// للافتراضي بدل ما نخترع فترة.
    /// </summary>
    [Fact]
    public void Custom_without_both_dates_falls_back()
    {
        var day = CairoDay.Today.AddDays(-3);

        Assert.Equal("30", AnalyticsPeriod.Resolve("custom", null, null).Key);
        Assert.Equal("30", AnalyticsPeriod.Resolve("custom", day, null).Key);
        Assert.Equal("30", AnalyticsPeriod.Resolve("custom", null, day).Key);
    }

    /// <summary>⚠️ والمقلوب بيتصلّح بدل ما يترفض — النية واضحة.</summary>
    [Fact]
    public void Custom_dates_in_the_wrong_order_are_swapped()
    {
        var a = CairoDay.Today.AddDays(-10);
        var b = CairoDay.Today.AddDays(-3);

        var period = AnalyticsPeriod.Resolve("custom", b, a);

        Assert.Equal(a, period.FirstCairoDay);
        Assert.Equal(b, period.LastCairoDay);
    }

    /// <summary>
    /// 🔴 <b>المستقبل مالوش بيانات، والفترة المفتوحة بتخلّي
    /// الاستعلام يمسح الجدول كله.</b>
    /// </summary>
    [Fact]
    public void Custom_dates_in_the_future_are_pulled_back_to_today()
    {
        var period = AnalyticsPeriod.Resolve(
            "custom", CairoDay.Today.AddDays(-2), CairoDay.Today.AddYears(5));

        Assert.Equal(CairoDay.Today, period.LastCairoDay);
    }

    [Fact]
    public void A_custom_range_longer_than_a_year_is_clipped()
    {
        var period = AnalyticsPeriod.Resolve(
            "custom", CairoDay.Today.AddYears(-10), CairoDay.Today);

        // ⚠️ ٣٦٦ يوم + اليوم نفسه.
        Assert.Equal(367, period.Days);
        Assert.Equal("custom", period.Key);
    }

    /// <summary>
    /// ⚠️ يوم واحد مخصّص بياخد اسم اليوم بالعربي، والمدى بياخد
    /// التاريخين.
    /// </summary>
    [Fact]
    public void A_one_day_custom_range_is_labelled_with_the_arabic_date()
    {
        var day = CairoDay.Today.AddDays(-5);

        var single = AnalyticsPeriod.Resolve("custom", day, day);
        var span = AnalyticsPeriod.Resolve("custom", day, day.AddDays(2));

        Assert.Equal(ArabicDate.Long(day), single.Label);
        Assert.True(single.Hourly);

        Assert.Contains("←", span.Label);
        Assert.False(span.Hourly);
    }

    // =================================================================
    //  «من البداية»
    // =================================================================

    /// <summary>
    /// 🔴 <b><c>all</c> مصنع بالاسم، مش مفتاح جوّه القارئ.</b>
    ///
    /// <para>لو بقت مفتاح عادي، أي حد يبعت <c>?range=all</c> —
    /// ومنحنى الحجم بيعمل نقطة لكل يوم من أول يوم في الفترة لآخره،
    /// يعني رد فيه أكتر من عشرين ألف نقطة في طلب واحد.</para>
    /// </summary>
    [Fact]
    public void The_all_key_is_not_reachable_from_the_query_string()
    {
        var fromQuery = AnalyticsPeriod.Resolve("all", null, null);

        // ⚠️ «all» من الرابط بترجع للافتراضي.
        Assert.Equal("30", fromQuery.Key);

        // والمصنع بالاسم هو الطريق الوحيد.
        Assert.Equal("all", AnalyticsPeriod.Everything().Key);
    }

    /// <summary>
    /// ⚠️ <b>البداية <c>UnixEpoch</c> حرفياً</b>، مش معدّية على
    /// البنّاء: البنّاء كان هيحوّلها لبداية اليوم بتوقيت القاهرة
    /// (حد أوسع)، والقيمة دي بالظبط هي اللي القديم بيرجّعها — فالنقل
    /// مابيزحزحش ولا صف واحد.
    /// </summary>
    [Fact]
    public void Everything_starts_at_the_unix_epoch_exactly()
    {
        var period = AnalyticsPeriod.Everything();

        Assert.Equal(DateTime.UnixEpoch, period.FromUtc);
        Assert.Equal(CairoDay.StartOf(CairoDay.Today.AddDays(1)), period.ToUtc);
        Assert.False(period.Hourly);
    }

    // =================================================================
    //  التوقيت الصيفي
    // =================================================================

    /// <summary>
    /// 🔴 <b>مصر بتقدّم الساعة من نص الليل — فنص الليل نفسه مش
    /// موجود في اليوم ده.</b>
    ///
    /// <para>والدالة لازم تفضل ترجّع قيمة بدل ما ترمي: صفحة بتقع في
    /// يوم واحد في السنة أسوأ من فرق ساعة.</para>
    /// </summary>
    [Fact]
    public void A_spring_forward_day_still_resolves()
    {
        // ⚠️ آخر جمعة في أبريل — مصر بتقدّم الساعة فيها.
        var springForward = new DateTime(2026, 4, 24);

        var period = AnalyticsPeriod.SingleDay(springForward);

        Assert.Equal(springForward, period.FirstCairoDay);
        Assert.True(period.ToUtc > period.FromUtc);
        Assert.Equal(1, period.Days);
    }

    /// <summary>
    /// ⚠️ والإزاحة بتتغيّر بين الصيف والشتا — فأي <c>+2</c> مكتوبة
    /// في الكود بتغلط نص السنة.
    /// </summary>
    [Fact]
    public void The_offset_is_not_a_constant()
    {
        int winter = CairoDay.OffsetMinutes(new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc));
        int summer = CairoDay.OffsetMinutes(new DateTime(2026, 7, 15, 12, 0, 0, DateTimeKind.Utc));

        Assert.Equal(120, winter);
        Assert.Equal(180, summer);
    }

    /// <summary>⚠️ وبكرة مالوش بيانات — تقرير اليوم بيرجع للنهاردة.</summary>
    [Fact]
    public void A_future_day_report_is_pulled_back_to_today()
    {
        var period = AnalyticsPeriod.SingleDay(CairoDay.Today.AddDays(30));

        Assert.Equal(CairoDay.Today, period.FirstCairoDay);
    }

    [Fact]
    public void Days_is_never_less_than_one()
    {
        Assert.Equal(1, AnalyticsPeriod.Resolve("today", null, null).Days);
        Assert.True(AnalyticsPeriod.Everything().Days > 1);
    }
}
