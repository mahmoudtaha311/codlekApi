namespace Codlek.Core.Text;

/// <summary>
/// بناء أنماط LIKE من نص كتبه المستخدم.
///
/// <para><b>ليه الملف ده موجود.</b> البحث كان بيحط اللي المستخدم كتبه
/// جوّه <c>%...%</c> على طول ويبعته لـ <c>EF.Functions.Like</c>. يعني
/// المحارف الخاصة بتاعة LIKE كانت بتتنفّذ بدل ما تتقرا كنص:</para>
///
/// <list type="bullet">
/// <item>واحد بيدوّر على <c>%</c> كان بيرجّع <b>كل</b> الصفوف.</item>
/// <item>واحد بيدوّر على موديل فيه <c>_</c> كان بيلاقي نتايج غلط، لأن
/// الشرطة السفلية معناها «أي حرف».</item>
/// <item><c>[</c> بيفتح مجموعة محارف، فبحث فيه قوس كان ممكن يرجّع
/// حاجات مالهاش علاقة أو ميرجّعش حاجة خالص.</item>
/// </list>
///
/// <para>⚠️ ودي مش تفصيلة شكلية على <c>SearchText</c>: العمود ده
/// <c>nvarchar(max)</c> ومفيش عليه فهرس، فـ<c>%</c> لوحدها كانت
/// بتحوّل الصفحة لمسح كامل للجدول.</para>
///
/// <para><b>مش بيلمس التطبيع.</b> الهروب بيتعمل <b>بعد</b> التطبيع
/// العربي وبيشتغل على النتيجة بس — قواعد الهوية والتطبيع زي ما هي
/// بالظبط.</para>
/// </summary>
public static class SearchPattern
{
    /// <summary>المحرف اللي بيهرب اللي بعده. لازم يتبعت لـ LIKE في جملة ESCAPE.</summary>
    public const string Escape = "\\";

    /// <summary>
    /// بيهرب المحارف اللي LIKE بيعتبرها خاصة، فتتقرا كنص عادي.
    ///
    /// <para>⚠️ الترتيب مقصود: الشرطة المايلة الأول. لو اتعملت في الآخر
    /// كانت هتهرب الشرط المايلة اللي إحنا ضفناها بنفسنا مرة تانية.</para>
    ///
    /// <para><c>]</c> و<c>^</c> و<c>-</c> ليهم معنى <b>جوّه</b> المجموعة
    /// بس، ولما <c>[</c> تتهرب المجموعة عمرها ما بتتفتح — فمحتاجينش
    /// نهربهم.</para>
    /// </summary>
    public static string EscapeLike(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";

        return value
            .Replace(Escape, Escape + Escape)
            .Replace("%", Escape + "%")
            .Replace("_", Escape + "_")
            .Replace("[", Escape + "[");
    }

    /// <summary>نمط «بيحتوي على» جاهز لـ LIKE، والنص جوّاه حرفي.</summary>
    public static string Contains(string? value) => "%" + EscapeLike(value) + "%";
}
