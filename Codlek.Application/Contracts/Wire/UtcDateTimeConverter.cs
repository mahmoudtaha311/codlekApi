using System.Text.Json;
using System.Text.Json.Serialization;

namespace Codlek.Application.Contracts.Wire;

/// <summary>
/// بيكتب كل تاريخ على السلك كـUTC صريح — <b>بحرف <c>Z</c> في
/// الآخر</b>.
///
/// <para>🔴 <b>وده عيب حقيقي حصل في الميدان.</b></para>
///
/// <para>EF بيقرا <c>datetime2</c> من SQL Server و<c>Kind</c> بيرجع
/// <c>Unspecified</c>. وقتها <c>System.Text.Json</c> بيكتب
/// <c>"2026-09-23T16:51:05"</c> <b>من غير أي منطقة</b>. والمتصفح
/// بيقرا النص ده على إنه <b>توقيت محلي</b>، فتحويل «اعرض بتوقيت
/// القاهرة» بعده مابيعملش حاجة.</para>
///
/// <para>اللي المدير شافه: فحص خلص ٧:٥٢ مساءً بتوقيت القاهرة كان
/// مكتوب عليه ٤:٥١ — وقت UTC خام معروض كأنه محلي. والواجهة كان
/// فيها محوّل قاهرة شغّال فعلاً؛ المشكلة إن اللي داخله كان متقري
/// غلط.</para>
///
/// <para>🔴 <b>وفي تغذية الراكة الفرق أخطر من العرض.</b> الراكة
/// بتاخد علامة الوقت من الرد وبتبعتها تاني في <c>?since=</c>.
/// تاريخ من غير منطقة بيتقرا محلي، فالعلامة بتتزحلق بفرق التوقيت —
/// يعني يا الراكة بتسحب كل حاجة من تاني كل دورة (لو اتزحلقت لورا)،
/// يا <b>بتتخطّى أوامر حقيقية للأبد</b> (لو اتزحلقت لقدّام).
/// والتانية دي بتضيّع شغل من غير أي عرض.</para>
///
/// <para>⚠️ <b>والتخزين مابيتغيّرش.</b> كله بيفضل UTC في القاعدة —
/// ده صح ومش هيتغيّر، لأن ساعات الراكات نفسها مش موثوقة. اللي
/// بيتصلّح هنا هو إن السلك يقول الحقيقة عن اللي بيبعته.</para>
///
/// <para>⚠️ وأسماء الحقول نفسها بتقول <c>...AtUtc</c> — كتابتها من
/// غير <c>Z</c> كانت تناقض معلن.</para>
/// </summary>
public sealed class UtcDateTimeConverter : JsonConverter<DateTime>
{
    /// <summary>الشكل على السلك — <b>بدقة المللي ثانية بالظبط</b>.</summary>
    public const string Format = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";

    /// <summary>
    /// 🔴 <b>القراءة زي ما هي — مافيهاش أي تحويل.</b>
    ///
    /// <para>⚠️ الإعدادات دي بتربط حمولات الراكة الصاعدة كمان، يعني
    /// أي تغيير هنا بيمسّ <b>كل حمولة بترفعها راكة</b> — وده أخطر
    /// مسار في النظام كله.</para>
    ///
    /// <para>والعيب اللي بنصلّحه عيب <b>كتابة</b> (السلك مكانش
    /// بيقول إن الوقت UTC)، فالقراءة مالهاش دعوة بيه وبتفضل على
    /// السلوك الافتراضي حرف بحرف.</para>
    /// </summary>
    public override DateTime Read(
        ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.GetDateTime();

    public override void Write(
        Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        var utc = value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),

            // 🔴 ده المسار اللي كان بيكسر: EF بيرجّع `Unspecified`،
            //    واللي متخزّن فعلاً UTC.
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };

        writer.WriteStringValue(utc.ToString(Format));
    }
}
