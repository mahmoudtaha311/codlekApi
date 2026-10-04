using Codlek.Core.Time;

namespace Codlek.Core.Analytics;

/// <summary>
/// الفترة اللي اللوحة كلها بتتحسب عليها — <b>مصدر واحد</b>.
///
/// <para>🔴 <b>الحدود بأيام القاهرة.</b> جهاز بيتفحص ١٢:٣٠ بالليل
/// بتوقيت القاهرة لازم يتحسب على <b>يومه</b>، مش على اللي فات.
/// المقارنة بتتعمل UTC لأن ده اللي متخزّن، بس الحدود نفسها بتتبني
/// من تواريخ قاهرية.</para>
/// </summary>
/// <param name="ToUtc">
/// 🔴 <b>النهاية بتتبني من اليوم اللي <u>بعده</u>.</b> المقارنة في
/// الاستعلامات <c>&lt; ToUtc</c>، فلو أخدنا بداية آخر يوم كان اليوم
/// الأخير كله هيختفي من الفترة.
/// </param>
/// <param name="Hourly">
/// ⚠️ يوم واحد مابيتعرضش كعمود واحد عملاق: بيتفكّ على ٢٤ ساعة، وده
/// اللي بيخلّي «الشغل بيتعمل امتى» سؤال له إجابة.
/// </param>
public sealed record AnalyticsPeriod(
    DateTime FromUtc,
    DateTime ToUtc,
    string Key,
    string Label,
    DateTime FirstCairoDay,
    DateTime LastCairoDay,
    bool Hourly)
{
    /// <summary>عدد أيام القاهرة اللي الفترة بتغطّيها — واحد على الأقل.</summary>
    public int Days => Math.Max(1, (LastCairoDay - FirstCairoDay).Days + 1);

    /// <summary>
    /// الإزاحة المستعملة في تجميع SQL — راجع
    /// <see cref="CairoDay.OffsetMinutes"/>.
    /// </summary>
    public int OffsetMinutes => CairoDay.OffsetMinutes(FromUtc);

    /// <summary>أقصى عدد أيام مسموح في فترة مخصّصة.</summary>
    private const int MaxCustomDays = 366;

    /// <summary>
    /// بيقرا الفترة من الاستعلام: <c>today · 7 · 30 · 90 · month ·
    /// custom</c>.
    /// </summary>
    /// <param name="to">
    /// نهاية الفترة المخصّصة — <b>شاملة</b>، يعني اليوم ده جوّه
    /// الفترة.
    /// </param>
    /// <remarks>
    /// ⚠️ <b>أي قيمة مش مفهومة بترجع لـ«آخر ٣٠ يوم» بدل ما ترمي.</b>
    /// ده مدخل من المتصفح، والفترة مش قرار أمني — والوقوف هنا بيحوّل
    /// رابط قديم محفوظ لصفحة خطأ من غير أي مكسب.
    /// </remarks>
    public static AnalyticsPeriod Resolve(string? key, DateTime? from, DateTime? to)
    {
        var today = CairoDay.Today;

        AnalyticsPeriod Span(int daysBack, string k, string label) =>
            Build(today.AddDays(-daysBack), today, k, label);

        switch ((key ?? "30").Trim().ToLowerInvariant())
        {
            case "today":
                return Build(today, today, "today", "النهاردة");

            case "7":
                return Span(6, "7", "آخر ٧ أيام");

            case "90":
                return Span(89, "90", "آخر ٩٠ يوم");

            case "month":
                return Build(new DateTime(today.Year, today.Month, 1), today, "month", "الشهر ده");

            case "custom":
            {
                // ⚠️ من غير تاريخين مفهومين، «مخصّص» مالوش معنى —
                // بنرجع للافتراضي بدل ما نخترع فترة.
                if (from is null || to is null) return Span(29, "30", "آخر ٣٠ يوم");

                var a = from.Value.Date;
                var b = to.Value.Date;

                // ⚠️ المقلوب بيتصلّح بدل ما يترفض — المستخدم ملخبط
                // الخانتين، والنية واضحة.
                if (b < a) (a, b) = (b, a);

                /*
                  🔴 **المستقبل مالوش بيانات، والفترة المفتوحة بتخلّي
                  الاستعلام يمسح الجدول كله.**
                */
                if (b > today) b = today;
                if (a > b) a = b;
                if ((b - a).Days > MaxCustomDays) a = b.AddDays(-MaxCustomDays);

                string label = a == b
                    ? ArabicDate.Long(a)
                    : $"{a:yyyy-MM-dd} ← {b:yyyy-MM-dd}";

                return Build(a, b, "custom", label);
            }

            default:
                return Span(29, "30", "آخر ٣٠ يوم");
        }
    }

    /// <summary>فترة ليوم واحد — تقرير اليوم بيستعملها.</summary>
    public static AnalyticsPeriod SingleDay(DateTime cairoDay)
    {
        var day = cairoDay.Date;

        // ⚠️ بكرة مالوش بيانات.
        if (day > CairoDay.Today) day = CairoDay.Today;

        return Build(day, day, "day", ArabicDate.Long(day));
    }

    /// <summary>
    /// من أول يوم في النظام لحد النهاردة — <b>مفتاح <c>all</c></b>.
    ///
    /// <para>🔴 <b>ودي مصنع بالاسم عن قصد، مش حالة جوّه
    /// <see cref="Resolve"/>.</b> لو <c>all</c> بقت مفتاح عادي، أي
    /// حد يبعت <c>?range=all</c> — ومنحنى الحجم بيعمل نقطة لكل يوم
    /// من أول يوم في الفترة لآخره، يعني رد فيه أكتر من عشرين ألف
    /// نقطة في طلب واحد. الشاشة اللي محتاجة «من الأول» هي شاشة
    /// الفنيين، وهي مابترسمش خط زمني.</para>
    ///
    /// <para>⚠️ <b>والبداية <c>UnixEpoch</c> حرفياً</b>، مش معدّية
    /// على البنّاء: البنّاء كان هيحوّلها لبداية اليوم بتوقيت القاهرة
    /// (‏1969-12-31T22:00:00Z) — حد أوسع. والقيمة دي بالظبط هي اللي
    /// القديم بيرجّعها، فالنقل مابيزحزحش ولا صف واحد.</para>
    ///
    /// <para>⚠️ <b>و<see cref="OffsetMinutes"/> على الفترة دي محسوب
    /// من سنة ١٩٧٠</b>، فممنوع تتغذّى منها أي استعلام بيقسّم بالساعة
    /// أو باليوم. دي حدّين وخلاص.</para>
    /// </summary>
    public static AnalyticsPeriod Everything() =>
        new(DateTime.UnixEpoch,
            CairoDay.StartOf(CairoDay.Today.AddDays(1)),
            "all",
            "من البداية",
            DateTime.UnixEpoch.Date,
            CairoDay.Today,
            Hourly: false);

    private static AnalyticsPeriod Build(
        DateTime firstCairoDay, DateTime lastCairoDay, string key, string label) =>
        new(
            CairoDay.StartOf(firstCairoDay),

            // 🔴 اليوم اللي بعده — راجع التعليق على `ToUtc`.
            CairoDay.StartOf(lastCairoDay.AddDays(1)),

            key,
            label,
            firstCairoDay,
            lastCairoDay,

            // ⚠️ يوم واحد = تجميع بالساعة.
            Hourly: firstCairoDay == lastCairoDay);
}
