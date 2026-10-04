using Codlek.Core.Enums;

namespace Codlek.Application.Interfaces.Repositories;

/// <summary>
/// صف لاب للتصدير — <b>أعمدة أقل من القايمة</b>.
///
/// <para>⚠️ الملف مالوش «آخر فحص» كامل ولا حائز: دي أعمدة بتتقرا
/// على الشاشة بالتفاعل، ومفيش معنى لإنها تنزل في إكسل. والحاوية
/// <b>موجودة</b> — المدير اللي بيصدّر شحنة كاملة محتاج الرمز في
/// الشيت.</para>
/// </summary>
public sealed record DeviceExportRow(
    string PublicCode,
    string Manufacturer,
    string RawModel,
    string CommercialModelName,
    string ContainerCode,
    DeviceLifecycleStatus Status,
    DeviceIdentityConfidence Confidence,
    DeviceOperationalStage Stage,
    string LocationName,
    int TestCount,
    DateTime FirstSeenAtUtc,
    DateTime LastSeenAtUtc);
