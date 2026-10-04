namespace Codlek.Core.Enums;

/// <summary>
/// اسم حالة الراكة بالعربي — <b>المصدر الوحيد</b>.
///
/// <para>⚠️ <b>كان فيه نسختين من الخريطة دي في القديم:</b> واحدة في
/// قايمة الراكات وواحدة في اللوحة. واللي في اللوحة كانت بترجع
/// لـ<c>ToString()</c> في الحالة المجهولة، فراكة لسه ماتفعّلتش كانت
/// بتظهر «PendingPairing» بالإنجليزي جمب أسماء عربية.</para>
/// </summary>
public static class RackStatusText
{
    public static string Arabic(RackStatus status) => status switch
    {
        RackStatus.Active => "نشطة",
        RackStatus.Suspended => "موقوفة",
        RackStatus.Revoked => "ملغية",

        // ⚠️ والافتراضي عربي كمان — مش اسم الـenum.
        _ => "مستنية التفعيل",
    };
}
