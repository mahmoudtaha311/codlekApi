using System;
using System.Collections.Generic;
using System.Text;

namespace Codlek.Core.Text;

/// <summary>ليه الماركة دي اتربطت باللاب ده.</summary>
public enum BrandMatch
{
    /// <summary>مطابقة كاملة على الاسم أو على اسم بديل.</summary>
    Exact,

    /// <summary>
    /// أول كلمة طابقت — <c>Dell Inc.</c> ← <c>Dell</c>.
    ///
    /// <para>⚠️ بترجع كسبب منفصل عن المطابقة الكاملة عن قصد:
    /// لو طلعت غلط، اللي بيبص لازم يعرف إنها اتحلّت بأول كلمة
    /// مش بمطابقة.</para>
    /// </summary>
    FirstWord,

    /// <summary>ماركة مش في القايمة.</summary>
    Unknown
}

/// <summary>ماركة واحدة وأسماؤها البديلة — <b>بيانات، مش جدول</b>.</summary>
public readonly record struct BrandRule(Guid Id, string Name, IReadOnlyList<string> Aliases);

/// <summary>نتيجة الحل.</summary>
public readonly record struct BrandResult(Guid? BrandId, string Name, BrandMatch Match)
{
    public static BrandResult None(string raw) => new(null, raw, BrandMatch.Unknown);
}

/// <summary>
/// توحيد اسم الماركة وحلّه لماركة معروفة — <b>قاعدة نقية</b>.
///
/// <para>🔴 <b>ليه دي موجودة أصلاً.</b> اسم الماركة متخزّن
/// <b>خام زي ما ويندوز بيقوله</b>، ومكتوب على الحقل نفسه في
/// السيرفر: «للعرض والبحث بس — محدش يطابق عليه». القيم الحقيقية
/// بتيجي بأشكال مختلفة لنفس الشركة: <c>HP</c> و
/// <c>Hewlett-Packard</c>، <c>LENOVO</c> و<c>Lenovo</c>،
/// <c>Dell</c> و<c>Dell Inc.</c>.</para>
///
/// <para>يعني أي قاعدة بتقول «الفني ده لماركة HP» على الاسم الخام
/// <b>هيعدّي عليها لاب مكتوب Hewlett-Packard</b> من غير ما حد
/// ياخد باله — وده أسوأ من مفيش قاعدة.</para>
///
/// <para>⚠️ <b>الملف ده متكرّر في المشروعين ولازم يفضلوا
/// متطابقين</b> — نفس قاعدة <see cref="ArabicText"/>. لو حد عدّل
/// نسخة واحدة، الراكة هتقرّر حاجة والسيرفر حاجة تانية على نفس
/// اللاب.</para>
/// </summary>
public static class BrandToken
{
    /// <summary>
    /// الاسم الخام ← شكل واحد للمقارنة.
    ///
    /// <para>⚠️ <b>مفيش قايمة لواحق شركات هنا عن قصد</b> (شيل
    /// <c>INC</c> و<c>CO</c> و<c>LTD</c>…). كده
    /// <c>HEWLETT PACKARD ENTERPRISE</c> — وهي سيرفرات مش لابات —
    /// كانت هتتجمّع في صمت مع لابات HP. صاحب الشغل بيدير الأسماء
    /// البديلة بإيده، <b>وده هو التصميم</b> مش نقص فيه.</para>
    /// </summary>
    public static string Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";

        // ⚠️ بتعدّي على التطبيع المشترك الأول: بيكبّر اللاتيني
        // وبيوحّد المسافات، وبيتفحص ذاتياً عند كل تشغيل.
        string text = ArabicText.Normalize(raw);

        var output = new StringBuilder(text.Length);

        foreach (char c in text)
        {
            // الفواصل بين الكلمات بتبقى مسافة: Hewlett-Packard
            // لازم يطابق Hewlett Packard.
            if (c is '-' or '_' or '/' or '\\')
            {
                output.Append(' ');
                continue;
            }

            // وعلامات الترقيم بتتشال: "Dell Inc." ← "DELL INC"
            if (c is '.' or ',' or ';' or ':' or '\'' or '"') continue;

            output.Append(c);
        }

        return Squash(output.ToString());
    }

    /// <summary>
    /// اللاب ده ماركته إيه؟
    ///
    /// <para>⚠️ <b>بتاخد القواعد كبيانات</b> عشان تتفحص من غير
    /// قاعدة بيانات، والراكة تشغّل <b>نفس</b> الدالة على نسختها
    /// المحفوظة.</para>
    ///
    /// <para>🔴 <b>ومفيش مطابقة جزئية خالص.</b> ماركة من حرفين
    /// (<c>LG</c>) جوّه اسم تاني (<c>LGE</c>) كانت هتخلّي الربط
    /// غلط — وأول كلمة بس هي اللي بتتقارن.</para>
    /// </summary>
    public static BrandResult Resolve(IReadOnlyList<BrandRule> rules, string? rawManufacturer)
    {
        string raw = (rawManufacturer ?? "").Trim();
        string key = Normalize(raw);

        if (key.Length == 0 || rules.Count == 0) return BrandResult.None(raw);

        // ١ — مطابقة كاملة على الاسم.
        foreach (var rule in rules)
            if (Normalize(rule.Name) == key)
                return new BrandResult(rule.Id, rule.Name, BrandMatch.Exact);

        // ٢ — مطابقة كاملة على اسم بديل.
        foreach (var rule in rules)
            foreach (string alias in rule.Aliases)
                if (Normalize(alias) == key)
                    return new BrandResult(rule.Id, rule.Name, BrandMatch.Exact);

        // ٣ — أول كلمة. دي اللي بتحل "Dell Inc." من غير اسم بديل،
        //     وهي نفس القاعدة اللي المشروع اختارها مرتين قبل كده
        //     في عرض اسم الجهاز.
        string head = FirstWord(key);
        if (head.Length == 0) return BrandResult.None(raw);

        foreach (var rule in rules)
        {
            if (FirstWord(Normalize(rule.Name)) == head)
                return new BrandResult(rule.Id, rule.Name, BrandMatch.FirstWord);

            foreach (string alias in rule.Aliases)
                if (FirstWord(Normalize(alias)) == head)
                    return new BrandResult(rule.Id, rule.Name, BrandMatch.FirstWord);
        }

        return BrandResult.None(raw);
    }

    /// <summary>
    /// الفني ده مسموحله اللاب ده؟
    /// </summary>
    /// <param name="allowed">
    /// ماركات الفني. <b>فاضية = مفيش قيد</b> — شوف التعليق تحت.
    /// </param>
    /// <param name="resolved">نتيجة حل ماركة اللاب.</param>
    public static bool Allows(IReadOnlyCollection<Guid> allowed, BrandResult resolved)
    {
        // 🔴 **الفاضي معناه «كل الماركات» مش «ولا ماركة».**
        //
        // كل فني في الورشة ماركاته فاضية لحظة ما الميزة تنزل.
        // القراية التانية — وهي الأقرب للذهن — كانت هتقفل على
        // **كل الفنيين** في نفس اللحظة.
        if (allowed.Count == 0) return true;

        // 🔴 **والماركة المش معروفة بتعدّي.**
        //
        // القايمة هتفضل ناقصة دايماً: لاب بماركة جديدة بيوصل
        // الورشة وصاحب الشغل مش موجود يضيفها. المنع ساعتها بيوقّف
        // صيانة حقيقية بسبب سطر ناقص في جدول.
        //
        // ⚠️ والأمان مش في المنع — الأمان في إن الحالة دي
        // **بتتعلّم وبتتعرض**، وفيه شاشة بتوري الماركات اللي لسه
        // مش في القايمة عشان تتضاف.
        if (resolved.BrandId is not { } id) return true;

        return allowed.Contains(id);
    }

    /// <summary>
    /// حالات التوحيد — <b>الجدول ده هو العقد بين النسختين</b>.
    ///
    /// <para>🔴 الملف متكرّر في المشروعين. لو حد عدّل نسخة واحدة،
    /// الراكة هتقرّر إن اللاب ده HP والسيرفر يقول لأ — على نفس
    /// اللاب. الفحص الذاتي بيوقف التشغيل بدل ما الاختلاف يعيش.</para>
    /// </summary>
    private static readonly string[][] Vectors =
    {
        new[] { "HP", "HP" },
        new[] { "hp", "HP" },
        new[] { "  HP  ", "HP" },
        new[] { "Hewlett-Packard", "HEWLETT PACKARD" },
        new[] { "Hewlett Packard", "HEWLETT PACKARD" },
        new[] { "Dell", "DELL" },
        new[] { "Dell Inc.", "DELL INC" },
        new[] { "LENOVO", "LENOVO" },
        new[] { "Lenovo", "LENOVO" },
        new[] { "LG", "LG" },
        new[] { "Micro-Star/MSI", "MICRO STAR MSI" },
        new[] { "", "" },
        new[] { "   ", "" }
    };

    /// <summary>
    /// بيتأكد إن كل حالات التوحيد بتعدّي.
    ///
    /// <para>بيرجّع قايمة الاختلافات — فاضية معناها النسختين
    /// متطابقين.</para>
    /// </summary>
    public static System.Collections.Generic.List<string> SelfTest()
    {
        var failures = new System.Collections.Generic.List<string>();

        foreach (string[] pair in Vectors)
        {
            string actual = Normalize(pair[0]);
            if (actual == pair[1]) continue;

            failures.Add($"«{pair[0]}» ← متوقّع «{pair[1]}» وطلع «{actual}»");
        }

        return failures;
    }

    private static string FirstWord(string normalized)
    {
        int space = normalized.IndexOf(' ');
        return space < 0 ? normalized : normalized[..space];
    }

    private static string Squash(string value)
    {
        var output = new StringBuilder(value.Length);
        bool pending = false;

        foreach (char c in value)
        {
            if (c == ' ')
            {
                pending = output.Length > 0;
                continue;
            }

            if (pending)
            {
                output.Append(' ');
                pending = false;
            }

            output.Append(c);
        }

        return output.ToString();
    }
}
