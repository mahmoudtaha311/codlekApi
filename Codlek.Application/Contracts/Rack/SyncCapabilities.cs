namespace Codlek.Application.Contracts.Rack;

/// <summary>
/// قدرات السيرفر — <b>الرد اللي الراكة بتخزّنه على قرصها</b>.
///
/// <para>🔴 <b>والرد ده بيتكاش، والفشل <u>مابيمسحهوش</u>.</b> يعني
/// وقت التحويل، نسخة قديمة متخزّنة من السيرفر القديم بتسيب أزرار
/// الصيانة أوفلاين <b>شغّالة</b> على الراكة والنقط الجديدة بترجّع
/// <c>404</c> — والفني بيدوس وبيلاقي عطل مالوش تفسير.</para>
///
/// <para>⚠️ <b>والراكة الملغية بتاخد رد كامل عن قصد:</b> هي محتاجة
/// تعرف إن السيرفر بيفهمها عشان تعرض رسالة صح، والقدرات مش
/// سر.</para>
/// </summary>
public sealed class SyncCapabilities
{
    public int ProtocolVersion { get; init; }

    /// <summary>
    /// الأنواع اللي السيرفر ده بيقبلها فعلاً.
    ///
    /// <para>⚠️ <b>مرتّبة ترتيب بايت.</b> الراكة بتقارن القايمة
    /// بالمتخزّن عندها، وترتيب مختلف بيتقري «القدرات اتغيّرت».</para>
    /// </summary>
    public IReadOnlyList<string> EntityTypes { get; init; } = [];

    /// <summary>نسخة شكل الحمولة لكل نوع.</summary>
    public IReadOnlyDictionary<string, int> PayloadVersions { get; init; } =
        new Dictionary<string, int>();

    /// <summary>التغذيات النازلة — <b>بترتيبها</b>.</summary>
    public IReadOnlyList<string> Downstream { get; init; } = [];

    /// <summary>
    /// ساعة السيرفر.
    ///
    /// <para>⚠️ الراكة بتقارنها بساعتها عشان تعرف لو ساعتها مزحلقة
    /// — وده بيحصل فعلاً: كان فيه راكة ساعتها متقدّمة ٥٧ دقيقة.</para>
    /// </summary>
    public DateTime ServerTimeUtc { get; init; }
}
