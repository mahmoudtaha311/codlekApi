namespace Codlek.Core.Devices;

/// <summary>
/// اسم اللاب اللي الناس بتستعمله.
///
/// <para>⚠️ <b>الاسم التجاري بيكسب الموديل الخام</b> — والرجوع للخام
/// بس لما التجاري فاضي. والقاعدة دي مكتوبة في مكان واحد عشان كل شاشة
/// تقراها من نفس المكان؛ النسخ باليد معناه إن أول تغيير في القاعدة
/// بيسيب شاشة ورا.</para>
///
/// <para>⚠️ و<c>Trim()</c> هو اللي بيمنع لاب مصنّعه فاضي يتعرض بمسافة
/// في الأول.</para>
/// </summary>
public static class DeviceNaming
{
    public static string Display(
        string? manufacturer, string? commercialModelName, string? rawModel) =>
        $"{manufacturer} {Model(commercialModelName, rawModel)}".Trim();

    /// <summary>
    /// الموديل لوحده — <b>من غير الماركة</b>.
    ///
    /// <para>⚠️ التصدير محتاج ده: الماركة عمود مستقل في الشيت،
    /// فحشرها في عمود الموديل بتدّي «HP HP ProBook». والقاعدة
    /// (التجاري بيكسب الخام) واحدة في الحالتين — عشان كده
    /// <see cref="Display"/> بينده الدالة دي بدل ما يكرّرها.</para>
    /// </summary>
    public static string Model(string? commercialModelName, string? rawModel) =>
        string.IsNullOrWhiteSpace(commercialModelName)
            ? rawModel ?? ""
            : commercialModelName.Trim();
}
