namespace Codlek.Application.Interfaces.Repositories;

/// <summary>
/// ترويسة لقطة — <b>من غير مكوّنات</b>.
///
/// <para>⚠️ ومن غير عدد مكوّنات كمان، عن قصد. الاستعلام اللي
/// بيجيب ترويسة فحص واحد مابيعدّش الصفوف — المنادي بيعدّ من
/// القايمة اللي حمّلها أصلاً. راجع
/// <c>CompareSideInfo.ComponentCount</c>.</para>
/// </summary>
public sealed record SnapshotHeaderFacts(
    Guid ReportId,
    Guid? DeviceId,

    /// <summary>
    /// 🔴 كود الجهاز <b>زي ما اللقطة سجّلته</b> — احتياطي بس.
    ///
    /// <para>الجهاز المربوط هو مصدر الكود الحقيقي؛ ده بيستعمل
    /// للفحص اللي لسه مش مربوط بجهاز.</para>
    /// </summary>
    string SnapshotDeviceCode,

    DateTime StartedAtUtc,
    DateTime? CapturedAtUtc,
    string TechnicianName,
    string TechnicianCode,
    Guid? SourceRackId,
    string CollectorVersion,
    bool IsPartial,
    bool RanAsAdministrator,
    string Warnings,
    string CommercialModelName);
