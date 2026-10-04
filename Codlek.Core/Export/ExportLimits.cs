namespace Codlek.Core.Export;

/// <summary>
/// سقوف التصدير.
///
/// <para>🔴 <b>والقص بيتقال <u>جوّه الملف</u>.</b> سطر أخير بيقول إن
/// فيه صفوف ماطلعتش. ملف مقصوص في صمت بيتقري على إنه كل البيانات —
/// والمدير بيبني عليه قرار جرد.</para>
/// </summary>
public static class ExportLimits
{
    /// <summary>
    /// أقصى عدد صفوف في الصفحة الواحدة.
    ///
    /// <para>⚠️ <b>الملف بيتبني في الذاكرة.</b> <c>ZipArchive</c>
    /// بيكتب متزامن، وASP.NET Core بيمنع الكتابة المتزامنة على
    /// <c>Response.Body</c> افتراضياً — وفتح الباب ده عشان التصدير
    /// بيفتحه للتطبيق كله. فالسقف هو اللي بيخلّي الحجم معروف.</para>
    /// </summary>
    public const int MaxRows = 50_000;

    /// <summary>
    /// أقصى عدد صفحات «فني لكل صفحة».
    ///
    /// <para>⚠️ إكسل بيقبل صفحات كتير، بس ملف فيه ٢٠٠ صفحة مالوش أي
    /// فايدة عملية — والسقف بيتقال في صفحة الملخّص.</para>
    /// </summary>
    public const int MaxSheets = 40;

    public const string TruncationNote =
        "⚠️ الملف وقف عند الحد الأقصى — فيه صفوف تانية ماطلعتش. ضيّق الفلاتر.";

    /// <summary>
    /// بيقصّ القايمة ويضيف سطر التحذير لو زادت.
    ///
    /// <para>⚠️ دالة نقية — القص بيتقاس من غير ما حد يزرع ٥٠ ألف
    /// صف في قاعدة.</para>
    /// </summary>
    public static List<object?[]> Capped<T>(
        IReadOnlyList<T> rows, Func<T, object?[]> map)
    {
        var list = new List<object?[]>(Math.Min(rows.Count, MaxRows) + 1);

        for (int i = 0; i < rows.Count; i++)
        {
            if (i >= MaxRows)
            {
                list.Add([TruncationNote]);
                break;
            }

            list.Add(map(rows[i]));
        }

        return list;
    }
}
