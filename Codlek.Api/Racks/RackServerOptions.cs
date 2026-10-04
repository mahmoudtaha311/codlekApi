using Codlek.Core.Racks;

namespace Codlek.Api.Racks;

/// <summary>
/// إعدادات السيرفر اللي سطح الراكة بيقراها.
///
/// <para>🔴 <b>كلها من الإعدادات، ولا واحدة من الطلب.</b> العنوان
/// اللي بيتبعت للراكة وقت التسجيل بتفضل عليه <b>شهور</b> — فبناؤه
/// من ترويسة <c>Host</c> معناه إن اللي بيسجّل بيحدّد فين الراكة
/// هترفع شغلها.</para>
/// </summary>
public sealed class RackServerOptions
{
    public const string Section = "Server";

    /// <summary>
    /// <b>الأصل</b> اللي عنوان المزامنة بيتبنى عليه — زي
    /// <c>https://codlek.runasp.net</c>.
    ///
    /// <para>🔴 <b>الأصل بس — من غير مسار.</b> المسار
    /// (<c>/api/sync/reports</c>) مكتوب حرفياً جوّه البرنامج المنزّل
    /// على الراكات، فهو <b>عقد مش إعداد</b> وبيتلزّق في
    /// <see cref="RackSyncAddress.SyncEndpoint"/>.</para>
    ///
    /// <para>⚠️ <b>وفاضي بيوقّف الإقلاع بره التطوير.</b> شوف
    /// <see cref="CloudAddresses.Resolve"/> — الوقوف مقصود.</para>
    /// </summary>
    public string PublicBaseUrl { get; set; } = "";

    /// <summary>
    /// 🔴 <b>«أنا اتنقلت» — ويتحط على السيرفر <u>القديم</u>
    /// بس.</b>
    ///
    /// <para>لو الاتنين بيعلنوا، الراكات بتفضل تلف بينهم
    /// للأبد.</para>
    /// </summary>
    public string MovedTo { get; set; } = "";

    /// <summary>
    /// كام يوم تنفع الراكة تدخّل الفني من نسخته المحفوظة وهي
    /// أوفلاين.
    ///
    /// <para>🔴 <b>والرقم ده بينزل مع رد الدخول كتاريخ نهاية.</b>
    /// الراكة بتخزّنه وبتعتمد عليه؛ لو جه <c>null</c> أو في الماضي،
    /// الفني مابيقدرش يدخل أوفلاين خالص — حتى لو عنده نسخة
    /// صالحة.</para>
    ///
    /// <para>⚠️ وسبعة زي القديم. أطول من كده بيخلّي فني اتسحبت
    /// صلاحيته يفضل شغّال أسبوعين؛ أقصر بيقفل ورشة نتها بيقطع
    /// أسبوع.</para>
    /// </summary>
    public int OfflineTechnicianValidityDays { get; set; } = 7;
}

/// <summary>
/// العناوين العامة محسوبة <b>مرة واحدة عند الإقلاع</b>.
///
/// <para>⚠️ بتتحقن في النقط بدل ما كل واحدة تقرا الإعدادات وتتحقق
/// لوحدها — التحقق المتكرر هو اللي بيخلّي مكان واحد ينسى.</para>
/// </summary>
public sealed class CloudAddresses
{
    public CloudAddresses(string baseUrl)
    {
        BaseUrl = baseUrl;
        SyncUrl = RackSyncAddress.SyncEndpoint(baseUrl);
    }

    /// <summary>الأصل الموثوق، من غير شرطة في الآخر.</summary>
    public string BaseUrl { get; }

    /// <summary>العنوان اللي الراكة بتخزّنه وبترفع عليه.</summary>
    public string SyncUrl { get; }

    /// <summary>
    /// بيرجّع العنوان من الإعدادات، أو بيرمي برسالة واضحة.
    ///
    /// <para>🔴 <b>وليه بنوقف عند الإقلاع لو ناقص في الإنتاج.</b>
    /// خدمة شغّالة بعنوان فاضي بتوزّع <c>syncUrl</c> فاضي على كل
    /// راكة بتتسجّل، والراكة بتخزّنه وبتفضل تحاول ترفع عليه —
    /// ومحدّش بياخد باله غير بعد ما شغل أسبوع يبقى واقف في الطابور.
    /// الوقوف بيتصلّح في دقيقة؛ العنوان الفاضي بيتصلّح <b>بإعادة
    /// تسجيل كل المحطات</b>.</para>
    ///
    /// <para>⚠️ ولقينا ده بضرب حقيقي على HTTP: التسجيل كان بيرجّع
    /// <c>201</c> و<c>syncUrl</c> فاضي، وكل الفحوص خضرا.</para>
    /// </summary>
    public static CloudAddresses Resolve(IConfiguration configuration, bool isDevelopment)
    {
        string key = RackServerOptions.Section + ":"
            + nameof(RackServerOptions.PublicBaseUrl);

        string? configured = configuration[key];

        if (string.IsNullOrWhiteSpace(configured))
        {
            if (isDevelopment) return new CloudAddresses(RackSyncAddress.DevelopmentDefault);

            throw new InvalidOperationException(
                "مفيش عنوان عام للسيرفر.\n"
                + $"حط {key} (أو متغيّر البيئة "
                + $"{RackServerOptions.Section}__{nameof(RackServerOptions.PublicBaseUrl)}) "
                + "— مثلاً https://codlek.runasp.net\n\n"
                + "الوقوف هنا مقصود: من غيره رد تسجيل الراكة بيرجّع عنوان مزامنة "
                + "فاضي، والراكة بتخزّنه وبتفضل ترفع عليه — والتصليح بعد كده "
                + "بيحتاج إعادة تسجيل كل المحطات.");
        }

        return new CloudAddresses(RackSyncAddress.Validate(configured.Trim(), key));
    }
}
