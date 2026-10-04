using Codlek.Core.Enums;

namespace Codlek.Application.Interfaces.Repositories;

/// <summary>
/// صف لاب خام — <b>بالـenums مش بالنصوص</b>.
///
/// <para>⚠️ ومفيش حرف عربي هنا: الترجمة في الـHandler بعد
/// القراية.</para>
/// </summary>
public sealed record DeviceListRow(
    Guid Id,
    string PublicCode,
    string Manufacturer,
    string RawModel,
    string CommercialModelName,
    string MachineType,
    DeviceLifecycleStatus Status,
    DeviceIdentityConfidence Confidence,
    DateTime LastSeenAtUtc,
    DeviceOperationalStage Stage,
    DateTime? StageChangedAtUtc,
    Guid? CurrentLocationId,
    Guid? CurrentHolderTechnicianId,
    Guid? ContainerId,
    string ContainerCode,
    DateTime? PartChangedAtUtc,
    string PartChangeSummary,

    /// <summary>
    /// 🔴 كود اللاب الكانوني لو ده مدموج — عشان الكود المتقاعد
    /// يوصّل للاب الحقيقي بدل صفحة ميتة.
    /// </summary>
    string? MergedIntoCode,

    int TestCount,
    DeviceLastTestRow? Last);
