using Codlek.Core.Text;

namespace Codlek.Application.Features.Reports;

/// <summary>
/// حدود المسح والاسترجاع.
///
/// <para>🔴 <b>المسح بعلامة، مش حذف.</b> الصف بيفضل، وبيخرج من كل
/// عدّ لأن كل قراية عدّ بتفلتر <c>!IsDeleted</c> — اللوحة والجرد
/// والأجهزة والصيانة والتقرير اليومي.</para>
/// </summary>
public static class ReportDeletionRules
{
    /// <summary>
    /// 🔴 <b>سبب المسح ٥ حروف على الأقل — بعد التقليم.</b>
    ///
    /// <para>المالك بيراجع اللي اتمسح؛ مسح من غير سبب معناه رقم في
    /// تقييم فني اختفى ومحدش يعرف ليه.</para>
    /// </summary>
    public const int MinDeleteReasonLength = 5;

    /// <summary>طول عمودي <c>DeletedReason</c> و<c>RestoredReason</c>.</summary>
    public const int MaxReasonLength = TextClip.Lengths.Reason;

    /// <summary>
    /// اسم الفحص في سطر السجل — كود الجهاز وقت الفحص.
    ///
    /// <para>⚠️ الفحوص القديمة المستوردة ممكن تبقى من غير كود، وسطر
    /// «فحص  اتمسح» بمسافتين بيتقري على إنه عطل.</para>
    /// </summary>
    public static string DeviceLabel(string? deviceCode) =>
        string.IsNullOrWhiteSpace(deviceCode) ? "من غير كود" : deviceCode;
}
