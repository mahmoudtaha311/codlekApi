using System.Text.Json;

namespace Codlek.Api.Racks;

/// <summary>
/// تسلسل عقود سلك الراكة — <b>تعريف واحد، مجمّد</b>.
///
/// <para>🔴 <b>وده موجود عشان عيب حقيقي.</b> رد دفعة المزامنة كان
/// بيتكتب بطريقتين: الرد الطازة camelCase، وإعادة الإرسال كنص
/// مخزّن اتسلسل بخيارات مالهاش سياسة تسمية (PascalCase). والراكة
/// بتقرا بـ<c>JsonDocument.TryGetProperty</c> وهي <b>حسّاسة لحالة
/// الحروف</b> — فكل إعادة إرسال كانت بتبان لها رد فاضي، وصف مرفوض
/// كان بيتقفل ويضيع في صمت.</para>
///
/// <para><b>والقاعدة دلوقتي:</b> متسامح جوّه، قانوني برّه. اللي
/// متخزّن ممكن يكون بأي شكل من الاتنين؛ واللي بيخرج على السلك
/// camelCase دايماً، مهما كان مصدره — طازة، إعادة إرسال، أو تعافي
/// من تكرار.</para>
///
/// <para>⚠️ <b>ومثبّتة صراحةً مش معتمدة على الافتراضي.</b> لما
/// <c>/api/v1</c> ياخد سياسة JSON خاصة بيه، العقد المجمّد ده مالوش
/// يتحرّك معاه.</para>
/// </summary>
public static class RackWire
{
    /// <summary>
    /// الشكل الخارج لرد الدفعة — camelCase، <b>من غير</b> محوّل
    /// تواريخ.
    ///
    /// <para>🔴 <b>مطابق بايت ببايت للي القديم بيطلّعه</b> — الراكات
    /// في الميدان بتقرا الرد ده بـ<c>JsonDocument</c>، وأي تغيير في
    /// شكله بيمسّ أخطر مسار في النظام.</para>
    /// </summary>
    public static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// قراية الردود المخزّنة — الصفوف القديمة PascalCase والجديدة
    /// camelCase، <b>والاتنين بيتقروا</b>.
    /// </summary>
    public static readonly JsonSerializerOptions Stored = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// الشكل الخارج للتغذيات النازلة — camelCase <b>ومعاه
    /// <c>Z</c></b>.
    ///
    /// <para>🔴 <b>وليه إعدادات لوحدها مش زيادة على
    /// <see cref="Wire"/>.</b> <c>Wire</c> بيطلّع رد الدفعة، وده
    /// عقد مجمّد بايت ببايت. والتغذيات النازلة بتاخد الشكل الصح من
    /// أول يوم.</para>
    ///
    /// <para>🔴 <b>و<c>Z</c> فرق في التغذية دي تحديداً:</b> الراكة
    /// بتاخد علامة الوقت من الرد وبتبعتها تاني في <c>?since=</c>.
    /// تاريخ من غير منطقة بيتقرا محلي، فالعلامة بتتزحلق بفرق
    /// التوقيت — يعني يا سحب كل حاجة من تاني كل دورة، يا
    /// <b>تخطّي أوامر حقيقية للأبد</b>.</para>
    /// </summary>
    public static readonly JsonSerializerOptions Downstream = BuildDownstream();

    private static JsonSerializerOptions BuildDownstream()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        options.Converters.Add(new UtcDateTimeConverter());

        /*
          ⚠️ **والاختياري تأكيد مش إصلاح.**

          STJ أصلاً بيلفّ محوّل `DateTime` ويستعمله مع `DateTime?`،
          والقديم بيسجّل محوّل واحد وبيخرج `Z` صح. بنسجّله صراحةً
          عشان الضمانة تبقى في كودنا مش في تفاصيل المكتبة.
        */
        options.Converters.Add(new NullableUtcDateTimeConverter());

        return options;
    }
}
