using Codlek.Core.Time;

namespace Codlek.Application.Features.Export;

/// <summary>قيم الخلايا المشتركة بين ملفات التصدير.</summary>
internal static class ExportValues
{
    /// <summary>
    /// UTC ← توقيت القاهرة.
    ///
    /// <para>🔴 <b>لازم يتحوّل هنا.</b> إكسل مافيهوش فكرة المناطق
    /// الزمنية: الرقم اللي بيتكتب بيتعرض زي ما هو. لو اتكتب UTC،
    /// المدير هيقرا فحص الساعة ٣ العصر على إنه ١٢ الضهر.</para>
    /// </summary>
    public static DateTime Cairo(DateTime utc) => CairoDay.ToCairo(utc);

    /// <summary>⚠️ والفاضي بيفضل فاضي — مش ١٨٩٩.</summary>
    public static DateTime? Cairo(DateTime? utc) =>
        utc.HasValue ? CairoDay.ToCairo(utc.Value) : null;

    /// <summary>
    /// مدة بالدقايق لأقرب عشر.
    ///
    /// <para>⚠️ المصدر مللي ثانية، والملف بيتقرا بالدقيقة —
    /// والمدير بيجمع العمود ده في إكسل.</para>
    /// </summary>
    public static double Minutes(long milliseconds) =>
        Math.Round(milliseconds / 60000.0, 1);

    public static string YesNo(bool value) => value ? "نعم" : "لأ";
}
