namespace Codlek.Core.Racks;

/// <summary>
/// عنوان المزامنة اللي بيترجّع للراكة — <b>من الإعدادات، مش من
/// الطلب</b>.
///
/// <para><b>العطل اللي الملف ده اتعمل عشانه.</b> رد تسجيل الراكة كان
/// بيبني العنوان كده:</para>
///
/// <code>syncUrl = $"{Request.Scheme}://{Request.Host}/api/sync/reports"</code>
///
/// <para>🔴 و<c>Request.Host</c> جايّة من ترويسة <c>Host</c> اللي
/// العميل نفسه بيبعتها. يعني اللي بيسجّل راكة كان بيقدر يقول
/// للسيرفر «إنت مين». والعنوان ده <b>مش بيتستخدم مرة واحدة</b> —
/// الراكة بتخزّنه وبتفضل ترفع عليه كل الفحوص بعد كده. يعني ترويسة
/// واحدة مزوّرة = كل شغل المحطة بيروح لسيرفر غريب، والراكة شايفة إن
/// كل حاجة تمام.</para>
///
/// <para>⚠️ ونفس الكلام على <c>X-Forwarded-Host</c> — هي كمان ترويسة
/// من العميل. وحتى ورا بروكسي موثوق، الاعتماد عليها معناه إن أمان
/// النظام بقى متعلّق بإعداد البروكسي، والإعداد ده مش في المستودع
/// ومحدّش بيختبره.</para>
///
/// <para>🔴 <b>والمسار نفسه مش إعداد — هو عقد.</b> المسار مكتوب
/// حرفياً جوّه البرنامج المنزّل على الراكات، فاللي بيتظبّط هو
/// <b>الأصل</b> بس والمسار بيتلزّق عليه هنا.</para>
/// </summary>
public static class RackSyncAddress
{
    /// <summary>
    /// 🔴 <b>المسار اللي الراكة بترفع عليه — جزء من العقد
    /// المجمّد.</b>
    /// </summary>
    public const string SyncPath = "/api/sync/reports";

    /// <summary>
    /// عنوان التطوير المحلي.
    ///
    /// <para>⚠️ <b>قيمة صريحة أحسن من الرجوع لترويسة الطلب في
    /// التطوير.</b> لو رجعنا للترويسة، الفرق بين التطوير والإنتاج
    /// يبقى في <b>مصدر الثقة</b> نفسه — والاختبار وقتها مش بيختبر
    /// اللي بينشر.</para>
    /// </summary>
    public const string DevelopmentDefault = "http://localhost:5097";

    /// <summary>
    /// بيتأكد إن القيمة أصل مطلق ينفع يتخزّن على راكة.
    ///
    /// <para>⚠️ <b>والشروط مش شكلية:</b> مسار نسبي أو بروتوكول غريب
    /// بيعدّي من <c>Uri</c> وبيقع بعدين <b>على الراكة</b> وهي
    /// بتحاول ترفع — وساعتها الخطأ بيبان على المحطة مش على السيرفر
    /// اللي غلط.</para>
    /// </summary>
    public static string Validate(string value, string source)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
            throw new InvalidOperationException(
                $"{source} مش عنوان مطلق صالح: «{value}». "
                + "المتوقّع حاجة زي https://codlek.runasp.net");

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException(
                $"{source} لازم يبقى http أو https، مش «{uri.Scheme}».");

        if (!string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            throw new InvalidOperationException(
                $"{source} لازم يبقى أصل بس من غير استعلام أو علامة: «{value}».");

        /*
          ⚠️ بنعيد البناء من أجزاء الـ`Uri` عشان أي تشويه في النص
          الأصلي (مسافات، حروف كبيرة في اسم المضيف، شرطة زايدة)
          مايوصلش للراكة. الناتج شكل واحد مهما اتكتب الإعداد إزاي.
        */
        return uri.GetLeftPart(UriPartial.Authority) + uri.AbsolutePath.TrimEnd('/');
    }

    /// <summary>العنوان الكامل اللي بيترجّع للراكة وقت التسجيل.</summary>
    public static string SyncEndpoint(string baseUrl) => baseUrl + SyncPath;
}
