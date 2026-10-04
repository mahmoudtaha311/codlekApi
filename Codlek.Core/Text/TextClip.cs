namespace Codlek.Core.Text;

/// <summary>
/// قص النصوص لطول العمود — <b>قبل ما EF تشوفها</b>.
///
/// <para>🔴 <b>بنقص مابنرفضش.</b> لقطة جاية من راكة أطول من العمود
/// معناها استثناء قص من SQL Server عند الحفظ، والحفظ ده في مسار
/// المزامنة — والرفض هناك <b>نهائي</b>، فشغل حصل فعلاً على البنش
/// بيتمسح. القص بيحفظ الحقيقة ناقصة بدل ما يمسحها.</para>
///
/// <para>⚠️ والقص بالوحدات (UTF-16) فممكن يقسم زوج بديل — مش مشكلة
/// للعربي، بس مشكلة للإيموجي.</para>
/// </summary>
public static class TextClip
{
    /// <summary>أطوال الأعمدة — <b>مطابقة لـ[MaxLength] بالحرف</b>.</summary>
    public static class Lengths
    {
        public const int PublicCode = 20;
        public const int ActorType = 20;
        public const int PersonName = 120;
        public const int Reason = 400;
        public const int IssueCode = 40;
        public const int IssueTitle = 160;
        public const int IssueCategory = 40;
        public const int PartName = 200;
        public const int InventoryCode = 60;
        public const int SerialNumber = 120;
    }

    /// <summary>عمره ما بيرجّع <c>null</c>، وعمره ما بيعدّي الطول.</summary>
    public static string To(string? value, int max)
    {
        if (string.IsNullOrEmpty(value)) return "";

        return value.Length <= max ? value : value[..max];
    }
}
