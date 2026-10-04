namespace Codlek.Application.Contracts.Hardware;

/// <summary>لقطة عتاد فحص واحد.</summary>
public sealed record ReportHardwareResponse(
    Guid ReportId,
    Guid? DeviceId,
    string DevicePublicCode,
    DateTime StartedAtUtc,
    DateTime? CapturedAtUtc,
    string TechnicianName,
    string TechnicianCode,
    string RackCode,
    string CollectorVersion,

    /// <summary>
    /// 🔴 اللقطة ناقصة؟ — <b>ده أهم حقل في الرد</b>.
    ///
    /// <para>اللقطة الناقصة معناها إن غياب أي قطعة <b>مش</b> مثبت.
    /// الشاشة لازم تقول كده صريح، مش تعرض الجدول كأنه كامل.</para>
    /// </summary>
    bool IsPartial,

    /// <summary>
    /// ⚠️ من غير صلاحيات مسؤول، أغلب السيريالات مابتتقراش — فالثقة
    /// بتنزل والمقارنة بتبقى «مش مؤكّدة» من غير ما يكون فيه أي
    /// تبديل.
    /// </summary>
    bool RanAsAdministrator,

    string Warnings,
    IReadOnlyList<HardwareComponentItem> Components);
