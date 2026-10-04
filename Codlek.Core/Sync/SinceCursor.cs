using System.Globalization;

namespace Codlek.Core.Sync;

/// <summary>
/// علامة السحب التراكمي — <b>الراكة بتبعت آخر وقت شافته</b>.
///
/// <para>🔴 <b>والدالة دي نقية لأن كل سطر فيها عطل حقيقي ممكن.</b>
/// الراكة بتخزّن العلامة <b>كنص خام</b> وبتبعتها تاني زي ما هي من
/// غير ما تفكّها — فأي زحلقة هنا بتبقى دايمة ومش باينة في أي
/// لوج.</para>
/// </summary>
public static class SinceCursor
{
    /// <summary>
    /// حجم الصفحة.
    ///
    /// <para>⚠️ الراكة بتسحب لحد ما <c>hasMore</c> تبقى
    /// <c>false</c>، فالرقم ده سقف الطلب الواحد مش سقف الشغل.</para>
    /// </summary>
    public const int PageSize = 200;

    /// <summary>
    /// بيقرا <c>?since=</c> — <b>ولا بيرمي ولا بيرفض</b>.
    ///
    /// <para>🔴 <b><c>RoundtripKind</c> لوحده — ممنوع يتخلط مع
    /// <c>AdjustToUniversal</c>.</b> الاتنين مع بعض بيرموا
    /// <c>ArgumentException</c> <b>قبل</b> أي تحليل، و<c>TryParse</c>
    /// مابتلمّهاش — يعني النقطة بتاخد <c>500</c>. والراكة بتعتبر أي
    /// رد غير 2xx «أجّل»، جوّه <c>catch { }</c>، فمابتقدّمش علامتها
    /// ومابتكتبش لوج ومابتسحبش تاني — <b>للأبد</b>. والعطل بيبان
    /// كأنه انقطاع في الأسطول كله مع تنصيب جديد نضيف.</para>
    ///
    /// <para>🔴 <b>وتاريخ مش مفهوم بيرجع <c>null</c> مش خطأ.</b>
    /// يعني الراكة بتاخد <c>200</c> بأول صفحة — نفس اللي بتاخده لو
    /// مابعتتش علامة خالص. ورفضه بـ<c>400</c> كان بيوقّف السحب على
    /// راكة علامتها اتخربت، وهي الحالة اللي محتاجة تسحب من
    /// الأول.</para>
    /// </summary>
    public static DateTime? Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        if (!DateTime.TryParse(
                raw, CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out var parsed))
            return null;

        /*
          🔴 **والذراع المهم هو `Local` مش `Unspecified`.**

          وقت بـ`+02:00` بيتقرا `Local` بتوقيت السيرفر، ومقارنته
          بعمود UTC من غير تحويل بتزحلق النافذة بفرق التوقيت: زحلقة
          لورا = الراكة بتسحب كل حاجة من تاني كل دورة؛ زحلقة لقدام =
          أوامر حقيقية بتقع في الفجوة و**بتتفوّت للأبد**.

          أما `Unspecified` فـ`SpecifyKind` بتغيّر اللافتة مش
          القيمة — ومع ده بنكتبها صريح: ده **واجهة آلة**، فالوقت من
          غير منطقة UTC، مش توقيت السيرفر.
        */
        return parsed.Kind switch
        {
            DateTimeKind.Utc => parsed,
            DateTimeKind.Local => parsed.ToUniversalTime(),
            _ => DateTime.SpecifyKind(parsed, DateTimeKind.Utc),
        };
    }

    /// <summary>كام صف نطلب عشان نعرف لو فيه كمان.</summary>
    public static int Need => PageSize + 1;

    /// <summary>فيه صفحة تانية؟</summary>
    public static bool HasMore(int fetched) => fetched > PageSize;
}
