namespace Codlek.Core.Paging;

/// <summary>
/// تصفيح — <b>بيقصّ مابيرفضش</b>.
///
/// <para>⚠️ <c>?page=0</c> أو <c>?pageSize=-5</c> لازم يرجّعوا أول
/// صفحة، مش <c>400</c>: المشروع القديم كده، والداش بورد معتمدة عليه.
/// والقص هو اللي بيمنع <c>Skip</c> بقيمة سالبة.</para>
///
/// <para>🔴 و<see cref="MaxPageSize"/> هو الحاجز الوحيد اللي بيمنع
/// <c>?pageSize=1000000</c> من إنه يسحب الجدول كله.</para>
/// </summary>
public static class Paging
{
    public const int MaxPageSize = 200;

    public const int DefaultPageSize = 40;

    public static (int Page, int Size) Clamp(
        int? page, int? pageSize, int fallback = DefaultPageSize)
    {
        int p = page is null or < 1 ? 1 : page.Value;
        int s = pageSize is null or < 1 ? fallback : Math.Min(pageSize.Value, MaxPageSize);

        return (p, s);
    }

    /// <summary>عدد الصفحات — ومفيش قسمة على صفر.</summary>
    public static int TotalPages(int totalItems, int pageSize) =>
        (int)Math.Ceiling(totalItems / (double)Math.Max(1, pageSize));
}
