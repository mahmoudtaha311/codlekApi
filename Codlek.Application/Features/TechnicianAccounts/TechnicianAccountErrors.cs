using Codlek.Application.Abstractions;
using Codlek.Core.Technicians;
using Codlek.Core.Text;

namespace Codlek.Application.Features.TechnicianAccounts;

/// <summary>
/// أسباب فشل إدارة حسابات الفنيين — <b>منقولة بالحرف</b>.
///
/// <para>⚠️ وكلها <c>400</c> ما عدا «مش موجود». والقديم كان بيرجّع
/// <c>400</c> على الفني المش موجود كمان (رسالة في الجسم) — واللي
/// اتنقل هنا <c>404</c> للمسارات اللي فيها معرّف في العنوان، عشان
/// الرد يطابق باقي المشروع. والرسالة نفسها زي ما هي.</para>
/// </summary>
public static class TechnicianAccountErrors
{
    public static readonly Error NotFound =
        new("technician.not_found", "الفني ده مش موجود", 404);

    public static readonly Error NameTooShort =
        new("technician.name_too_short", "اكتب اسم الفني بالكامل", 400);

    public static readonly Error UsernameTooShort =
        new("technician.username_too_short",
            $"اسم الدخول لازم يكون {ArabicDigits.Of(TechnicianAccountRules.MinUsername)} حروف على الأقل",
            400);

    public static readonly Error PasswordTooShort =
        new("technician.password_too_short",
            $"كلمة المرور لازم تكون {ArabicDigits.Of(TechnicianAccountRules.MinPassword)} حروف على الأقل",
            400);

    /// <summary>
    /// ⚠️ <b>«في الشركة دي» جزء من الرسالة عن قصد</b> — الاسم فريد
    /// جوّه الشركة مش عالمياً، والمدير لازم يفهم إن الاسم ممكن يكون
    /// مستخدم في ورشة تانية.
    /// </summary>
    public static readonly Error UsernameTaken =
        new("technician.username_taken",
            "اسم الدخول ده مستخدم قبل كده في الشركة دي", 400);

    public static readonly Error SuspendReasonRequired =
        new("technician.suspend_reason_required",
            "اكتب سبب الإيقاف — الفني بيشوفه على المحطة", 400);

    public static readonly Error UnknownSpecialty =
        new("technician.unknown_specialty", "التخصص المبعوت مش معروف", 400);

    public static readonly Error UnknownBrand =
        new("technician.unknown_brand", "فيه ماركة مش موجودة في القايمة", 400);

    /// <summary>
    /// 🔴 <b>الرمي ده مقصود وبيوصل للمدير.</b> ٤٠ محاولة فاشلة
    /// معناها إن مجال الأكواد (٩٠٠ ألف) خلص تقريباً — ودي حاجة لازم
    /// يعرفها، مش كود مكرر يتحشر في القاعدة.
    /// </summary>
    public static readonly Error CodesExhausted =
        new("technician.codes_exhausted",
            "مش لاقيين كود فني فاضي. ده معناه إن الأكواد خلصت تقريباً.", 500);
}
