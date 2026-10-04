using Codlek.Application.Abstractions;

namespace Codlek.Application.Features.Rack.RegisterRack;

/// <summary>
/// أسباب فشل التسجيل — <b>وأكواد الحالة مجمّدة</b>.
///
/// <para>🔴 <b>والراكة بتفرّق بينهم بالرقم.</b> <c>404</c> معناها
/// «جرّب كود تاني»، و<c>410</c> معناها «الكود ده خلص»، و<c>409</c>
/// معناها «حد سبقك» — وكل واحدة بتدّي للفني تعليمة مختلفة. ودمجهم
/// في <c>400</c> واحدة كان بيخلّي الشاشة تقول «في مشكلة» وخلاص.</para>
///
/// <para>⚠️ <b>وكود الخطأ النصي جزء من العقد كمان:</b> الراكة
/// بتقرا <c>code</c> من الجسم وبتعرض رسالة محلية بيه.</para>
/// </summary>
public static class RackRegisterErrors
{
    /// <summary>
    /// ⚠️ <b>نفس الرد للكود الناقص والغلط والمستخدم
    /// والمقفول.</b> اللي بيحاول مالوش يعرف إيه اللي ناقص.
    /// </summary>
    public static readonly Error InvalidCode =
        new("InvalidCode", "كود الاقتران غلط أو اتستخدم قبل كده.", 404);

    public static readonly Error CodeExpired =
        new("CodeExpired",
            "كود الاقتران انتهت صلاحيته. اطلب واحد جديد من لوحة الإدارة.", 410);

    /// <summary>
    /// 🔴 <b>حد سبقك باللحظة.</b> الاستهلاك مشروط، فراكتين بنفس
    /// الكود: واحدة بتكسب والتانية بتاخد ده.
    /// </summary>
    public static readonly Error CodeUsed =
        new("CodeUsed", "كود الاقتران ده اتستخدم من راكة تانية للتو.", 409);

    /// <summary>
    /// ⚠️ الكود اتعمل لشركة اتمسحت بعدين — واللي بيسجّل مالوش يد
    /// فيها، فالرد «مقفول» مش «غلط».
    /// </summary>
    public static readonly Error TenantMissing =
        new("TenantMissing", "الشركة مش موجودة.", 423);
}
