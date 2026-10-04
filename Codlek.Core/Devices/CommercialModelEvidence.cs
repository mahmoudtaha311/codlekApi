namespace Codlek.Core.Devices;

/// <summary>
/// اختيار الاسم التجاري للجهاز من فحوصه — <b>من دليل متخزّن بس</b>.
///
/// <para>🔴 <b>المشكلة اللي اتعمل عشانها:</b> الاسم التجاري كان
/// بيتحط على <b>الفحص</b> ومابيتحطش على <b>الجهاز</b>. فصفحة الأجهزة
/// فضلت تعرض <c>LENOVO 81FK</c> رغم إن فحوص نفس الجهاز شايلة
/// <c>ideapad 330-15ICH</c>.</para>
///
/// <para>🔴 <b>ومفيش اختراع أسامي هنا خالص.</b> مفيش بحث في النت،
/// ومفيش جدول تخمين، ومفيش اشتقاق من كود المصنع. القيمة الوحيدة اللي
/// بتتكتب هي قيمة <b>الراكة قرأتها من عتاد اللاب نفسه</b> واتخزّنت
/// في فحص. لو مفيش فحص شايل قيمة، الجهاز بيفضل على الخام — وده
/// الصح.</para>
///
/// <para>⚠️ <b>والأحدث مش معناه الأصح.</b> بناخد أحدث فحص <b>بمصدر
/// موثوق</b>؛ فحص أحدث من غير مصدر مابيدهسش قيمة موجودة.</para>
/// </summary>
public static class CommercialModelEvidence
{
    /// <summary>
    /// المصادر اللي بنثق فيها.
    ///
    /// <para>🔴 دي أسامي المصادر اللي <b>الراكة</b> بتكتبها. أي قيمة
    /// بمصدر مش في القايمة دي بتتجاهل — وده اللي بيمنع «إدخال يدوي»
    /// غلط أو مصدر جديد مش متحقق منه إنه يتسرّب لصفحة
    /// الأجهزة.</para>
    /// </summary>
    public static readonly IReadOnlyList<string> TrustedSources =
    [
        "SMBIOS (Product Version)",
        "SystemFamily",
        "Model",
        "SKU",
    ];

    /// <summary>المصدر ده موثوق؟</summary>
    public static bool IsTrusted(string? source) =>
        !string.IsNullOrWhiteSpace(source)
        && TrustedSources.Any(t =>
            string.Equals(t, source.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// القيمة دي صالحة كاسم تجاري؟
    ///
    /// <para>🔴 <b>المصدر الموثوق مش كفاية — القيمة نفسها لازم
    /// تتفحص.</b> <c>SystemFamily</c> مصدر موثوق فعلاً، بس على HP
    /// بيرجّع <c>103C_5336AN HP EliteBook</c> — كود مصنّع مش اسم.
    /// <b>١٢ جهاز في الإنتاج</b> اتخزّنوا كده.</para>
    ///
    /// <para>⚠️ <b>وبنرفض القيمة، مابنوسّعش ولا بنضيّق
    /// <see cref="TrustedSources"/>:</b> شيل <c>SystemFamily</c> من
    /// القايمة كان هيكسّر لينوفو (عيلتهم اسم حقيقي)، وده أوسع من
    /// العطل.</para>
    /// </summary>
    public static bool IsUsableName(string? value)
    {
        string text = (value ?? "").Trim();

        return text.Length > 0 && !DeviceNaming.IsOemCodeName(text);
    }

    /// <summary>
    /// أحدث دليل <b>موثوق وصالح</b> من القايمة — أو <c>-1</c>.
    /// </summary>
    /// <param name="sources">مصادر الأدلة، <b>من الأحدث للأقدم</b>.</param>
    /// <param name="names">الأسامي بنفس الترتيب.</param>
    /// <returns>
    /// فهرس الدليل المختار، أو <c>-1</c> لو مفيش دليل موثوق.
    /// </returns>
    /// <remarks>
    /// ⚠️ <b>بيرجّع فهرس مش قيمة</b> عشان المنادي ياخد الصف كله
    /// (الاسم والمصدر وكود المصنع) من نفس الدليل — خلطهم من أدلة
    /// مختلفة بيدّي جهاز باسم من فحص ومصدر من فحص تاني.
    /// </remarks>
    public static int Pick(IReadOnlyList<string?> sources, IReadOnlyList<string?> names)
    {
        int count = Math.Min(sources.Count, names.Count);

        for (int i = 0; i < count; i++)
        {
            if (IsTrusted(sources[i]) && IsUsableName(names[i])) return i;
        }

        return -1;
    }
}
