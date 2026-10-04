using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Entities;
using Codlek.Core.Enums;
using Codlek.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Codlek.Infrastructure.Repositories;

public sealed class DeviceCodeLeaseRepository(AppDbContext db) : IDeviceCodeLeaseRepository
{
    /// <summary>
    /// 🔴 <b>متتبّعة</b> — المعالج بيقفلهم في مكانهم والحفظ من وحدة
    /// العمل.
    /// </summary>
    public async Task<IReadOnlyList<DeviceCodeLease>> OpenForRackAsync(
        Guid tenantId, Guid rackId, CancellationToken ct = default) =>
        await db.DeviceCodeLeases
            .Where(l => l.TenantId == tenantId
                     && l.RackId == rackId
                     && l.Status == DeviceCodeLeaseStatus.Open)
            .ToListAsync(ct);

    /// <summary>
    /// ⚠️ <b>بيدوّر بالمدى</b> — الراكة بتقول رقم، مش معرّف بلوك.
    /// </summary>
    public Task<DeviceCodeLease?> ContainingAsync(
        Guid tenantId, Guid rackId, int number, CancellationToken ct = default) =>
        db.DeviceCodeLeases
            .FirstOrDefaultAsync(
                l => l.TenantId == tenantId
                  && l.RackId == rackId
                  && l.FromNumber <= number
                  && l.ToNumber >= number, ct);

    public void Add(DeviceCodeLease lease) => db.DeviceCodeLeases.Add(lease);
}
