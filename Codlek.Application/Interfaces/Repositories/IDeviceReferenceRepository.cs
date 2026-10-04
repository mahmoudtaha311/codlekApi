using Codlek.Core.Enums;

namespace Codlek.Application.Interfaces.Repositories;

/// <summary>
/// القرايتين اللي تتبّع سلسلة الدمج محتاجهم — <b>وبس</b>.
///
/// <para>⚠️ <b>اللفّة نفسها مش هنا عن قصد.</b> عدد الخطوات وكشف
/// الحلقة وتخطّي شواهد القبور كلهم <b>منطق</b>، وبيتجرّبوا
/// بمزيّف؛ والاستعلامين دول <b>قرايتين</b> وبيتقاسوا على قاعدة
/// حقيقية. خلطهم كان بيخلّي نص الحالات مش ممكن تتقاس.</para>
/// </summary>
public interface IDeviceReferenceRepository
{
    /// <summary>حالة الأجهزة دي — اللي مالوش صف مابيظهرش.</summary>
    Task<IReadOnlyDictionary<Guid, DeviceMergeState>> DeviceStatesAsync(
        Guid tenantId, IReadOnlyCollection<Guid> ids, CancellationToken ct = default);

    /// <summary>
    /// الأسامي المستعارة دي بتشاور على إيه — اللي مش مستعار مابيظهرش.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, Guid>> AliasTargetsAsync(
        Guid tenantId, IReadOnlyCollection<Guid> aliasIds, CancellationToken ct = default);
}

/// <summary>
/// 🔴 <b>الحالة + اللي اتدمج فيه — والاتنين مع بعض عشان شاهد القبر
/// يتعرف.</b>
/// </summary>
public sealed class DeviceMergeState
{
    public DeviceLifecycleStatus Status { get; init; }

    public Guid? MergedIntoDeviceId { get; init; }

    /// <summary>الصف ده شاهد قبر؟</summary>
    public bool IsTombstone =>
        Status == DeviceLifecycleStatus.Merged
        && MergedIntoDeviceId is { } into
        && into != Guid.Empty;
}
