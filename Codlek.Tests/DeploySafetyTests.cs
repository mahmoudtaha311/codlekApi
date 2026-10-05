using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Codlek.Tests;

/// <summary>
/// حاجات في ملفات <c>deploy/</c> لو اتشالت <b>النشر بيأذي حاجة شغّالة</b>
/// — موقع تاني، أو شهادة الموقع، أو سر بيتنسخ في مكان مش مكانه.
///
/// <para>⚠️ الفحوص بتقرا الملفات كنص. تشغيل السكريبت نفسه محتاج
/// <c>msdeploy</c> وملف نشر حقيقي، والاتنين مش موجودين على أي جهاز فحص
/// — والمطلوب هنا إن السطر الحامي مايتشالش من غير ما حد ياخد باله.</para>
/// </summary>
public class DeploySafetyTests
{
    private static string DeployFile(string name)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "CodlekApi.sln")))
            dir = dir.Parent;

        Assert.NotNull(dir);

        return File.ReadAllText(Path.Combine(dir!.FullName, "deploy", name));
    }

    private static readonly Regex Placeholder = new(@"__[A-Z][A-Z_]*__");

    /// <summary>
    /// 🔴 <b>سكريبت الرفع مابيرفعش غير على مواقع التجربة.</b> هو بيخلّي
    /// الموقع نسخة طبق الأصل من المجلد — اللي مش في المجلد بيتمسح. ملف نشر
    /// الإنتاج بالغلط كان هيمسح البرنامج الشغّال، وده اتلقط في مراجعة بيئة
    /// التجربة يوم ٥ أكتوبر.
    ///
    /// <para>⚠️ والفحص لازم يبقى <b>قبل</b> الاتصال — فحص بعد الرفع مالوش
    /// لازمة.</para>
    /// </summary>
    [Fact]
    public void Deploy_script_refuses_every_site_but_the_test_sites_before_connecting()
    {
        string script = DeployFile("deploy-site.ps1");

        int allowList = script.IndexOf("$TestSites = @(", StringComparison.Ordinal);
        int refusal = script.IndexOf("$TestSites -notcontains $site", StringComparison.Ordinal);
        int upload = script.IndexOf("& $MsDeploy", StringComparison.Ordinal);

        Assert.True(allowList >= 0, "قايمة مواقع التجربة اتشالت من deploy-site.ps1");
        Assert.True(refusal > allowList, "رفض المواقع اللي برّه القايمة اتشال");
        Assert.True(upload > refusal, "الرفض لازم يبقى قبل ما msdeploy يتنده");
    }

    /// <summary>
    /// 🔴 <b>مجلد <c>.well-known</c> مابيتلمسش.</b> الاستضافة بتحط فيه
    /// ملفات تجديد شهادة HTTPS. الرفع بالمراية كان هيمسحها (أول تجربة
    /// جافة ورّت كده)، والتجديد كان هيفشل بعد أسابيع من غير سبب باين.
    /// </summary>
    [Fact]
    public void Deploy_script_never_touches_the_certificate_folder()
    {
        Assert.Contains(@"-skip:objectName=dirPath,absolutePath=\.well-known",
            DeployFile("deploy-site.ps1"));
    }

    /// <summary>
    /// ⚠️ <b>شهادة نقطة الرفع بتتراجع.</b> <c>-allowUntrusted</c> كان
    /// بيقبل أي شهادة — يعني اللي في النص يستلم باسورد الاستضافة. نقط
    /// MonsterASP عندها شهادة سليمة (اتأكدنا يوم ٥ أكتوبر)، فمفيش داعي.
    /// </summary>
    [Fact]
    public void Deploy_script_checks_the_host_certificate()
    {
        Assert.DoesNotContain("-allowUntrusted", DeployFile("deploy-site.ps1"));
    }

    /// <summary>
    /// 🔴 <b>أسماء الخانات في القالب جوّه <c>value="..."</c> بس.</b>
    /// الملف الحقيقي بيتعمل ببحث واستبدال، والقالب كان بيذكر الخانات
    /// في التعليق اللي فوق — فنص الاتصال بالباسورد ومفتاح JWT اتنسخوا
    /// في التعليق كمان، تحت جملة «TEMPLATE ONLY».
    /// </summary>
    [Fact]
    public void Web_config_template_spells_placeholders_only_inside_values()
    {
        var doc = XDocument.Parse(DeployFile("api.web.config.template"));

        foreach (var comment in doc.DescendantNodes().OfType<XComment>())
            Assert.DoesNotMatch(Placeholder, comment.Value);

        var inValues = doc.Descendants()
            .SelectMany(e => e.Attributes())
            .Where(a => Placeholder.IsMatch(a.Value))
            .ToList();

        Assert.NotEmpty(inValues);
        Assert.All(inValues, a => Assert.Equal("value", a.Name.LocalName));
    }

    private static XElement DashboardRule(string name) =>
        XDocument.Parse(DeployFile("dashboard.web.config"))
            .Descendants("rule")
            .Single(r => (string?)r.Attribute("name") == name);

    /// <summary>
    /// ⚠️ <b>ملف سكريبت ناقص بيرجع ٤٠٤ مش الصفحة.</b> بعد كل رفع، التبويب
    /// المفتوح بيطلب أسماء الملفات القديمة. الصفحة مكان السكريبت بتطلّع خطأ
    /// «module» ملخبط بدل ما يبان إن الملف مش موجود.
    /// </summary>
    [Fact]
    public void Dashboard_fallback_never_answers_a_missing_asset_with_the_page()
    {
        var guards = DashboardRule("dashboard-routes")
            .Descendants("add")
            .Where(c => (string?)c.Attribute("input") == "{URL}"
                     && (string?)c.Attribute("negate") == "true")
            .Select(c => (string?)c.Attribute("pattern"));

        Assert.Contains("^/app/assets/", guards);
    }

    /// <summary>
    /// 🔴 <b>اللوحة على https بس — و<c>.well-known</c> برّه التحويل.</b>
    /// على http الصفحة بتتغيّر في السكة والمتصفح بيقفل
    /// <c>navigator.locks</c>، فتبويبين بيجدّدوا التوكن مع بعض والسيرفر
    /// بيقفل كل جلسات الحساب. وشهادة الموقع بتتجدّد على http، فالتحويل
    /// مايلمسهاش.
    ///
    /// <para>⚠️ والتحويل <b>مش دايم (٣٠١)</b>: المتصفح بيحفظ الدايم للأبد،
    /// ومشكلة في الشهادة بعدين ماكانتش هتتصلّح من هنا.</para>
    /// </summary>
    [Fact]
    public void Dashboard_goes_to_https_but_leaves_the_certificate_folder_on_http()
    {
        var rule = DashboardRule("https-only");

        var conditions = rule.Descendants("add").ToList();
        Assert.Contains(conditions, c => (string?)c.Attribute("input") == "{HTTPS}");
        Assert.Contains(conditions, c => (string?)c.Attribute("negate") == "true"
                                      && ((string?)c.Attribute("pattern") ?? "").Contains("well-known"));

        var action = rule.Element("action");
        Assert.NotNull(action);
        Assert.StartsWith("https://", (string?)action!.Attribute("url"));
        Assert.NotEqual("Permanent", (string?)action.Attribute("redirectType"));
    }
}
