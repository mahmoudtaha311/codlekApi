namespace Codlek.Core.Time;

/// <summary>
/// حدود اليوم بتوقيت القاهرة — <b>لفلترة التواريخ</b>.
///
/// <para>🔴 <b>مصر فيها توقيت صيفي، فمفيش إزاحة ثابتة.</b> أي
/// <c>+2</c> أو <c>+3</c> مكتوب في الكود بيغلط نص السنة — واللي بيبان
/// هو إن فلتر «من ١ أكتوبر» بيرجّع أوامر ٣٠ سبتمبر بالليل، أو بيخفي
/// أوامر ١ أكتوبر الصبح.</para>
///
/// <para>🔴 <b>والحد الأعلى مفتوح (exclusive).</b> «لـ٥ أكتوبر» معناها
/// «لحد نص ليل ٦ أكتوبر» — مش «لحد ٥ أكتوبر ١٢ بالظهر». ولو كان
/// مقفول، كل الأوامر اللي اتعملت في اليوم الأخير بعد اللحظة دي
/// بتختفي.</para>
/// </summary>
public static class CairoDay
{
    /// <summary>
    /// المنطقة الزمنية — <b>والاسم بيختلف بين لينكس وويندوز</b>.
    ///
    /// <para>⚠️ فبنجرّب الاتنين. ولو مفيش ولا واحد (حاوية من غير
    /// بيانات مناطق زمنية)، بنرجع لإزاحة ثابتة <c>+2</c> بدل ما
    /// التطبيق مايقلّعش — فرق ساعة في الصيف أهون من سيرفر واقف.</para>
    /// </summary>
    private static readonly TimeZoneInfo Zone = Resolve();

    private static TimeZoneInfo Resolve()
    {
        foreach (string id in new[] { "Africa/Cairo", "Egypt Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }

        return TimeZoneInfo.CreateCustomTimeZone(
            "Cairo-Fallback", TimeSpan.FromHours(2), "القاهرة", "القاهرة");
    }

    /// <summary>
    /// بداية اليوم ده بتوقيت القاهرة، كـUTC.
    ///
    /// <para>⚠️ بترجّع <c>null</c> للتاريخ الفاضي — «من» مش مكتوبة
    /// معناها «من الأول»، مش «من سنة ١».</para>
    /// </summary>
    public static DateTime? StartUtc(DateTime? cairoDate) =>
        cairoDate is { } date ? StartOfDayUtc(date) : null;

    /// <summary>
    /// <b>بعد</b> اليوم ده — يعني بداية اللي بعده، كـUTC.
    ///
    /// <para>🔴 دي اللي بتخلّي المدى <c>[start, after)</c> — والحد
    /// الأعلى مفتوح. استعمالها غلط (<c>&lt;=</c> بدل <c>&lt;</c>)
    /// بيضيف يوم كامل للنتيجة.</para>
    /// </summary>
    public static DateTime? AfterUtc(DateTime? cairoDate) =>
        cairoDate is { } date ? StartOfDayUtc(date.Date.AddDays(1)) : null;

    private static DateTime StartOfDayUtc(DateTime cairoDate)
    {
        try
        {
            var midnight = DateTime.SpecifyKind(cairoDate.Date, DateTimeKind.Unspecified);

            return TimeZoneInfo.ConvertTimeToUtc(midnight, Zone);
        }
        catch (ArgumentException)
        {
            /*
              🔴 **مصر بتقدّم الساعة من نص الليل.**

              يعني في يوم واحد في السنة، الساعة ١٢:٠٠ ص **مش موجودة
              أصلاً** — الساعة بتنقل من ١١:٥٩:٥٩ لـ١:٠٠:٠٠.
              و`ConvertTimeToUtc` بترمي `ArgumentException` على وقت
              مش موجود.

              ⚠️ والبديل إننا نستعمل إزاحة اليوم اللي قبله: فرق ساعة
              في يوم واحد في السنة — وأهون بكتير من صفحة بتقع.
            */
            var offset = Zone.GetUtcOffset(cairoDate.Date.AddDays(-1));

            return DateTime.SpecifyKind(cairoDate.Date - offset, DateTimeKind.Utc);
        }
        catch
        {
            // ⚠️ آخر ملجأ: نعتبره UTC. غلط بساعتين، بس مش بيوقّف حاجة.
            return DateTime.SpecifyKind(cairoDate.Date, DateTimeKind.Utc);
        }
    }

    /// <summary>
    /// وقت UTC بتوقيت القاهرة — <b>للعرض بس</b>.
    ///
    /// <para>⚠️ <b>ممنوع يتستعمل في مقارنة أو فلتر.</b> الفلترة
    /// بتحصل على UTC بـ<see cref="StartUtc"/> و
    /// <see cref="AfterUtc"/> — تحويل كل صف للقاهرة بعدين بيمنع
    /// SQL Server من إنه يستعمل الفهرس.</para>
    /// </summary>
    public static DateTime ToCairo(DateTime utc)
    {
        try
        {
            var source = utc.Kind == DateTimeKind.Local
                ? utc.ToUniversalTime()
                : DateTime.SpecifyKind(utc, DateTimeKind.Utc);

            return TimeZoneInfo.ConvertTimeFromUtc(source, Zone);
        }
        catch
        {
            return utc;
        }
    }
}
