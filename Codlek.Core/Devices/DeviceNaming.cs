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
        string? manufacturer, string? commercialModelName, string? rawModel)
    {
        string model = string.IsNullOrWhiteSpace(commercialModelName)
            ? rawModel ?? ""
            : commercialModelName;

        return $"{manufacturer} {model}".Trim();
    }
}
