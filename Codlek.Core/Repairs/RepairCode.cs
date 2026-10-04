namespace Codlek.Core.Repairs;

/// <summary>
/// رقم أمر الصيانة — <b>RP- وتمن أرقام بالظبط</b>.
///
/// <para>🔴 <b>الشكل ده مطبوع على ورق.</b> والبحث في القايمة مطابقة
/// تامة (<c>PublicCode == raw</c>)، فتغيير الحشو بيخلّي الأوامر
/// الموجودة مش قابلة للبحث برقمها المكتوب.</para>
/// </summary>
public static class RepairCode
{
    /// <summary>
    /// مفتاح العدّاد في <c>TenantCounters</c>.
    ///
    /// <para>🔴 <b>مستقل عن عدّاد الأجهزة والراكات.</b> RP-1 مالهاش
    /// أي علاقة بـLP-1. وإعادة تسمية المفتاح بتبدأ العدّ من ١ من
    /// جديد وبتكرّر أرقام موجودة.</para>
    /// </summary>
    public const string CounterName = "repair";

    public const string Prefix = "RP-";

    /// <summary>
    /// ⚠️ العدّاد بيزيد في جملة واحدة ومابيرجعش لورا — حتى لو الأمر
    /// اتلغى. الرقم عمره ما بيتعاد استخدامه.
    /// </summary>
    public static string Format(int number) => Prefix + number.ToString("D8");
}
