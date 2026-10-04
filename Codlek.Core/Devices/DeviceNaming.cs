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
///
/// <para>🔴 <b>وفي القديم فيه قاعدة <u>تانية</u> أغنى من دي — ومقصود
/// إننا مش بناخدها.</b></para>
///
/// <para><c>CodlekWeb.Services.DeviceNaming</c> فيه
/// <c>Compose</c>/<c>Display</c> بيعملوا حاجتين زيادة: بيشيلوا كود
/// المصنّع من أول الاسم (<c>103C_5336AN HP EliteBook</c> — و١٢ جهاز
/// في الإنتاج اتخزّنوا كده)، وبيمنعوا تكرار الماركة
/// (<c>HP</c> + <c>HP ProBook</c> = <c>HP ProBook</c> مش
/// <c>HP HP ProBook</c>).</para>
///
/// <para>🔴 <b>بس القاعدة دي بتتستعمل في
/// <c>Device.LaptopName</c>/<c>Report.LaptopName</c> وبس — وهما
/// بيتعرضوا في <u>صفحات Razor</u> القديمة لوحدها.</b> كل نقطة
/// <c>/api/v1</c> في القديم بتحسب الاسم بإيدها بالقاعدة البسيطة:
/// <c>/api/v1/repairs</c> بـ<c>$"{manufacturer} {commercial ?? raw}"</c>
/// (نعم، بتكرّر الماركة)، والتصدير بـ<c>commercial ?? raw</c> من غير
/// ماركة خالص.</para>
///
/// <para>⚠️ <b>فالمشروع ده بيطابق <c>/api/v1</c> مش صفحات
/// Razor</b> — الداش بورد بتقرا الـAPI، والصفحات دي مش بتتنقل.
/// وجرّبنا «نصلّحها» مرة: الفحص اللي بيثبّت
/// <c>HP HP ProBook</c> وقع، ورجعنا — <b>الفحص كان صح والتصليح كان
/// هو الانحراف</b>.</para>
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
