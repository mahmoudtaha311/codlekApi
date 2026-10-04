namespace Codlek.Core.Reports;

/// <summary>
/// نوع الفحص: كامل ولا جزئي.
///
/// <para>⚠️ <c>null</c> بيرجّع نص <b>فاضي</b> مش «غير معروف» —
/// الفحوص القديمة مالهاش القيمة دي أصلاً، و«غير معروف» جمب كل صف
/// قديم بيبان كأن فيه بيانات ناقصة.</para>
/// </summary>
public static class ReportScopeText
{
    public static string Arabic(int? scope) => scope switch
    {
        0 => "فحص كامل",
        1 => "فحص جزئي",
        _ => "",
    };
}
