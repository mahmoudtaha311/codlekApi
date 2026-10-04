using System.Text;
using System.Text.RegularExpressions;

namespace Codlek.Core.Devices;

/// <summary>
/// قراية كود اللاب من ماسح باركود — <b>مطابقة تامة، مفيش تقريب</b>.
///
/// <para><b>ودي مش البحث.</b> البحث الحر في قايمة الأجهزة بيمشي على
/// <c>LIKE</c> فوق نص موحّد توحيد عربي فاقد، وده صح للبحث وغلط تماماً
/// للمسح: الماسح بيدّي قيمة واحدة محددة، والمطلوب يا اللاب ده يا «مش
/// موجود». قيمة ممسوحة عمرها ما بتعدّي على مسار <c>LIKE</c>.</para>
///
/// <para>⚠️ ومكانها <c>Core</c> لأنها <b>نقية بالكامل</b> — مفيش
/// قاعدة بيانات. اللي بيلمس القاعدة هو
/// <c>IDeviceRepository.ResolveCodeAsync</c>.</para>
/// </summary>
public static class DeviceScan
{
    /// <summary>
    /// الشكل المقبول.
    ///
    /// <para>🔴 <b>شكلين مش واحد.</b> المخزن بيطبع الباركود بتاعه على
    /// اللاب قبل الفحص، والفني بيكتبه — فالكود بقى بتاعهم مش بتاعنا،
    /// ومالوش شكل إحنا بنفرضه.</para>
    ///
    /// <para>⚠️ و<c>LP-xxxxxxxx</c> بيفضل مقبول <b>للأبد</b>: فيه آلاف
    /// الاستيكرات ملزوقة على أجهزة شغّالة من قبل التغيير. رفضها كان
    /// هيخلّي نص المخزن «مش موجود».</para>
    ///
    /// <para>🔴 <c>[0-9]</c> مقصودة مش <c>\d</c>. الأخيرة بتطابق
    /// الأرقام العربية-الهندية كمان، فقيمة زي <c>LP-٠٠٠٠٠٠٠١</c> كانت
    /// هتعدّي التحقق وبعدين تفشل في القاعدة — «مش موجود» لجهاز موجود.
    /// الأرقام دي بتتحوّل قبل كده في <see cref="FoldDigits"/>، وأي
    /// حاجة فضلت غير لاتينية بتترفض هنا صراحةً.</para>
    ///
    /// <para>⚠️ <b>والشكل العام ضيّق عن قصد.</b> حروف وأرقام لاتينية
    /// وشرطة وشرطة سفلية وبس، من ٣ لـ٢٠ محرف. ده مش تحقّق من صحة
    /// الكود — السيرفر مش موقعه يحكم على ترقيم المخزن — ده حاجز ضد إن
    /// أي نص عشوائي أو جزء من رابط يتحوّل لاستعلام قاعدة.</para>
    /// </summary>
    private static readonly Regex Shape = new(
        "^(LP-[0-9]{8}|[A-Z0-9][A-Z0-9_-]{1,18}[A-Z0-9])$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>سقف طول الكود بعد ما يتفصل — حاجز أمان قبل أي تحليل روابط.</summary>
    private const int MaxInput = 200;

    /// <summary>
    /// سقف مطلق على المدخل الخام قبل أي شغل.
    ///
    /// <para>الليبل الجديد بيحط المواصفات ورا الكود في نفس الـQR،
    /// فالمدخل بقى أطول من الكود بكتير. السقف ده موجود عشان قيمة ضخمة
    /// مالهاش أي علاقة تترفض من غير ما نلف عليها.</para>
    /// </summary>
    private const int MaxRawInput = 400;

    /// <summary>
    /// الفاصل في حمولة الليبل الجديدة — لازم يطابق
    /// <c>spics.Reporting.DeviceLabel.Separator</c> في تطبيق الراكة.
    /// </summary>
    private const char PayloadSeparator = '|';

    private const string LegacyScheme = "codlek:";
    private const string LegacyDevice = "codlek:device/";

    /// <summary>مسار التوافق القديم — <c>/d/&lt;code&gt;</c>.</summary>
    public const string LegacyPathSegment = "d";

    /// <summary>
    /// بيرجّع الكود، أو <c>null</c> لو المدخل مش كود جهاز.
    ///
    /// <para>الترتيب: تنضيف ← تحويل أرقام ← أول حقل ← فكّ الأشكال
    /// القديمة ← تكبير حروف ← التحقق من الشكل.</para>
    ///
    /// <para>🔴 <b>وتكبير الحروف هنا حمّال لحاجة في مكان تاني.</b>
    /// البحث التاريخي بيقارن بـ<c>ArabicText.Normalize</c>، وهي
    /// <b>مابتكبّرش الحروف</b>. فالكود اللي بيوصل القاعدة لازم يكون
    /// كبير خلاص من هنا — ولو الـ<c>ToUpperInvariant</c> دي اتشالت،
    /// حد بيكتب <c>lp-00018425</c> بحروف صغيرة مش هيلاقي المرساة
    /// التاريخية، وهو شايف اللاب قدامه.</para>
    /// </summary>
    public static string? Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        /*
          الماسح بيبعت الصقة (Enter/Tab) كجزء من القيمة أحياناً،
          والنسخ واللصق بيجيب مساحات. `Trim` في .NET بتشيل كل فراغات
          يونيكود ومعاها \r و\n و\t.
        */
        string s = raw.Trim();

        if (s.Length == 0 || s.Length > MaxRawInput) return null;

        s = FoldDigits(s);
        s = FirstField(s);

        if (s.Length == 0 || s.Length > MaxInput) return null;

        string? code = Unwrap(s);

        if (code == null) return null;

        code = code.Trim().ToUpperInvariant();

        return Shape.IsMatch(code) ? code : null;
    }

    /// <summary>
    /// بياخد الكود من حمولة الليبل الجديدة.
    ///
    /// <para>🔴 <b>ده اللي بيخلّي الليبل القديم والجديد يفتحوا نفس
    /// اللاب.</b> الليبل بقى بيحط المواصفات ورا الكود عشان أي موبايل
    /// يعرض وصف اللاب من غير نت:</para>
    ///
    /// <para><c>LP-00000001|LENOVO|Legion 5 15ARH05|Ryzen 5 4600H|…</c></para>
    ///
    /// <para>والكود أول حقل دايماً، فاللي بيقرا بياخد اللي قبل أول
    /// فاصل ويرمي الباقي. استيكر من سنة فاتت (كود عاري، مفيش فاصل)
    /// بيعدّي من هنا زي ما هو.</para>
    ///
    /// <para>🔴 <b>والمواصفات مابتتقريش خالص.</b> اللي في الـQR وصف
    /// مطبوع يوم الفحص؛ الحقيقة في القاعدة. لو قرينا منه كنا هنعرض
    /// مواصفات قديمة على إنها الحالية بعد أي تغيير قطعة.</para>
    /// </summary>
    private static string FirstField(string s)
    {
        int at = s.IndexOf(PayloadSeparator);

        return at < 0 ? s : s[..at].Trim();
    }

    /// <summary>
    /// بيفك الأشكال القديمة اللي لسه ملزوقة على أجهزة في الورشة.
    ///
    /// <para>الليبل الجديد بيشيل الكود عارياً وبس. بس الراكة كانت
    /// بتطبع تلات أشكال قبل كده، ومنهم ليبلات لسه على أجهزة
    /// شغّالة:</para>
    ///
    /// <list type="bullet">
    ///   <item><c>codlek:device/LP-00000001</c> — لما الراكة مسجّلتش</item>
    ///   <item><c>https://host/d/LP-00000001</c> — لما الراكة مسجّلة</item>
    ///   <item><c>codlek:report/&lt;guid&gt;</c> — لما اللاب ماكانش واخد كود</item>
    /// </list>
    ///
    /// <para>🔴 الاتنين الأولانيين بيتفكّوا. <b>والتالت بيترفض</b>:
    /// معرّف فحص داخلي مش هوية لاب، واستعماله كده كان بيخلّي ورقة
    /// مطبوعة تقول إن اللاب هو الفحص.</para>
    ///
    /// <para>⚠️ والرابط بيتقرا بمحلّل روابط حقيقي، مش بتفتيش عن نص.
    /// «كلام فاضي فيه /d/LP-00000001» مش رابط ومش كود وبيترفض. وكمان
    /// المسار لازم يكون جزئين بالظبط — <c>/x/d/CODE</c> بيترفض.</para>
    /// </summary>
    private static string? Unwrap(string s)
    {
        if (s.StartsWith(LegacyScheme, StringComparison.OrdinalIgnoreCase))
        {
            return s.StartsWith(LegacyDevice, StringComparison.OrdinalIgnoreCase)
                ? s[LegacyDevice.Length..]

                // codlek:report/... وأي مخطط codlek: تاني
                : null;
        }

        if (Uri.TryCreate(s, UriKind.Absolute, out Uri? uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            string[] parts = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

            return parts.Length == 2
                   && parts[0].Equals(LegacyPathSegment, StringComparison.OrdinalIgnoreCase)
                ? parts[1]
                : null;
        }

        // مش ملفوف — الشكل هو اللي هيحكم.
        return s;
    }

    /// <summary>
    /// الأرقام العربية-الهندية والفارسية ← لاتينية.
    ///
    /// <para>تحويل محدّد واحد-لواحد، مش مطابقة تقريبية: <c>٠..٩</c>
    /// (U+0660) و<c>۰..۹</c> (U+06F0) ليهم مقابل لاتيني واحد مفيش
    /// غيره. ده بيخلّي اللي بيكتب بالإيد على لوحة عربية يلاقي اللاب
    /// بدل «مش موجود» لجهاز قدامه.</para>
    ///
    /// <para>🔴 <b>وليه مش <c>ArabicText.Normalize</c>.</b> المشروع
    /// فيه مطبّعين مختلفين <b>عن قصد</b>، و<c>ArabicText</c> هو
    /// <b>الفاقد</b>: بيوحّد الهمزات والتاء المربوطة ويشيل محارف
    /// تحكّم. ده صح لنص بحث حر وغلط لمعرّف — المعرّف المفروض يتحوّل
    /// بحاجات <b>عكسية</b> وبس. فبناخد منه <b>خريطة الأرقام</b> (نفس
    /// المدايين بالظبط) ونسيب الباقي.</para>
    /// </summary>
    public static string FoldDigits(string s)
    {
        StringBuilder? sb = null;

        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];

            char folded =
                c is >= '٠' and <= '٩' ? (char)('0' + (c - '٠')) :
                c is >= '۰' and <= '۹' ? (char)('0' + (c - '۰')) :
                c;

            if (folded == c) { sb?.Append(c); continue; }

            sb ??= new StringBuilder(s.Length).Append(s, 0, i);
            sb.Append(folded);
        }

        return sb?.ToString() ?? s;
    }
}
