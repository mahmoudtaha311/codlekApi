using Codlek.Core.Entities;

namespace Codlek.Application.Interfaces.Repositories;

/// <summary>
/// بلوكات أكواد الأجهزة المؤجّرة للراكات.
///
/// <para>🔴 <b>والبلوك هو اللي بيخلّي الراكة تشتغل أوفلاين.</b>
/// الفني بيفحص لاب جديد وهو مقطوع عن النت، واللاب محتاج كود فوراً
/// عشان الليبل يتطبع.</para>
/// </summary>
public interface IDeviceCodeLeaseRepository
{
    /// <summary>
    /// بلوكات الراكة المفتوحة — <b>متتبّعة</b> عشان تتقفل.
    /// </summary>
    Task<IReadOnlyList<DeviceCodeLease>> OpenForRackAsync(
        Guid tenantId, Guid rackId, CancellationToken ct = default);

    /// <summary>
    /// البلوك اللي الرقم ده جوّاه — <b>متتبّع</b>.
    ///
    /// <para>⚠️ بيدوّر بالمدى مش بالمعرّف: الراكة بتقول «استهلكت لحد
    /// الرقم كذا» ومابتعرفش معرّف البلوك.</para>
    /// </summary>
    Task<DeviceCodeLease?> ContainingAsync(
        Guid tenantId, Guid rackId, int number, CancellationToken ct = default);

    void Add(DeviceCodeLease lease);
}
