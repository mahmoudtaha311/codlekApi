using Codlek.Application.Abstractions;

namespace Codlek.Application.Features.Racks;

/// <summary>
/// أسباب فشل إدارة محطات الفحص — <b>منقولة بالحرف</b>.
///
/// <para>⚠️ ورسايل الرفض هنا <b>بتشرح السبب مش بتقول «ماينفعش»</b>.
/// المالك اللي بيدوس مسح على كود ولازم يعرف الفرق بين «ده دليل
/// مراجعة» و«استنى لحد ما ينتهي» — الأولى خلاص والتانية بعد
/// شوية.</para>
/// </summary>
public static class RackErrors
{
    /// <summary>
    /// ⚠️ رسالة واحدة للمحطة المش موجودة وللمحطة اللي في شركة
    /// تانية — عن قصد.
    /// </summary>
    public static readonly Error NotFound =
        new("rack.not_found", "المحطة دي مش موجودة", 404);

    public static readonly Error CodeNotFound =
        new("rack.code_not_found", "الكود ده مش موجود", 404);

    public static readonly Error NameTooShort =
        new("rack.name_too_short",
            "اكتب اسم لمحطة الفحص (مثال: محطة أحمد)", 400);

    /// <summary>
    /// 🔴 <b>الملغية نهائياً مش بترجع.</b>
    ///
    /// <para>والرسالة بتقول <b>البديل</b>: كود تفعيل جديد. من غيره،
    /// المالك بيفضل يدوس «تشغيل» وبيتفاجئ إن المحطة واقفة في
    /// الميدان.</para>
    /// </summary>
    public static readonly Error Revoked =
        new("rack.revoked",
            "المحطة دي ملغية نهائياً ومش بترجع. اعمل كود تفعيل جديد.", 400);

    public static readonly Error RevokeReasonRequired =
        new("rack.revoke_reason_required",
            "اكتب سبب الإلغاء — ده بيفضل في سجل المراجعة", 400);

    /// <summary>
    /// 🔴 <b>الكود المستهلك هو الدليل الوحيد</b> على إن المحطة
    /// الفلانية اتسجّلت بأنهي إذن ومن مين.
    /// </summary>
    public static readonly Error CodeAlreadyUsed =
        new("rack.code_already_used",
            "الكود ده اتفعّلت بيه محطة فعلاً — مابيتمسحش، "
            + "لأنه الدليل الوحيد على إن المحطة اتسجّلت إزاي", 400);

    /// <summary>
    /// ⚠️ <b>والرسالة مابتقولش «استنى كام دقيقة»</b> عن قصد: وقت
    /// الانتهاء بيترجّع في الصف نفسه
    /// (<c>ActivationCodeRow.ExpiresAtUtc</c>) واللوحة بتعرضه عدّ
    /// تنازلي — ورقم مكتوب في نص بيبقى غلط أول ما العمر يتغيّر.
    /// </summary>
    public static readonly Error CodeStillValid =
        new("rack.code_still_valid",
            "الكود ده لسه صالح — استنى لحد ما ينتهي، "
            + "أو ممكن يكون فيه حد مستنيه عشان يفعّل بيه محطة", 400);
}
