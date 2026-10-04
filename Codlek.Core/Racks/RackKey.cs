namespace Codlek.Core.Racks;

/// <summary>
/// مفتاح محطة الفحص — <b>قواعد شكله ونطاق البحث بيه</b>.
///
/// <para>🔴 <b>والمفتاح ده هو كل التحقق في سطح الراكة.</b> تسع نقط
/// معتمدة عليه، ومفيش فيهم ولا واحدة بتاخد معرّف شركة من الطلب:
/// الشركة بتتقرا <b>من المحطة</b> بعد ما مفتاحها يتحقق. يعني
/// الوصول العابر للشركات مش «ممنوع» — هو مش موجود كمسار
/// أصلاً.</para>
/// </summary>
public static class RackKey
{
    /// <summary>الترويسة اللي الراكة بتبعت فيها مفتاحها.</summary>
    public const string Header = "X-Api-Key";

    /// <summary>
    /// أقصر مفتاح يستاهل نبص عليه.
    ///
    /// <para>⚠️ <b>الرفض ده قبل القاعدة.</b> مفتاح من حرفين مش
    /// مفتاح، وتمرير أي حاجة للقاعدة معناه استعلام على كل طلب
    /// عابر.</para>
    /// </summary>
    public const int MinLength = 8;

    /// <summary>
    /// طول البادئة المتخزّنة.
    ///
    /// <para>🔴 <b>البادئة بتضيّق البحث من غير ما تكشف المفتاح.</b>
    /// التحقق التشفيري غالي، فتشغيله على <b>كل</b> راكة في القاعدة
    /// مع كل طلب كان بيخلّي كل مزامنة تكلّف الورشة وقت معالج
    /// حقيقي. والبادئة بتقلّل المرشّحين لواحد عملياً، والتحقق
    /// الحقيقي بيتعمل على اللي فاضل.</para>
    ///
    /// <para>⚠️ وعشرة حروف مش رقم عشوائي — ده طول العمود
    /// <c>Rack.KeyPrefix</c> بالظبط.</para>
    /// </summary>
    public const int PrefixLength = 10;

    /// <summary>
    /// المفتاح ده يستاهل بحث؟
    ///
    /// <para>⚠️ بيشيل المساحات الأول: الراكة بتقرا المفتاح من ملف
    /// إعدادات، والسطر بييجي فيه <c>\r\n</c>.</para>
    /// </summary>
    public static bool Usable(string? key) =>
        (key ?? "").Trim().Length >= MinLength;

    /// <summary>المفتاح بعد التنضيف — زي ما التحقق هيشوفه.</summary>
    public static string Clean(string? key) => (key ?? "").Trim();

    /// <summary>
    /// بادئة البحث.
    ///
    /// <para>⚠️ <c>Math.Min</c> موجودة عشان مفتاح أقصر من البادئة
    /// مايرميش — و<see cref="Usable"/> المفروض رفضه قبل كده، بس
    /// الدالة دي مابتفترضش.</para>
    /// </summary>
    public static string Prefix(string? key)
    {
        string clean = Clean(key);

        return clean[..Math.Min(PrefixLength, clean.Length)];
    }
}
