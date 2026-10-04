using Codlek.Application.Abstractions;

namespace Codlek.Application.Features.Hardware;

/// <summary>
/// أسباب فشل نقط العتاد.
///
/// <para>⚠️ الرسايل منقولة من المشروع القديم بالحرف — المشروعين
/// هيشتغلوا على نفس القاعدة فترة التحويل.</para>
/// </summary>
public static class HardwareErrors
{
    public static readonly Error ReportNotFound =
        new("hardware.report_not_found", "الفحص مش موجود.", 404);

    public static readonly Error DeviceNotFound =
        new("hardware.device_not_found", "الجهاز مش موجود.", 404);

    /// <summary>
    /// 🔴 <b>الحارس اللي بيمنع فني يقرا شغل فني تاني.</b>
    ///
    /// <para>صفحة الفحص نفسها بترفض تفتح فحص مش بتاعه، فمن غير
    /// الحارس ده هو بيوصل للّقطة من الرابط ده — يقرا سيريالات بضاعة
    /// مش شغله.</para>
    /// </summary>
    public static readonly Error NotYourReport =
        new("hardware.not_your_report", "الفحص ده مش بتاعك.", 403);

    /// <summary>
    /// ⚠️ اللاب اللي مالوش ولا لقطة — والواجهة بتفرّق بين ده وبين
    /// «لقطة فاضية».
    /// </summary>
    public static readonly Error NoSnapshot =
        new("hardware.no_snapshot", "الجهاز ده مالوش ولا لقطة عتاد.", 404);

    public static readonly Error CompareNeedsTwo =
        new("hardware.compare_needs_two", "لازم تحدّد الفحصين المطلوب مقارنتهم.", 400);

    public static readonly Error CompareSameReport =
        new("hardware.compare_same_report", "اختار فحصين مختلفين.", 400);

    /// <summary>
    /// 🔴 <b>الرسالة دي هي الحارس.</b>
    ///
    /// <para>التقييد بالشركة لوحده بيخلّي حد يقارن جهازين مختلفين
    /// بمجرد تغيير الأرقام في الرابط — ويطلّع فروق عتاد مالهاش أي
    /// معنى.</para>
    /// </summary>
    public static readonly Error ReportNotOnDevice =
        new("hardware.report_not_on_device", "واحد من الفحصين مش بتاع الجهاز ده.", 404);
}
